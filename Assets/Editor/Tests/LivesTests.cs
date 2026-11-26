using NUnit.Framework;

public class LivesTests
{
    const long Hour = 3600;

    [Test]
    public void LosingTakesOneAndItComesBackOnTheRefill()
    {
        var lives = new Lives(Lives.Max, 0);
        Assert.IsTrue(lives.Lose(1000));
        Assert.AreEqual(Lives.Max - 1, lives.Count(1000));
        Assert.AreEqual(Lives.RefillSeconds, lives.NextIn(1000));
        Assert.AreEqual(Lives.Max - 1, lives.Count(1000 + Lives.RefillSeconds - 1));
        Assert.AreEqual(Lives.Max, lives.Count(1000 + Lives.RefillSeconds));
        Assert.AreEqual(0, lives.NextIn(1000 + Lives.RefillSeconds));
    }

    [Test]
    public void LosingAgainKeepsTheTimeAlreadyWaited()
    {
        var lives = new Lives(Lives.Max, 0);
        lives.Lose(0);
        lives.Lose(600);
        Assert.AreEqual(Lives.Max - 2, lives.Count(600));
        Assert.AreEqual(Lives.RefillSeconds - 600, lives.NextIn(600), "the first refill is still on its way");
        Assert.AreEqual(Lives.Max - 1, lives.Count(Lives.RefillSeconds));
    }

    [Test]
    public void NoLivesMeansNoPlayUntilOneComesBack()
    {
        var lives = new Lives(0, 0);
        Assert.IsFalse(lives.Lose(10));
        Assert.AreEqual(0, lives.Count(10));
        Assert.AreEqual(1, lives.Count(Lives.RefillSeconds));
        Assert.AreEqual(Lives.Max, lives.Count(10 * Hour));
    }

    [Test]
    public void AKeptGoingAttemptGetsItsLifeBack()
    {
        var lives = new Lives(Lives.Max, 0);
        lives.Lose(100);
        lives.Give(150);
        Assert.AreEqual(Lives.Max, lives.Count(150));
        lives.Give(160);
        Assert.AreEqual(Lives.Max, lives.Count(160), "never past full");
    }

    [Test]
    public void AClockMovedBackRestartsTheWaitInsteadOfStoppingIt()
    {
        var lives = new Lives(Lives.Max, 0);
        lives.Lose(10000);
        lives.Lose(5000);
        Assert.AreEqual(Lives.Max - 2, lives.Count(5000));
        Assert.AreEqual(Lives.Max - 1, lives.Count(5000 + Lives.RefillSeconds));
    }

    [Test]
    public void FillAndRestoreRoundTrip()
    {
        var lives = new Lives(Lives.Max, 0);
        lives.Lose(100);
        lives.Lose(200);
        var restored = new Lives(lives.StoredFor(500), lives.SinceFor(500));
        Assert.AreEqual(lives.Count(500), restored.Count(500));
        Assert.AreEqual(lives.NextIn(500), restored.NextIn(500));
        restored.Fill();
        Assert.AreEqual(Lives.Max, restored.Count(500));
    }
}

public class KeepGoingTests
{
    [Test]
    public void AFailedLevelKeepsGoingWithMoreGems()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 10, Misses = 1 });
        rules.Catch(ItemKind.Gem, 0, 0);
        rules.Miss(ItemKind.Gem);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(LevelState.Failed, level.State);
        int dropsBefore = level.Drops;
        Assert.IsTrue(level.Continue(5));
        Assert.AreEqual(LevelState.Playing, level.State);
        Assert.AreEqual(dropsBefore + 5, level.Drops);
        Assert.AreEqual(4, level.MissesLeft, "one over, plus five more gems");
        Assert.AreEqual(1, level.Continues);
        Assert.IsFalse(level.Continue(5), "only a failed level continues");
    }

    [Test]
    public void GemsClearedAtTheFailFallAgainSoAKeptGoingAttemptAlwaysEnds()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 3, Misses = 0 });
        level.Drop(out LevelRules.Item a);
        level.Drop(out LevelRules.Item b);
        level.Drop(out LevelRules.Item inTheAir);
        rules.Catch(ItemKind.Gem, a.ColorId, a.ColorId);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(LevelState.Failed, level.State);
        level.Return(inTheAir.Kind, inTheAir.ColorId);
        level.Continue(2);
        int dropped = 0;
        while (level.Drop(out _)) dropped++;
        Assert.AreEqual(3, dropped, "the gem in the air plus the two extra");
        Assert.AreEqual(1, level.MissesLeft, "one over the limit, plus two more gems");
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(LevelState.Playing, level.State);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(LevelState.Failed, level.State, "settled before anything is left hanging");
    }

    [Test]
    public void ATrippedAlarmCannotBeKeptGoing()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 1, Misses = 1 }) { HoldsWin = true };
        rules.Catch(ItemKind.Gem, 0, 0);
        level.Finish(false);
        Assert.IsFalse(level.Continue(5));
    }

    [Test]
    public void SpendingCashNeedsEnough()
    {
        var rules = new HeistSortRules();
        double start = rules.Balance;
        Assert.IsFalse(rules.Spend(start + 1));
        Assert.AreEqual(start, rules.Balance);
        Assert.IsTrue(rules.Spend(start));
        Assert.AreEqual(0d, rules.Balance);
    }
}
