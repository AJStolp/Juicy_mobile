using NUnit.Framework;

public class CaseFileTests
{
    [Test]
    public void EachLevelHoldsItsOwnFixedSymbol()
    {
        var a = new CaseFile(6, 7);
        var b = new CaseFile(6, 7);
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 6; i++)
        {
            Assert.AreEqual(a.ClueAt(i).Symbol, b.ClueAt(i).Symbol, "same seed, same symbol");
            Assert.That(a.ClueAt(i).Symbol, Is.InRange(0, ClueRules.SymbolPool - 1));
            Assert.IsTrue(seen.Add(a.ClueAt(i).Symbol), "no repeats in a place");
        }
    }

    [Test]
    public void AWinFindsTheClueAndAReplayOnlyMakesItClearer()
    {
        var file = new CaseFile(6, 7);
        Assert.IsFalse(file.Found(2));
        Assert.IsTrue(file.Record(2, 1));
        Assert.IsTrue(file.Found(2));
        Assert.AreEqual(1, file.ClueAt(2).Tier);
        Assert.IsFalse(file.Record(2, 0), "a weaker replay changes nothing");
        Assert.AreEqual(1, file.ClueAt(2).Tier);
        Assert.IsTrue(file.Record(2, 9), "clamped to the clearest");
        Assert.AreEqual(ClueRules.MaxTier, file.ClueAt(2).Tier);
        Assert.IsFalse(file.Record(9, 3), "out of range");
    }

    [Test]
    public void RestoreReadsMissingAndBadEntriesAsUnfound()
    {
        var file = new CaseFile(4, 7);
        file.Restore(new[] { 2, -1, 7 });
        Assert.AreEqual(2, file.ClueAt(0).Tier);
        Assert.IsFalse(file.Found(1));
        Assert.AreEqual(ClueRules.MaxTier, file.ClueAt(2).Tier);
        Assert.IsFalse(file.Found(3));
        file.Restore(null);
        Assert.IsFalse(file.Found(0));
    }

    [Test]
    public void TheBestStreakTierOfAnAttemptIsKept()
    {
        var rules = new HeistSortRules();
        var level = new LevelRules(rules, new LevelSpec { Target = 40, Misses = 10 });
        for (int i = 0; i < HeistSortRules.TierStreak2; i++) rules.Catch(ItemKind.Gem, i % 2, i % 2);
        rules.Catch(ItemKind.Gem, 0, 1);
        Assert.AreEqual(0, rules.Tier);
        Assert.AreEqual(2, level.BestTier);
    }
}
