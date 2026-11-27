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

    static int Count(LevelRules level, ItemKind kind)
    {
        int n = 0;
        while (level.Drop(out LevelRules.Item item)) if (item.Kind == kind) n++;
        return n;
    }

    [Test]
    public void WinsWhenTheTargetIsSorted()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 10, Misses = 10 });
        LevelRules ended = null;
        level.Ended += l => ended = l;
        Correct(rules, 9);
        Assert.AreEqual(LevelState.Playing, level.State);
        Correct(rules, 1);
        Assert.AreEqual(LevelState.Won, level.State);
        Assert.AreSame(level, ended);
        Assert.AreEqual(0, level.ShortBy);
        Assert.AreEqual(10, level.MissesLeft);
    }

    [Test]
    public void DropsEnoughGemsForTheTargetWithTheMissesToSpare()
    {
        var level = new LevelRules(new HeistSortRules(), new LevelSpec { Target = 8, Misses = 2 });
        Assert.AreEqual(10, level.Gems);
        Assert.AreEqual(10, level.Drops);
        Assert.AreEqual(10, Count(level, ItemKind.Gem));
        Assert.IsFalse(level.CanDrop);
    }

    [Test]
    public void FailsTheMomentTheTargetCanNoLongerBeReached()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 75, Misses = 15 });
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
    public void WinningWithMissesUnusedLeavesThemToSpare()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 5, Misses = 4 });
        Wrong(rules, 1);
        Correct(rules, 5);
        Assert.AreEqual(LevelState.Won, level.State);
        Assert.AreEqual(3, level.MissesLeft);
    }

    [Test]
    public void DynamiteMustGoUp()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 10, Misses = 3 });
        rules.Trash(ItemKind.Bomb);
        Assert.AreEqual(3, level.MissesLeft, "flicked up: no cost");
        rules.Miss(ItemKind.Bomb);
        Assert.AreEqual(2, level.MissesLeft, "left to land");
        rules.Catch(ItemKind.Bomb, 0, 1);
        Assert.AreEqual(1, level.MissesLeft, "sorted into a chest");
        Assert.AreEqual(2, level.Lost);
        rules.Miss(ItemKind.Bomb);
        rules.Miss(ItemKind.Bomb);
        Assert.AreEqual(LevelState.Failed, level.State);
    }

    [Test]
    public void GoldCountsTwoAnyWayItIsCaughtAndNothingWhenMissed()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 10, Misses = 2 });
        rules.Catch(ItemKind.Gold, 0, 1);
        Assert.AreEqual(2, level.Sorted);
        Assert.AreEqual(4, level.MissesLeft, "gold gives two misses back");
        rules.Trash(ItemKind.Gold);
        Assert.AreEqual(4, level.Sorted);
        rules.Miss(ItemKind.Gold);
        Assert.AreEqual(4, level.Sorted);
        Assert.AreEqual(6, level.MissesLeft);
        Assert.AreEqual(2, level.GoldCaught);
        Correct(rules, 6);
        Assert.AreEqual(LevelState.Won, level.State);
    }

    [Test]
    public void ALevelHoldsExactlyItsItemsAfterTheOpeningGems()
    {
        var level = new LevelRules(new HeistSortRules(), new LevelSpec { Target = 50, Misses = 10, Dynamite = 12, Cash = 3, Seed = 9 });
        Assert.AreEqual(75, level.Drops);
        int bombs = 0, cash = 0, index = 0;
        while (level.Drop(out LevelRules.Item item))
        {
            if (index++ < LevelRules.OpeningGems) Assert.AreEqual(ItemKind.Gem, item.Kind, "opening drop " + index);
            if (item.Kind == ItemKind.Bomb) bombs++;
            if (item.Kind == ItemKind.Gold) cash++;
            if (item.Kind != ItemKind.Gem) Assert.AreEqual(0, item.ColorId);
        }
        Assert.AreEqual(60, level.Gems);
        Assert.AreEqual(12, bombs);
        Assert.AreEqual(3, cash);
    }

    [Test]
    public void RaisesNoHeat()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 30, Misses = 999 });
        Wrong(rules, 10);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(0f, rules.Heat);
        Assert.AreEqual(LevelState.Playing, level.State);
    }

    [Test]
    public void EverythingComesInTheSameOrderOnEveryAttempt()
    {
        var spec = new LevelSpec { Target = 30, Misses = 10, Seed = 7, Dynamite = 6, Cash = 3 };
        var rules = new HeistSortRules();
        var first = new LevelRules(rules, spec);
        var items = new System.Collections.Generic.List<LevelRules.Item>();
        while (first.Drop(out LevelRules.Item item)) items.Add(item);
        first.Dispose();
        rules.StartAttempt();
        var again = new LevelRules(rules, spec);
        Assert.AreEqual(items.Count, again.Drops);
        for (int i = 0; i < items.Count; i++)
        {
            again.Drop(out LevelRules.Item item);
            Assert.AreEqual(items[i].Kind, item.Kind, "kind of drop " + i);
            Assert.AreEqual(items[i].ColorId, item.ColorId, "color of drop " + i);
        }
    }

    [Test]
    public void TheOrderUsesBothColorsAndChangesWithTheSeed()
    {
        var a = new LevelRules(new HeistSortRules(), new LevelSpec { Target = 40, Misses = 0, Seed = 1 });
        var b = new LevelRules(new HeistSortRules(), new LevelSpec { Target = 40, Misses = 0, Seed = 2 });
        int reds = 0, differ = 0;
        for (int i = 0; i < 40; i++)
        {
            a.Drop(out LevelRules.Item itemA);
            b.Drop(out LevelRules.Item itemB);
            if (itemA.ColorId == 0) reds++;
            if (itemA.ColorId != itemB.ColorId) differ++;
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
        var easy = new LevelRules(rules, new LevelSpec { Target = 5, Misses = 3 });
        Assert.AreEqual(5, easy.Target);
        Assert.AreEqual(8, easy.Gems);
        easy.Dispose();
        var hard = new LevelRules(rules, new LevelSpec { Target = 9, Misses = 1 });
        Assert.AreEqual(1, hard.MissesLeft);
        Assert.AreEqual(10, hard.Drops);
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

    [Test]
    public void APlaceWithNoLevelsSaysSo()
    {
        var place = UnityEngine.ScriptableObject.CreateInstance<PlaceData>();
        place.PlaceName = "Beach";
        place.Levels = new LevelSpec[0];
        var error = Assert.Throws<System.InvalidOperationException>(() => place.Level(0));
        StringAssert.Contains("Beach has no levels", error.Message);
        UnityEngine.Object.DestroyImmediate(place);
    }
}

public class BossLevelTests
{
    [Test]
    public void ABossHoldsItsWinForTheFinale()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 3, Misses = 2 }) { HoldsWin = true };
        int met = 0;
        level.TargetMet += _ => met++;
        for (int i = 0; i < 3; i++) rules.Catch(ItemKind.Gem, i % 2, i % 2);
        Assert.AreEqual(1, met);
        Assert.IsTrue(level.TargetReached);
        Assert.AreEqual(LevelState.Playing, level.State);
        rules.Miss(ItemKind.Gem);
        rules.Miss(ItemKind.Gem);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(LevelState.Playing, level.State, "after the target, misses no longer fail it");
        level.Finish(true);
        Assert.AreEqual(LevelState.Won, level.State);
        Assert.IsFalse(level.FailedAtFinale);
    }

    [Test]
    public void ATrippedAlarmFailsTheBossAtTheFinale()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 1, Misses = 2 }) { HoldsWin = true };
        level.Finish(false);
        Assert.AreEqual(LevelState.Playing, level.State, "no finale before the target");
        rules.Catch(ItemKind.Gem, 0, 0);
        level.Finish(false);
        Assert.AreEqual(LevelState.Failed, level.State);
        Assert.IsTrue(level.FailedAtFinale);
    }
}
