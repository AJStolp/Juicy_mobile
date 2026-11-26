using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class ClueRulesTests
{
    static VaultProgress VaultAtHeist(int heist)
    {
        var vault = new VaultProgress();
        vault.Restore(heist, 0, 0);
        return vault;
    }

    static ClueRules FullCycle(int heist, int seed = 42)
    {
        var clues = new ClueRules(VaultAtHeist(heist), seed);
        while (!clues.HackDue) clues.ClueFound(1);
        return clues;
    }

    static int Wrong(int symbol) => (symbol + 1) % ClueRules.SymbolPool;

    [Test]
    public void CycleNeedsThreeCluesInHeistOneAndFourFromHeistTwo()
    {
        Assert.AreEqual(3, ClueRules.CluesFor(1));
        Assert.AreEqual(4, ClueRules.CluesFor(2));
        Assert.AreEqual(4, ClueRules.CluesFor(5));

        var first = new ClueRules(VaultAtHeist(1), 1);
        first.ClueFound(0);
        first.ClueFound(0);
        Assert.IsFalse(first.HackDue);
        first.ClueFound(0);
        Assert.IsTrue(first.HackDue);
        Assert.AreEqual(3, first.Count);

        var later = new ClueRules(VaultAtHeist(2), 1);
        for (int i = 0; i < 3; i++) later.ClueFound(0);
        Assert.IsFalse(later.HackDue);
        later.ClueFound(0);
        Assert.IsTrue(later.HackDue);
        Assert.AreEqual(4, later.Count);
    }

    [Test]
    public void FullCycleTakesNoMoreClues()
    {
        var clues = FullCycle(1);
        Assert.IsNull(clues.ClueFound(3));
        Assert.AreEqual(3, clues.Count);
    }

    [Test]
    public void SymbolsNeverRepeatWithinACycle()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var clues = new ClueRules(VaultAtHeist(2), seed);
            for (int cycle = 0; cycle < 4; cycle++)
            {
                while (!clues.HackDue) clues.ClueFound(0);
                int[] symbols = clues.SymbolsSnapshot();
                Assert.AreEqual(symbols.Length, symbols.Distinct().Count(), "seed " + seed + " cycle " + cycle);
                Assert.IsTrue(symbols.All(s => s >= 0 && s < ClueRules.SymbolPool));
                clues.Hack(symbols);
            }
        }
    }

    [Test]
    public void SameSeedGivesTheSameSymbols()
    {
        var a = new ClueRules(VaultAtHeist(2), 1234);
        var b = new ClueRules(VaultAtHeist(2), 1234);
        var seenA = new List<int>();
        var seenB = new List<int>();
        for (int cycle = 0; cycle < 3; cycle++)
        {
            while (!a.HackDue) seenA.Add(a.ClueFound(0).Value.Symbol);
            while (!b.HackDue) seenB.Add(b.ClueFound(2).Value.Symbol);
            a.Hack(new int[0]);
            b.Hack(new int[0]);
        }
        CollectionAssert.AreEqual(seenA, seenB);

        var other = new List<int>();
        for (int seed = 1; seed < 20; seed++)
        {
            var c = new ClueRules(VaultAtHeist(2), seed);
            other.Clear();
            while (!c.HackDue) other.Add(c.ClueFound(0).Value.Symbol);
            if (!other.SequenceEqual(seenA.Take(4))) return;
        }
        Assert.Fail("Every seed drew the same symbols");
    }

    [Test]
    public void ClarityGrowsWithTheLegsBestTier()
    {
        Assert.AreEqual(0.5f, ClueRules.CoveredFor(0));
        Assert.AreEqual(0f, ClueRules.CoveredFor(3));
        for (int tier = 1; tier <= 3; tier++)
        {
            Assert.Greater(ClueRules.SecondsShownFor(tier), ClueRules.SecondsShownFor(tier - 1));
            Assert.Less(ClueRules.CoveredFor(tier), ClueRules.CoveredFor(tier - 1));
        }
        Assert.AreEqual(ClueRules.SecondsShownFor(0), ClueRules.SecondsShownFor(-2));
        Assert.AreEqual(ClueRules.SecondsShownFor(3), ClueRules.SecondsShownFor(9));

        var clues = new ClueRules(VaultAtHeist(1), 5);
        Clue weak = clues.ClueFound(0).Value;
        Clue strong = clues.ClueFound(3).Value;
        Assert.AreEqual(0, weak.Tier);
        Assert.AreEqual(0.5f, weak.Covered);
        Assert.AreEqual(ClueRules.SecondsShownFor(0), weak.SecondsShown);
        Assert.AreEqual(3, strong.Tier);
        Assert.AreEqual(0f, strong.Covered);
        Assert.AreEqual(ClueRules.SecondsShownFor(3), strong.SecondsShown);
    }

    [Test]
    public void PerfectHackPaysTheWholeLadderAndFourVaultTurns()
    {
        var clues = FullCycle(1);
        HackReward reward = clues.Hack(clues.SymbolsSnapshot());
        Assert.AreEqual(3, reward.Steps);
        Assert.IsTrue(reward.Perfect);
        Assert.AreEqual(500d, reward.Cash);
        Assert.AreEqual(1, reward.Bribes);
        Assert.AreEqual(1, reward.Drills);
        Assert.AreEqual(4, reward.Turns);
    }

    [Test]
    public void WrongFirstTapEndsTheHackWithNothing()
    {
        var clues = FullCycle(1);
        int[] taps = clues.SymbolsSnapshot();
        taps[0] = Wrong(taps[0]);
        HackReward reward = clues.Hack(taps);
        Assert.AreEqual(0, reward.Steps);
        Assert.IsFalse(reward.Perfect);
        Assert.AreEqual(0d, reward.Cash);
        Assert.AreEqual(0, reward.Bribes);
        Assert.AreEqual(0, reward.Drills);
        Assert.AreEqual(0, reward.Turns);
    }

    [Test]
    public void WrongSecondTapKeepsTheFirstStepOnly()
    {
        var clues = FullCycle(2);
        int[] symbols = clues.SymbolsSnapshot();
        // Taps after the wrong one are right, and still do not count.
        int[] taps = { symbols[0], Wrong(symbols[1]), symbols[2], symbols[3] };
        HackReward reward = clues.Hack(taps);
        Assert.AreEqual(1, reward.Steps);
        Assert.IsFalse(reward.Perfect);
        Assert.AreEqual(500d, reward.Cash);
        Assert.AreEqual(0, reward.Bribes);
        Assert.AreEqual(0, reward.Drills);
        Assert.AreEqual(0, reward.Turns);
    }

    [Test]
    public void LadderOfThreePaysCashThenBribeThenDrill()
    {
        AssertRung(ClueRules.RewardFor(3, 1), 500d, 0, 0, 0);
        AssertRung(ClueRules.RewardFor(3, 2), 500d, 1, 0, 0);
        AssertRung(ClueRules.RewardFor(3, 3), 500d, 1, 1, 4);
    }

    [Test]
    public void LadderOfFourPaysTwoCashRungsThenBribeThenDrill()
    {
        AssertRung(ClueRules.RewardFor(4, 1), 500d, 0, 0, 0);
        AssertRung(ClueRules.RewardFor(4, 2), 1500d, 0, 0, 0);
        AssertRung(ClueRules.RewardFor(4, 3), 1500d, 1, 0, 0);
        AssertRung(ClueRules.RewardFor(4, 4), 1500d, 1, 1, 4);
    }

    static void AssertRung(HackReward reward, double cash, int bribes, int drills, int turns)
    {
        Assert.AreEqual(cash, reward.Cash);
        Assert.AreEqual(bribes, reward.Bribes);
        Assert.AreEqual(drills, reward.Drills);
        Assert.AreEqual(turns, reward.Turns);
    }

    [Test]
    public void ApplyingARewardPaysTheRulesAndTurnsTheVault()
    {
        var vault = new VaultProgress { TurnsByHeist = new[] { 9 } };
        var rules = new HeistSortRules();
        double before = rules.Balance;
        ClueRules.RewardFor(4, 4).Apply(rules, vault);
        Assert.AreEqual(before + 1500d, rules.Balance);
        Assert.AreEqual(1, rules.GearCount(GearKind.Bribe));
        Assert.AreEqual(1, rules.GearCount(GearKind.Drill));
        Assert.AreEqual(4, vault.Turns);
    }

    [Test]
    public void FinishingAHackClearsTheCycleAndDrawsAFreshOrder()
    {
        var clues = FullCycle(2, 9);
        int[] firstCycle = clues.SymbolsSnapshot();
        int[] taps = (int[])firstCycle.Clone();
        taps[0] = Wrong(taps[0]);
        clues.Hack(taps);
        Assert.AreEqual(0, clues.Count);
        Assert.IsFalse(clues.HackDue);
        Assert.AreEqual(1, clues.Cycle);
        Assert.IsTrue(clues.ClueFound(0).HasValue);
        Assert.AreEqual(1, clues.Count);
    }

    [Test]
    public void LegAdvancedCanReadTheFinishedLegsBestTier()
    {
        var rules = new HeistSortRules();
        int seen = -1;
        rules.LegAdvanced += (_, __) => seen = rules.LastLegBestTier;
        // Twelve correct sorts finish the first leg; ten of them reach tier 2.
        int perLeg = rules.VatFillsPerLeg * rules.CorrectsToFillVat;
        for (int i = 0; i < perLeg; i++) rules.Catch(ItemKind.Gem, 0, 0);
        Assert.AreEqual(2, seen);
    }

    [Test]
    public void SaveRoundTripKeepsAnUnfinishedCycle()
    {
        var vault = VaultAtHeist(2);
        var rules = new HeistSortRules();
        var clues = new ClueRules(vault, 321);
        clues.Hack(new int[0]);
        Clue a = clues.ClueFound(3).Value;
        Clue b = clues.ClueFound(1).Value;

        string json = HeistSave.Capture(vault, rules, clues, new PlaceProgress(6), new CaseFile(6, 7)).ToJson();

        var vault2 = new VaultProgress();
        var restored = new ClueRules(vault2, 999);
        HeistSave.FromJson(json).Apply(vault2, new HeistSortRules(), restored, new PlaceProgress(6), new CaseFile(6, 7));
        Assert.AreEqual(2, restored.Count);
        Assert.AreEqual(a.Symbol, restored.Clues[0].Symbol);
        Assert.AreEqual(3, restored.Clues[0].Tier);
        Assert.AreEqual(b.Symbol, restored.Clues[1].Symbol);
        Assert.AreEqual(1, restored.Clues[1].Tier);
        Assert.AreEqual(321, restored.Seed);
        Assert.AreEqual(1, restored.Cycle);
        Assert.IsFalse(restored.HackDue);

        // The rest of the cycle draws the same symbols the original would have.
        CollectionAssert.AreEqual(
            new[] { clues.ClueFound(0).Value.Symbol, clues.ClueFound(0).Value.Symbol },
            new[] { restored.ClueFound(0).Value.Symbol, restored.ClueFound(0).Value.Symbol });
    }

    [Test]
    public void OldSaveWithoutClueFieldsLoadsAnEmptyCycle()
    {
        const string old = "{\"heist\":1,\"stage\":5,\"turns\":4,\"balance\":54321.0,\"gear\":[2,0,1,1]}";
        var vault = new VaultProgress();
        var rules = new HeistSortRules();
        var clues = new ClueRules(vault, 5);
        clues.ClueFound(2);
        HeistSave.FromJson(old).Apply(vault, rules, clues, new PlaceProgress(6), new CaseFile(6, 7));
        Assert.AreEqual(0, clues.Count);
        Assert.AreEqual(0, clues.Cycle);
        Assert.IsFalse(clues.HackDue);
        Assert.AreEqual(5, vault.Stage);
        Assert.AreEqual(54321d, rules.Balance);
    }

    [Test]
    public void MalformedClueFieldsLoadAnEmptyCycle()
    {
        var clues = new ClueRules(new VaultProgress(), 5);
        clues.Restore(1, 0, new[] { 1, 2 }, new[] { 0 });
        Assert.AreEqual(0, clues.Count);
        clues.Restore(1, 0, new[] { 1, 99 }, new[] { 0, 0 });
        Assert.AreEqual(0, clues.Count);
        clues.Restore(1, 0, null, null);
        Assert.AreEqual(0, clues.Count);
    }
}
