using NUnit.Framework;

public class LevelRulesTests
{
    static void Correct(HeistSortRules rules, int times)
    {
        for (int i = 0; i < times; i++) rules.Catch(ItemKind.Gem, i % 2, i % 2);
    }

    static void Wrong(HeistSortRules rules, int times)
    {
        for (int i = 0; i < times; i++) rules.Catch(ItemKind.Gem, 0, 1);
    }

    [TestCase(LevelGoal.Heat)]
    [TestCase(LevelGoal.Clock)]
    [TestCase(LevelGoal.Gems)]
    public void WinsWhenTheTargetIsSorted(LevelGoal goal)
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, goal, target: 10, seconds: 60f, gems: 20);
        LevelRules ended = null;
        level.Ended += l => ended = l;
        for (int i = 0; i < 10; i++) level.Drop();
        Correct(rules, 9);
        Assert.AreEqual(LevelState.Playing, level.State);
        Correct(rules, 1);
        Assert.AreEqual(LevelState.Won, level.State);
        Assert.AreSame(level, ended);
        Assert.AreEqual(0, level.ShortBy);
    }

    [Test]
    public void HeatGoalFailsOnABustWithTheGemsShort()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, LevelGoal.Heat, target: 30);
        Correct(rules, 26);
        Wrong(rules, 5);
        Assert.AreEqual(LevelState.Failed, level.State);
        Assert.AreEqual(4, level.ShortBy);
        Assert.AreEqual(0f, level.Margin, 0.001f);
    }

    [Test]
    public void HeatGoalMarginIsTheRoomLeftBelowTheBustAtItsHighest()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, LevelGoal.Heat, target: 3);
        Wrong(rules, 1);
        rules.Tick(100f);
        Correct(rules, 3);
        Assert.AreEqual(LevelState.Won, level.State);
        Assert.AreEqual(HeistSortRules.MaxHeat - HeistSortRules.MisSortHeat, level.Margin, 0.001f);
    }

    [Test]
    public void ClockGoalFailsWhenTimeRunsOutAndAMistakeCostsSeconds()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, LevelGoal.Clock, target: 30, seconds: 20f);
        Correct(rules, 5);
        Wrong(rules, 1);
        Assert.AreEqual(20f - LevelRules.MistakeSeconds, level.TimeLeft, 0.001f);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(20f - 2f * LevelRules.MistakeSeconds, level.TimeLeft, 0.001f);
        level.Tick(13.9f);
        Assert.AreEqual(LevelState.Playing, level.State);
        level.Tick(0.2f);
        Assert.AreEqual(LevelState.Failed, level.State);
        Assert.AreEqual(25, level.ShortBy);
    }

    [Test]
    public void ClockGoalRaisesNoHeat()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, LevelGoal.Clock, target: 30, seconds: 999f);
        Wrong(rules, 10);
        Assert.AreEqual(0f, rules.Heat);
        Assert.AreEqual(LevelState.Playing, level.State);
    }

    [Test]
    public void GemsGoalStopsDroppingAtTheLimitAndFailsOnceEveryGemIsSpent()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, LevelGoal.Gems, target: 8, gems: 10);
        for (int i = 0; i < 12; i++) level.Drop();
        Assert.AreEqual(10, level.Dropped);
        Assert.IsFalse(level.CanDrop);
        Correct(rules, 6);
        Wrong(rules, 1);
        rules.Trash(ItemKind.Gem);
        Assert.AreEqual(LevelState.Playing, level.State);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(LevelState.Playing, level.State);
        Correct(rules, 1);
        Assert.AreEqual(LevelState.Failed, level.State);
        Assert.AreEqual(1, level.ShortBy);
        Assert.AreEqual(0f, rules.Heat);
    }

    [Test]
    public void GemsGoalMarginIsTheGemsThatNeverFell()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, LevelGoal.Gems, target: 5, gems: 12);
        for (int i = 0; i < 7; i++) level.Drop();
        Correct(rules, 5);
        Assert.AreEqual(LevelState.Won, level.State);
        Assert.AreEqual(5f, level.Margin, 0.001f);
    }

    [Test]
    public void AFinishedLevelIgnoresTheNextAttempt()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, LevelGoal.Clock, target: 2, seconds: 60f);
        Correct(rules, 2);
        level.Dispose();
        rules.StartAttempt();
        var next = new LevelRules(rules, LevelGoal.Clock, target: 2, seconds: 60f);
        Correct(rules, 1);
        Assert.AreEqual(2, level.Sorted);
        Assert.AreEqual(1, next.Sorted);
    }

    [Test]
    public void StartAttemptKeepsCashAndGearAndResetsTheRest()
    {
        var rules = new HeistSortRules();
        rules.AddGear(GearKind.Dynamite, 2);
        Correct(rules, 20);
        Wrong(rules, 2);
        double balance = rules.Balance;
        Assert.Greater(rules.Leg, 1);
        rules.StartAttempt();
        Assert.AreEqual(balance, rules.Balance);
        Assert.AreEqual(2, rules.GearCount(GearKind.Dynamite));
        Assert.AreEqual(0f, rules.Heat);
        Assert.AreEqual(0, rules.Streak);
        Assert.AreEqual(1, rules.Leg);
        Assert.AreEqual(0, rules.VatFills);
        Assert.AreEqual(0f, rules.Vat(0));
        Assert.AreEqual(0, rules.Craft(1));
    }
}
