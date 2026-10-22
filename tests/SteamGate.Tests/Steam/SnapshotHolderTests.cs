using SteamGate.Core;
using SteamGate.Core.Steam;
using SteamGate.Service;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class SnapshotHolderTests
{
    [Fact]
    public void Current_StartsEmpty_AndFollowsPublish()
    {
        var holder = new SnapshotHolder();
        var next = new GateSnapshot([], [new InstalledApp("G", "Game")]);

        Assert.Same(GateSnapshot.Empty, holder.Current);
        holder.Publish(next);

        Assert.Same(next, holder.Current);
        Assert.Empty(GateSnapshot.Empty.Games);
        Assert.Empty(GateSnapshot.Empty.Installed);
    }
}
