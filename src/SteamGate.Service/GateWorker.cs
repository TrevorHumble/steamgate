using System.Diagnostics.CodeAnalysis;
using SteamGate.Core;

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
    private long _tickCount;

    // Any unhandled error ends the process with exit code 1: a clean stop (exit code 0) would not make Windows restart it.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Last-resort handler that logs and ends the process so Windows restarts it.")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Return to the caller before any file work.
            await Task.Yield();

            var loaded = new SettingsStore(SteamGatePaths.DataFolder).Load();
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

        return Task.CompletedTask;
    }
}
