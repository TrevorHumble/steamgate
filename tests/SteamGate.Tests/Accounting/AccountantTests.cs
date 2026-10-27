using SteamGate.Core;
using SteamGate.Core.Accounting;
using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Accounting;

public sealed class AccountantTests
{
    private static readonly TimeSpan Five = TimeSpan.FromSeconds(5);
    private static readonly DateTime Utc = new(2026, 10, 22, 21, 0, 0, DateTimeKind.Utc);

    private static readonly RunningGame[] OneGame = [Game("Viewfinder", 20)];
    private static readonly RunningGame[] TwoGames = [Game("Viewfinder", 20), Game("Overwatch", 30)];

    [Fact]
    public void TwoGamesCountOnce()
    {
        var tally = Tally.Empty;
        for (var i = 0; i < 12; i++)
        {
            tally = Accountant.Tick(tally, new DateTime(2026, 10, 22, 21, 0, i * 5), Utc, Five, Accountant.MaxTickStep, TwoGames, GateSettings.Defaults);
        }

        Assert.Equal(60, tally.NightSeconds);
        Assert.Equal(60, tally.WeekSeconds);
    }

    [Fact]
    public void GapIsCapped()
    {
        var tally = Accountant.Tick(Tally.Empty, new DateTime(2026, 10, 22, 21, 0, 0), Utc, TimeSpan.FromSeconds(600), Accountant.MaxTickStep, OneGame, GateSettings.Defaults);

        Assert.Equal(15, tally.NightSeconds);
        Assert.Equal(15, tally.WeekSeconds);
    }

