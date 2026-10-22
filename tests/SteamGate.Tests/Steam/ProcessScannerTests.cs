using System.Diagnostics;
using SteamGate.Service;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class ProcessScannerTests
{
    [Fact]
    public void Scan_FindsSelf()
    {
        using var self = Process.GetCurrentProcess();

        var found = ProcessScanner.Scan().Single(entry => entry.Pid == Environment.ProcessId);

        Assert.Equal(Environment.ProcessPath, found.ExePath, ignoreCase: true);
        Assert.InRange(Math.Abs((found.StartUtc - self.StartTime.ToUniversalTime()).TotalSeconds), 0, 2);
    }
}
