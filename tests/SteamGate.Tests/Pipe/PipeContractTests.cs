using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class PipeContractTests
{
    [Fact]
    public void NameAndPathAgree()
    {
        Assert.Equal("SteamGate", PipeContract.PipeName);
        Assert.Equal(@"\\.\pipe\SteamGate", PipeContract.PipePath);
    }

    [Fact]
    public void ClientRightsAreExactlyTheOnesThePipeGrants()
    {
        // Read data, write data, read attributes, read extended attributes, read permissions, synchronize; no create-instance (0x4), no write attributes (0x100), no append.
        Assert.Equal(0x0012_008Bu, PipeContract.ClientAccessMask);
        Assert.Equal(0u, PipeContract.ClientAccessMask & 0x4u);
        Assert.Equal(0u, PipeContract.ClientAccessMask & 0x100u);
        Assert.Equal(0u, PipeContract.ClientAccessMask & 0x80000000u);
    }
}
