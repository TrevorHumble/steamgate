using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class ConnectionRulesTests
{
    private static List<ClientInfo> Clients(int count, Func<int, bool> verified) =>
        [.. Enumerable.Range(1, count).Select(id => new ClientInfo(id, verified(id)))];

    [Fact]
    public void NonHelloFirstLineDrops()
    {
        Assert.Null(ConnectionRules.AcceptedHello(new WarnMessage(5)));
        Assert.Null(ConnectionRules.AcceptedHello(new CloseMessage("g", "g", [1])));
        Assert.Null(ConnectionRules.AcceptedHello(null));
        Assert.Equal(new HelloMessage("status"), ConnectionRules.AcceptedHello(new HelloMessage("status")));
        Assert.Equal(new HelloMessage("helper"), ConnectionRules.AcceptedHello(new HelloMessage("helper")));
    }

    [Fact]
    public void HelloTimeoutIsTenSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), ConnectionRules.HelloTimeout);
    }

    [Fact]
    public void AfterHelloClientsMaySendOnlyRequestAndCancel()
    {
        Assert.True(ConnectionRules.DropsOnClientMessage(new WarnMessage(1)));
        Assert.True(ConnectionRules.DropsOnClientMessage(new CloseMessage("g", "g", [])));
        Assert.True(ConnectionRules.DropsOnClientMessage(new ReplyMessage("r", true, "x", null)));
        Assert.True(ConnectionRules.DropsOnClientMessage(new HelloMessage("status")));
        Assert.False(ConnectionRules.DropsOnClientMessage(new RequestMessage("r1", "enabled", null, true, null, null)));
        Assert.False(ConnectionRules.DropsOnClientMessage(new CancelMessage("r2", Guid.NewGuid())));
    }

    [Fact]
    public void StatusMaySendRequests()
    {
        var hello = ConnectionRules.AcceptedHello(new HelloMessage("status"));

        Assert.NotNull(hello);
        Assert.False(ConnectionRules.DropsOnClientMessage(new RequestMessage(new string('a', 64), "weeknightMinutes", 120, null, null, null)));
        Assert.False(ConnectionRules.DropsOnClientMessage(new CancelMessage(new string('a', 64), Guid.NewGuid())));
        Assert.True(ConnectionRules.DropsOnClientMessage(new RequestMessage(new string('a', 65), "weeknightMinutes", 120, null, null, null)));
        Assert.True(ConnectionRules.DropsOnClientMessage(new CancelMessage(new string('a', 65), Guid.NewGuid())));
        Assert.Equal(64, ConnectionRules.MaxRequestIdLength);
    }

    [Fact]
    public void IdleStatusDropped()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), ConnectionRules.IdleLimit);
        Assert.Equal(ConnectionRules.IdleLimit, ConnectionRules.IdleLimitFor(verifiedHelper: false));
        Assert.Null(ConnectionRules.IdleLimitFor(verifiedHelper: true));
    }

    [Fact]
    public void EveryoneJoinsBelowTheLimit()
    {
        var decision = ConnectionRules.Admit(Clients(7, _ => false), newIsVerifiedHelper: false);

        Assert.Equal(new AdmitDecision(true, null), decision);
        Assert.Equal(new AdmitDecision(true, null), ConnectionRules.Admit([], newIsVerifiedHelper: false));
    }

    [Fact]
    public void EvictsOldestUnverified()
    {
        // Clients 1 and 2 are verified; 3 is the oldest unverified.
        var clients = Clients(8, id => id <= 2);

        var decision = ConnectionRules.Admit(clients, newIsVerifiedHelper: true);

        Assert.Equal(new AdmitDecision(true, 3), decision);
    }

    [Fact]
    public void NinthNonHelperDropped()
    {
        var decision = ConnectionRules.Admit(Clients(8, _ => false), newIsVerifiedHelper: false);

        Assert.Equal(new AdmitDecision(false, null), decision);
    }

    [Fact]
    public void AHelperIsDroppedWhenEveryClientIsAVerifiedHelper()
    {
        var decision = ConnectionRules.Admit(Clients(8, _ => true), newIsVerifiedHelper: true);

        Assert.Equal(new AdmitDecision(false, null), decision);
    }

    [Fact]
    public void LimitIsEightClients()
    {
        Assert.Equal(8, ConnectionRules.MaxClients);
    }
}
