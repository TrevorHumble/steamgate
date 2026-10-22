using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SteamGate.Core;
using SteamGate.Core.Accounting;
using SteamGate.Core.Enforcement;
using SteamGate.Core.Pipe;
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

    private readonly GateLog _log;
    private readonly PipeServer _pipe;
    private readonly HelperLauncher _launcher;
    private readonly SnapshotHolder _snapshots;
    private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
    private readonly SteamLibraries _steam = new();
    private long _tickCount;

    // The settings loaded at start; a later issue makes them follow changes.
    private GateSettings _settings = GateSettings.Defaults;

    private readonly TallyStore _tallyStore = new(SteamGatePaths.DataFolder);
    private Tally _tally = Tally.Empty;
    private readonly StepTimer _stepTimer = new();

    // Enforcement: the planner's memory, started from this service's own start time.
    private PlannerState _planner = PlannerState.Initial(DateTime.UtcNow);

    public GateWorker(SnapshotHolder snapshots, GateLog log, PipeServer pipe)
    {
        _snapshots = snapshots;
        _log = log;
        _pipe = pipe;
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

            var loaded = new SettingsStore(SteamGatePaths.DataFolder).Load();
            _settings = loaded.Value;
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

        // One clock reading per tick, so the local and UTC times agree.
        var now = DateTimeOffset.Now;
        var localNow = now.DateTime;
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
        };
        Enforce(snapshot);
        _snapshots.Publish(snapshot with { HelperConnected = _pipe.HelperConnected });
        return Task.CompletedTask;
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
