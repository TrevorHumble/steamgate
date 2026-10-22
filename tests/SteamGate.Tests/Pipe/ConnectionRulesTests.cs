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
    public void ClientsMaySendOnlyHello()
    {
        Assert.True(ConnectionRules.DropsOnClientMessage(new WarnMessage(1)));
        Assert.True(ConnectionRules.DropsOnClientMessage(new CloseMessage("g", "g", [])));
        Assert.False(ConnectionRules.DropsOnClientMessage(new HelloMessage("status")));
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
