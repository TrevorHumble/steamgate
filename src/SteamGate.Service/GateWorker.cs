using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Channels;
using SteamGate.Core;
using SteamGate.Core.Accounting;
using SteamGate.Core.Enforcement;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Steam;

namespace SteamGate.Service;

/// <summary>
/// The one loop that carries every periodic step; later issues add their steps inside RunTickAsync.
/// No other periodic loop or timer.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the service container.")]
internal sealed class GateWorker : BackgroundService
{

    // Ships in the product because the install test uses it to prove a crash is restarted; only an admin can set it.
    private const string FaultTestVariable = "STEAMGATE_FAULT_TEST";

    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);
    private static readonly int TicksPerMinute = (int)(TimeSpan.FromMinutes(1) / ScanInterval);

    // About every 30 seconds the worker rechecks the helpers and starts one if none is connected.
    private const int HelperCheckTicks = 6;

    // While anything is pending the settings are saved about once a minute, to record the countdown.
    private const int PendingSaveTicks = 12;

    private readonly GateLog _log;
    private readonly PipeServer _pipe;
    private readonly HelperLauncher _launcher;
    private readonly SnapshotHolder _snapshots;
    private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
    private readonly SteamLibraries _steam = new();
    private readonly ChannelReader<SettingsCommand> _commands;
    private readonly RateLimitedLog _requestLog;
    private long _tickCount;

    // The one in-memory copy of the settings, loaded at start and never re-read from disk. The lock changes it inside RunTickAsync,
    // before the matcher, Budget, and the planner read it, so a change that applies takes effect in the same tick.
    private readonly SettingsStore _settingsStore = new(SteamGatePaths.DataFolder);
    private GateSettings _settings = GateSettings.Defaults;
    private bool _settingsDirty;
    private Guid? _bootId;
    private bool _firstLockTick = true;
    private long _prevUptimeMs;

    private readonly TallyStore _tallyStore = new(SteamGatePaths.DataFolder);
    private Tally _tally = Tally.Empty;
    private readonly StepTimer _stepTimer = new();

    // Enforcement: the planner's memory, started from this service's own start time.
    private PlannerState _planner = PlannerState.Initial(DateTime.UtcNow);

    public GateWorker(SnapshotHolder snapshots, GateLog log, PipeServer pipe, Channel<SettingsCommand> commands, RateLimitedLog requestLog)
    {
        _snapshots = snapshots;
        _log = log;
        _pipe = pipe;
        _commands = commands.Reader;
        _requestLog = requestLog;
        _launcher = new HelperLauncher(log);
    }

    // Any unhandled error ends the process with exit code 1: a clean stop (exit code 0) would not make Windows restart it.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Last-resort handler that logs and ends the process so Windows restarts it.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Return to the caller before any file work.
            await Task.Yield();

            var loaded = _settingsStore.Load();
            _settings = loaded.Value;
            _bootId = BootMarker.Open();
            if (loaded.Corrupt)
            {
                _log.Write("settings corrupt, defaults restored");
            }

            _log.Write(FormattableString.Invariant($"started pid={Environment.ProcessId}"));

            if (Environment.GetEnvironmentVariable(FaultTestVariable) == "1")
            {
                throw new InvalidOperationException("fault test");
            }

            var tallyLoaded = _tallyStore.Load();
            _tally = tallyLoaded.Value;
            if (tallyLoaded.Corrupt)
            {
                _log.Write("tally corrupt, reset");
            }

            _log.Write(FormattableString.Invariant($"tally loaded night={_tally.Night:yyyy-MM-dd} seconds={_tally.NightSeconds.ToString("R", CultureInfo.InvariantCulture)}"));

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunTickAsync(stoppingToken).ConfigureAwait(false);
                try
                {
                    await Task.Delay(ScanInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            SaveTally();
            if (_settingsDirty || _settings.Pending.Count > 0)
            {
                SaveSettings(DateTime.UtcNow);
            }
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && stoppingToken.IsCancellationRequested))
        {
            try
            {
                _log.Write(FormattableString.Invariant($"unhandled pid={Environment.ProcessId} {ex.Message}"));
            }
            finally
            {
                Environment.Exit(1);
            }
        }
    }

    private Task RunTickAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _tickCount++;
        if (_tickCount % TicksPerMinute == 0)
        {
            _log.Write("alive");
        }

        // The pipe is a convenience: if it is down this retries it and nothing else changes.
        _pipe.EnsureListening();
        if (_tickCount % HelperCheckTicks == 0)
        {
            _pipe.RecheckHelpers();
            if (!_pipe.HelperConnected)
            {
                _launcher.Start();
            }
        }

        // One clock reading per tick, so the local and UTC times agree.
        var now = DateTimeOffset.Now;
        var localNow = now.DateTime;
        RunLock(now.UtcDateTime);

        var (libraries, installed, commonTargets, refreshed) = _steam.Refresh(
            SteamRootLocator.Locate,
            _sinceStart.Elapsed,
            LocalPathPolicy.IsLocal,
            path => _log.Write("manifest skipped " + path));
        if (refreshed)
        {
            _log.Write(FormattableString.Invariant($"libraries {libraries.Count}"));
        }

        var games = GameMatcher.Match(ProcessScanner.Scan(), libraries, installed, _settings.NotCounted, commonTargets);

        // The last published snapshot is the previous tick's games; only this worker publishes.
        var (started, stopped) = GameTransitions.Diff(
            _snapshots.Current.Games.Select(game => game.Folder),
            games.Select(game => game.Folder));
        foreach (var folder in started)
        {
            _log.Write("game started " + folder);
        }

        foreach (var folder in stopped)
        {
            _log.Write("game stopped " + folder);
        }

        CountTime(games, localNow, now.UtcDateTime);

        var remaining = Budget.RemainingSeconds(_tally, _settings, localNow);
        if (games.Count > 0 && _tickCount % TicksPerMinute == 0)
        {
            _log.Write(FormattableString.Invariant($"remaining {remaining:F0}"));
        }

        var snapshot = new GateSnapshot(games, installed)
        {
            Tally = _tally,
            RemainingSeconds = remaining,
            TonightLimitMinutes = Budget.NightLimitMinutes(NightClock.NightOf(localNow), _settings),
            Settings = _settings,
        };
        Enforce(snapshot);
        _snapshots.Publish(snapshot with { HelperConnected = _pipe.HelperConnected });
        return Task.CompletedTask;
    }

    // The lock, in this order: count the pending entries down, answer the requests that arrived, apply the entries that are due.
    // The one in-memory settings object is replaced here, so everything later in the tick reads the result.
    private void RunLock(DateTime utcNow)
    {
        var uptimeMs = Environment.TickCount64;
        var clock = _settings.Clock;
        var sameBoot = _bootId is { } boot && clock?.BootId == boot;
        var step = ChangeEngine.StepSeconds(_firstLockTick, sameBoot, utcNow, _prevUptimeMs, uptimeMs, clock?.LastSavedUtc, clock?.LastSavedUptimeMs);
        _firstLockTick = false;
        _prevUptimeMs = uptimeMs;

        var settings = ChangeEngine.Countdown(_settings, step);
        var answered = new List<(SettingsCommand Command, ChangeOutcome Outcome)>();
        while (_commands.TryRead(out var command))
        {
            var outcome = command.Request is { } request
                ? ChangeEngine.Request(settings, request, utcNow, Guid.NewGuid)
                : ChangeEngine.Cancel(settings, command.CancelId ?? Guid.Empty);
            settings = outcome.Updated;
            _settingsDirty |= outcome.Changed;
            var result = ChangeLog.ResultName(outcome.Result);
            _requestLog.Write(command.Request is { } logged
                ? ChangeLog.Request(logged.Field, result, command.ClientPid, command.SessionId)
                : ChangeLog.Cancel(result, command.ClientPid, command.SessionId));
            answered.Add((command, outcome));
        }

        var (updated, applied, skipped) = ChangeEngine.ApplyDue(settings);
        settings = updated;
        foreach (var entry in applied)
        {
            _log.Write(ChangeLog.Applied(entry));
        }

        foreach (var entry in skipped)
        {
            _log.Write(ChangeLog.Skipped(entry));
        }

        _settingsDirty |= applied.Count > 0 || skipped.Count > 0;
        _settings = settings;

        var saveFailed = false;
        if (_settingsDirty || (settings.Pending.Count > 0 && _tickCount % PendingSaveTicks == 0))
        {
            saveFailed = !SaveSettings(utcNow);
        }

        // Replies go out after the save attempt, so each can say whether the change is only held in memory.
        foreach (var (command, outcome) in answered)
        {
            command.Replies.TryPost(ChangeMessages.Reply(command.RequestId, outcome, saveFailed));
        }
    }

    // Saves with the clock bookkeeping stamped in. A failed save leaves the settings marked unsaved, so the next tick tries again.
    private bool SaveSettings(DateTime utcNow)
    {
        var stamped = _settings with { Clock = new LockClock(utcNow, Environment.TickCount64, _bootId) };
        if (!_settingsStore.Save(stamped))
        {
            _log.Write("save failed settings.json");

            // Unsaved: the next tick tries again, even when this save was only the once-a-minute countdown record.
            _settingsDirty = true;
            return false;
        }

        _settings = stamped;
        _settingsDirty = false;
        return true;
    }

    // Runs the planner once and carries out what it asks. A warning that no helper could show is kept and shown when one connects.
    private void Enforce(GateSnapshot snapshot)
    {
        var remaining = snapshot.RemainingSeconds ?? double.NaN;
        var (next, actions) = EnforcementPlanner.Step(_planner, remaining, snapshot.Games, _settings.Enabled, _sinceStart.Elapsed);
        _planner = next;
        foreach (var action in actions)
        {
            switch (action)
            {
                case Warn warn:
                    _log.Write(FormattableString.Invariant($"warn {warn.Minutes}"));
                    DeliverWarning(warn);
                    break;
                case PoliteClose polite:
                    _log.Write("polite close " + polite.Game.Folder);
                    _pipe.PostToHelpers(new CloseMessage(polite.Game.Folder, polite.Game.DisplayName, [.. polite.Game.Processes.Select(process => process.Pid)]));
                    break;
                case ForceClose force:
                    ForceCloseGame(force.Game);
                    break;
            }
        }

        var replay = EnforcementPlanner.ReplayWarn(_planner, remaining, snapshot.Games.Count > 0);
        if (replay is not null && _pipe.HelperConnected)
        {
            DeliverWarning(replay);
        }
    }

    private void DeliverWarning(Warn warn)
    {
        _planner = _pipe.PostToHelpers(new WarnMessage(warn.Minutes)) > 0
            ? EnforcementPlanner.MarkDelivered(_planner)
            : EnforcementPlanner.MarkUndelivered(_planner);
    }

    private void ForceCloseGame(RunningGame game)
    {
        _log.Write("force close " + game.Folder);
        foreach (var process in game.Processes)
        {
            if (!ProcessKiller.Kill(process.Pid, process.StartUtc))
            {
                _log.Write(FormattableString.Invariant($"force close not done pid={process.Pid}"));
            }
        }
    }

    // Counts this tick, whether or not SteamGate is enabled. On the first tick after start the downtime counts only
    // when a game is open now and was open at the last count; StepTimer chooses the step and its cap.
    private void CountTime(IReadOnlyList<RunningGame> games, DateTime localNow, DateTime utcNow)
    {
        var (step, maxStep) = _stepTimer.Next(_tally, utcNow, games.Count > 0);
        var next = Accountant.Tick(_tally, localNow, utcNow, step, maxStep, games);
        var changed = next != _tally;
        _tally = next;

        // Rewrite once a minute even when unchanged, so a lost or hand-edited file is restored within a minute.
        if (changed || _tickCount % TicksPerMinute == 0)
        {
            SaveTally();
        }
    }

    private void SaveTally()
    {
        if (!_tallyStore.Save(_tally))
        {
            _log.Write("save failed tally.json");
        }
    }
}
