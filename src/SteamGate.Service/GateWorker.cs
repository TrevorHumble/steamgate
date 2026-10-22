using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using SteamGate.Core;
using SteamGate.Core.Steam;

namespace SteamGate.Service;

/// <summary>
/// The one loop that carries every periodic step; later issues add their steps inside RunTickAsync.
/// No other periodic loop or timer.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the service container.")]
internal sealed class GateWorker : BackgroundService
{
    private const int AliveEveryTicks = 12;

    // Ships in the product because the install test uses it to prove a crash is restarted; only an admin can set it.
    private const string FaultTestVariable = "STEAMGATE_FAULT_TEST";

    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);

    private readonly GateLog _log = new(SteamGatePaths.DataFolder);
    private readonly SnapshotHolder _snapshots;
    private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
    private readonly SteamLibraries _steam = new();
    private long _tickCount;

    // The settings loaded at start; a later issue makes the not-counted list follow changes.
    private IReadOnlyList<string> _notCounted = GateSettings.Defaults.NotCounted;

    public GateWorker(SnapshotHolder snapshots)
    {
        _snapshots = snapshots;
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
            _notCounted = loaded.Value.NotCounted;
            if (loaded.Corrupt)
            {
                _log.Write("settings corrupt, defaults restored");
            }

            _log.Write(FormattableString.Invariant($"started pid={Environment.ProcessId}"));

            if (Environment.GetEnvironmentVariable(FaultTestVariable) == "1")
            {
                throw new InvalidOperationException("fault test");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunTickAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(ScanInterval, stoppingToken).ConfigureAwait(false);
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
        if (_tickCount % AliveEveryTicks == 0)
        {
            _log.Write("alive");
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

        var games = GameMatcher.Match(ProcessScanner.Scan(), libraries, installed, _notCounted, commonTargets);

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

        _snapshots.Publish(new GateSnapshot(games, installed));
        return Task.CompletedTask;
    }
}
