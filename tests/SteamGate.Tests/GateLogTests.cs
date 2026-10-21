using SteamGate.Service;
using Xunit;

namespace SteamGate.Tests;

public sealed class GateLogTests
{
    [Fact]
    public void Write_LogLocked_DropsLineWithoutThrowing()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("service.log");
        File.WriteAllText(path, "existing");
        using var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var exception = Record.Exception(() => new GateLog(temp.Folder).Write("x"));

        held.Dispose();
        Assert.Null(exception);
        Assert.Equal("existing", File.ReadAllText(path));
    }
}
