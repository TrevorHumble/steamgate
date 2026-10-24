using SteamGate.Core.Pipe;
using SteamGate.Core.Status;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class StatusFeedTests
{
    private static readonly StateMessage A = StateFixtures.State();
    private static readonly StateMessage B = StateFixtures.State(nightSeconds: 2825);

    [Fact]
    public void OnlyOnChange()
    {
        var feed = new StatusFeed();

        Assert.True(feed.Offer(A));
        Assert.False(feed.Offer(A));
        Assert.True(feed.Offer(B));
        Assert.False(feed.Offer(B));
        Assert.Equal(B, feed.Latest);
    }

    [Fact]
    public void ForgetKeepsNothing()
    {
        var feed = new StatusFeed();
        Assert.Null(feed.Latest);
        feed.Offer(A);
        Assert.Equal(A, feed.Latest);

        feed.Forget();

        Assert.Null(feed.Latest);
        Assert.True(feed.Offer(A));
        Assert.Equal(A, feed.Latest);
    }

    [Fact]
    public void TheSameLineIsTheSameState()
    {
        var feed = new StatusFeed();
        feed.Offer(A);

        Assert.False(feed.Offer(StateFixtures.State()));
        Assert.True(feed.Offer(StateFixtures.State(nightSeconds: 1)));
    }

    [Fact]
    public void ARefusedOfferStoresNothingNew()
    {
        var feed = new StatusFeed();
        feed.Offer(A);

        feed.Offer(A);

        Assert.Equal(A, feed.Latest);
    }
}
