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

    [Test]
    public void WinsWhenTheTargetIsSorted()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 10, Gems = 20 });
        LevelRules ended = null;
        level.Ended += l => ended = l;
        for (int i = 0; i < 10; i++) level.Drop(out _, out _);
        Correct(rules, 9);
        Assert.AreEqual(LevelState.Playing, level.State);
        Correct(rules, 1);
        Assert.AreEqual(LevelState.Won, level.State);
        Assert.AreSame(level, ended);
        Assert.AreEqual(0, level.ShortBy);
        Assert.AreEqual(10, level.MissesLeft);
    }

    [Test]
    public void StopsDroppingAtTheLimit()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 8, Gems = 10 });
        int drops = 0;
        while (level.Drop(out _, out _)) drops++;
        Assert.AreEqual(10, drops);
        Assert.AreEqual(10, level.Dropped);
        Assert.IsFalse(level.CanDrop);
    }

    [Test]
    public void FailsTheMomentTheTargetCanNoLongerBeReached()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 75, Gems = 90 });
        Correct(rules, 20);
        Wrong(rules, 10);
        rules.Trash(ItemKind.Gem);
        rules.Miss(ItemKind.Gem);
        Wrong(rules, 3);
        Assert.AreEqual(LevelState.Playing, level.State, "15 lost of 15 to spare still wins with no more");
        Assert.AreEqual(0, level.MissesLeft);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(LevelState.Failed, level.State);
        Assert.AreEqual(55, level.ShortBy);
    }

    [Test]
    public void WinningWithEveryMissUnusedLeavesThemAllToSpare()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 5, Gems = 9 });
        Wrong(rules, 1);
        Correct(rules, 5);
        Assert.AreEqual(LevelState.Won, level.State);
        Assert.AreEqual(3, level.MissesLeft);
    }

    [Test]
    public void RaisesNoHeat()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 30, Gems = 999 });
        Wrong(rules, 10);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(0f, rules.Heat);
        Assert.AreEqual(LevelState.Playing, level.State);
    }

    [Test]
    public void GemsComeInTheSameOrderOnEveryAttempt()
    {
        var rules = new HeistSortRules();
        var first = new LevelRules(rules, new LevelSpec { Gems = 40, Seed = 7 });
        var colors = new int[40];
        var rolls = new float[40];
        for (int i = 0; i < 40; i++) first.Drop(out colors[i], out rolls[i]);
        first.Dispose();
        rules.StartAttempt();
        var again = new LevelRules(rules, new LevelSpec { Gems = 40, Seed = 7 });
        for (int i = 0; i < 40; i++)
        {
            again.Drop(out int color, out float roll);
            Assert.AreEqual(colors[i], color, "color of gem " + i);
            Assert.AreEqual(rolls[i], roll, "chute of gem " + i);
        }
    }

    [Test]
    public void TheOrderUsesBothColorsAndChangesWithTheSeed()
    {
        var a = new LevelRules(new HeistSortRules(), new LevelSpec { Gems = 40, Seed = 1 });
        var b = new LevelRules(new HeistSortRules(), new LevelSpec { Gems = 40, Seed = 2 });
        int reds = 0, differ = 0;
        for (int i = 0; i < 40; i++)
        {
            a.Drop(out int colorA, out float rollA);
            b.Drop(out int colorB, out _);
            Assert.That(colorA, Is.InRange(0, HeistSortRules.Sides - 1));
            Assert.That(rollA, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            if (colorA == 0) reds++;
            if (colorA != colorB) differ++;
        }
        Assert.That(reds, Is.InRange(1, 39));
        Assert.Greater(differ, 0);
    }

    [Test]
    public void AFinishedLevelIgnoresTheNextAttempt()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 2 });
        Correct(rules, 2);
        level.Dispose();
        rules.StartAttempt();
        var next = new LevelRules(rules, new LevelSpec { Target = 2 });
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

public class LevelSpecTests
{
    [Test]
    public void LevelsPlayTheirOwnNumbers()
    {
        var rules = new HeistSortRules();
        var easy = new LevelRules(rules, new LevelSpec { Target = 5, Gems = 8 });
        Assert.AreEqual(5, easy.Target);
        Assert.AreEqual(8, easy.GemLimit);
        easy.Dispose();
        var hard = new LevelRules(rules, new LevelSpec { Target = 9, Gems = 10 });
        int drops = 0;
        while (hard.Drop(out _, out _)) drops++;
        Assert.AreEqual(10, drops);
        Assert.AreEqual(1, hard.Spec.Slack);
    }

    [Test]
    public void APlaceClampsItsLevelIndex()
    {
        var place = UnityEngine.ScriptableObject.CreateInstance<PlaceData>();
        place.Levels = new[] { new LevelSpec { Name = "A" }, new LevelSpec { Name = "B" } };
        Assert.AreEqual("A", place.Level(-1).Name);
        Assert.AreEqual("B", place.Level(1).Name);
        Assert.AreEqual("B", place.Level(7).Name);
        UnityEngine.Object.DestroyImmediate(place);
    }
}