    [Fact]
    public void Limits_AreFifteenSecondsPerTickAndSixtyForRestart()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), Accountant.MaxTickStep);
        Assert.Equal(TimeSpan.FromSeconds(60), Accountant.MaxRestartGap);
    }

    [Fact]
    public void NoGameNoCount()
    {
        var tally = Tally.Empty;
        for (var i = 0; i < 10; i++)
        {
            tally = Accountant.Tick(tally, new DateTime(2026, 10, 22, 21, 0, i * 5), Utc, Five, Accountant.MaxTickStep, [], GateSettings.Defaults);
        }

        Assert.Equal(0, tally.NightSeconds);
        Assert.Equal(0, tally.WeekSeconds);
    }

    [Fact]
    public void NightRollsOverWeekDoesNot()
    {
        var start = new Tally(new DateOnly(2026, 10, 22), 3000, new DateOnly(2026, 10, 19), 20000, null);

        var tally = Accountant.Tick(start, new DateTime(2026, 10, 23, 4, 0, 5), Utc, Five, Accountant.MaxTickStep, OneGame, GateSettings.Defaults);

        Assert.Equal(new DateOnly(2026, 10, 23), tally.Night);
        Assert.Equal(5, tally.NightSeconds);
        Assert.Equal(new DateOnly(2026, 10, 19), tally.Week);
        Assert.Equal(20005, tally.WeekSeconds);
    }

    [Fact]
    public void WeekRollsOverMonday4AM()
    {
        var start = new Tally(new DateOnly(2026, 10, 25), 3000, new DateOnly(2026, 10, 19), 30000, null);

        var tally = Accountant.Tick(start, new DateTime(2026, 10, 26, 4, 0, 5), Utc, Five, Accountant.MaxTickStep, OneGame, GateSettings.Defaults);

        Assert.Equal(new DateOnly(2026, 10, 26), tally.Night);
        Assert.Equal(5, tally.NightSeconds);
        Assert.Equal(new DateOnly(2026, 10, 26), tally.Week);
        Assert.Equal(5, tally.WeekSeconds);
    }

    [Fact]
    public void FractionalStepsKept()
    {
        var tally = Tally.Empty;
        for (var i = 0; i < 3; i++)
        {
            tally = Accountant.Tick(tally, new DateTime(2026, 10, 22, 21, 0, 0), Utc, TimeSpan.FromSeconds(4.999), Accountant.MaxTickStep, OneGame, GateSettings.Defaults);
        }

        Assert.Equal(14.997, tally.NightSeconds, 0.001);
    }

    [Fact]
    public void NegativeStepOrCap_CountsNothing()
    {
        var local = new DateTime(2026, 10, 22, 21, 0, 0);

        var negativeStep = Accountant.Tick(Tally.Empty, local, Utc, TimeSpan.FromSeconds(-5), Accountant.MaxTickStep, OneGame, GateSettings.Defaults);
        var negativeCap = Accountant.Tick(Tally.Empty, local, Utc, Five, TimeSpan.FromSeconds(-5), OneGame, GateSettings.Defaults);

        Assert.Equal(0, negativeStep.NightSeconds);
        Assert.Equal(0, negativeCap.NightSeconds);
    }

    [Fact]
    public void StepBelowCap_IsCountedWhole()
    {
        var tally = Accountant.Tick(Tally.Empty, new DateTime(2026, 10, 22, 21, 0, 0), Utc, TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(9), OneGame, GateSettings.Defaults);

        Assert.Equal(7, tally.NightSeconds);
    }

    [Fact]
    public void Tick_AddsToExistingCounts_AndStampsTheTime()
    {
        var start = new Tally(new DateOnly(2026, 10, 22), 100, new DateOnly(2026, 10, 19), 200, null);

        var tally = Accountant.Tick(start, new DateTime(2026, 10, 22, 21, 0, 0), Utc, Five, Accountant.MaxTickStep, OneGame, GateSettings.Defaults);

        Assert.Equal(105, tally.NightSeconds);
        Assert.Equal(205, tally.WeekSeconds);
        Assert.Equal(Utc, tally.LastCountedUtc);
    }

    [Fact]
    public void RestartGapCounted()
    {
        var local = new DateTime(2026, 10, 22, 21, 0, 0);

        Assert.Equal(20, RiseAfterRestart(Utc.AddSeconds(-20)));
        Assert.Equal(60, RiseAfterRestart(Utc.AddSeconds(-600)));
        Assert.Equal(0, RiseAfterRestart(Utc.AddSeconds(5)));

        var counting = Accountant.Tick(Tally.Empty, local, Utc, Five, Accountant.MaxTickStep, OneGame, GateSettings.Defaults);
        var stopped = Accountant.Tick(counting, local, Utc, Five, Accountant.MaxTickStep, [], GateSettings.Defaults);
        Assert.NotNull(counting.LastCountedUtc);
        Assert.Null(stopped.LastCountedUtc);
        Assert.Equal(TimeSpan.Zero, Accountant.RestartStep(stopped, Utc, anyGameOpen: true));

        double RiseAfterRestart(DateTime lastCounted)
        {
            var loaded = new Tally(new DateOnly(2026, 10, 22), 1000, new DateOnly(2026, 10, 19), 2000, lastCounted);
            var step = Accountant.RestartStep(loaded, Utc, anyGameOpen: true);
            var after = Accountant.Tick(loaded, local, Utc, step, step, OneGame, GateSettings.Defaults);
            return after.NightSeconds - 1000;
        }
    }

    [Fact]
    public void RestartStep_NoGameOpenNow_IsZero()
    {
        var loaded = new Tally(new DateOnly(2026, 10, 22), 1000, new DateOnly(2026, 10, 19), 2000, Utc.AddSeconds(-20));

        Assert.Equal(TimeSpan.Zero, Accountant.RestartStep(loaded, Utc, anyGameOpen: false));
    }

    [Fact]
    public void RestartStep_CountsFractionsAndEdges()
    {
        Assert.Equal(TimeSpan.FromSeconds(12.5), Accountant.RestartStep(Loaded(Utc.AddSeconds(-12.5)), Utc, anyGameOpen: true));
        Assert.Equal(TimeSpan.FromSeconds(60), Accountant.RestartStep(Loaded(Utc.AddSeconds(-60)), Utc, anyGameOpen: true));
        Assert.Equal(TimeSpan.FromSeconds(60), Accountant.RestartStep(Loaded(Utc.AddSeconds(-61)), Utc, anyGameOpen: true));
        Assert.Equal(TimeSpan.Zero, Accountant.RestartStep(Loaded(Utc), Utc, anyGameOpen: true));
    }

    private static Tally Loaded(DateTime lastCounted) =>
        new(new DateOnly(2026, 10, 22), 0, new DateOnly(2026, 10, 19), 0, lastCounted);

    private static RunningGame Game(string folder, int pid) =>
        new(folder, folder, [new ProcessSample(pid, Utc, @"C:\Steam\steamapps\common\" + folder + @"\game.exe")]);

    [Fact]
    public void PastTheOldLimitCountsDouble()
    {
        var monday = new DateOnly(2026, 10, 26);
        var local = new DateTime(2026, 10, 26, 21, 0, 0);
        var settings = GateSettings.Defaults;

        Tally Start(double night, double week, int? minutes = null) =>
            new(monday, night, monday, week, null, minutes is { } m ? new NightExtension(monday, m) : null);

        (double, double) After(Tally tally, int step, GateSettings? with = null)
        {
            var next = Accountant.Tick(tally, local, Utc, TimeSpan.FromSeconds(step), TimeSpan.FromSeconds(step), OneGame, with ?? settings);
            return (next.NightSeconds, next.WeekSeconds);
        }

        Assert.Equal((5415.0, 10030.0), After(Start(5400, 10000, 30), 15));
        Assert.Equal((5405.0, 10020.0), After(Start(5390, 10000, 30), 15));
        Assert.Equal((5415.0, 10015.0), After(Start(5400, 10000), 15));
        Assert.Equal((5015.0, 10015.0), After(Start(5000, 10000, 30), 15));
        Assert.Equal((6120.0, 11440.0), After(Start(5400, 10000, 30), 720));

        var idle = Accountant.Tick(Start(5400, 10000, 30), local, Utc, Five, Accountant.MaxTickStep, [], settings);
        Assert.Equal((5400.0, 10000.0, (DateTime?)null), (idle.NightSeconds, idle.WeekSeconds, idle.LastCountedUtc));

        var staleExtension = new Tally(monday, 5400, monday, 10000, null, new NightExtension(new DateOnly(2026, 10, 25), 30));
        Assert.Equal((5415.0, 10015.0), After(staleExtension, 15));

        var rolled = Accountant.Tick(Start(5400, 10000, 30), new DateTime(2026, 10, 27, 4, 0, 5), Utc, TimeSpan.FromSeconds(15), Accountant.MaxTickStep, OneGame, settings);
        Assert.Equal(new DateOnly(2026, 10, 27), rolled.Night);
        Assert.Equal((15.0, 10015.0), (rolled.NightSeconds, rolled.WeekSeconds));
        Assert.Null(rolled.Extension);

        // Trevor's example: take 30, play 15. The week is charged 30, and the other 15 minutes of the extension stay playable at no cost.
        var played = Accountant.Tick(Start(5400, 10000, 30), local, Utc, TimeSpan.FromSeconds(900), TimeSpan.FromSeconds(900), OneGame, settings);
        Assert.Equal((6300.0, 11800.0), (played.NightSeconds, played.WeekSeconds));
        Assert.Equal(900, Budget.RemainingSeconds(played, settings, local));

        Assert.Equal((7200.0, 13600.0), After(played, 900));

        var lowered = settings with { WeeknightMinutes = 60 };
        Assert.Equal((4015.0, 10030.0), After(Start(4000, 10000, 30), 15, lowered));

        Assert.Equal((7215.0, 13630.0), After(Start(7200, 13600, 30), 15));
        Assert.Equal((7210.0, 13630.0), After(Start(7195, 13600, 30), 15));
    }
}
