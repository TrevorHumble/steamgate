using System.Diagnostics;
using Microsoft.Win32.SafeHandles;
using SteamGate.Service;
using Xunit;

namespace SteamGate.Tests.Service;

public sealed class ProcessKillerTests
{
    private const string SlowInMutationRun = "starts and waits on a child process; it exercises only the service, which Stryker does not mutate";

    private static Process StartPing()
    {
        var info = new ProcessStartInfo("ping.exe", "-n 30 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
        return Process.Start(info) ?? throw new InvalidOperationException("ping did not start");
    }

    // The start time as ProcessScanner reads it, so the test uses the same path as the service.
    private static DateTime TrueStartUtc(int pid) => ProcessScanner.Scan().Single(sample => sample.Pid == pid).StartUtc;

    [Fact]
    public void StartTimeMismatchNeverKills()
    {
        Assert.SkipWhen(StrykerRun.Active, SlowInMutationRun);
        using var child = StartPing();
        try
        {
            var startUtc = TrueStartUtc(child.Id);

            var wrong = ProcessKiller.Kill(child.Id, startUtc.AddTicks(1));
            var stillRunning = !child.WaitForExit(2000);
            var right = ProcessKiller.Kill(child.Id, startUtc);
            var exited = child.WaitForExit(5000);

            Assert.False(wrong);
            Assert.True(stillRunning);
            Assert.True(right);
            Assert.True(exited);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill();
            }
        }
    }

    [Fact]
    public void AStartTimeOneTickEarlierDoesNotKillEither()
    {
        Assert.SkipWhen(StrykerRun.Active, SlowInMutationRun);
        using var child = StartPing();
        try
        {
            var startUtc = TrueStartUtc(child.Id);

            Assert.False(ProcessKiller.Kill(child.Id, startUtc.AddTicks(-1)));
            Assert.False(child.WaitForExit(500));
        }
        finally
        {
            child.Kill();
        }
    }

    [Fact]
    public void AnUnknownProcessReturnsFalse()
    {
        Assert.False(ProcessKiller.Kill(int.MaxValue, DateTime.UtcNow));
        Assert.False(ProcessKiller.Kill(0, DateTime.UtcNow));
        Assert.False(ProcessKiller.Kill(-4, DateTime.UtcNow));
    }

    [Fact]
    public void StartTimeAgreesWithTheProcessClass()
    {
        using var self = Process.GetCurrentProcess();
        using var handle = ProcessImage.OpenForQuery(Environment.ProcessId);

        Assert.True(ProcessStartTime.TryRead(handle, out var startUtc));

        Assert.Equal(DateTimeKind.Utc, startUtc.Kind);
        Assert.InRange(Math.Abs((startUtc - self.StartTime.ToUniversalTime()).TotalSeconds), 0, 2);
    }

    [Fact]
    public void AnInvalidHandleGivesNoStartTime()
    {
        using var invalid = new SafeProcessHandle();

        Assert.False(ProcessStartTime.TryRead(invalid, out _));
    }
}
