using NUnit.Framework;
using UnityEngine;

public class HeistSortRulesTests
{
    static void SortCorrect(HeistSortRules rules, int side, int times)
    {
        for (int i = 0; i < times; i++) rules.Catch(ItemKind.Gem, side, side);
    }

    [Test]
    public void StartsWithOneChuteAndOpensOnePerLegUpToFour()
    {
        var rules = new HeistSortRules();
        Assert.AreEqual(1, rules.Chutes);
        int perLeg = rules.VatFillsPerLeg * rules.CorrectsToFillVat;
        for (int leg = 2; leg <= 6; leg++)
        {
            SortCorrect(rules, 0, perLeg);
            Assert.AreEqual(leg, rules.Leg);
            Assert.AreEqual(Mathf.Min(leg, 4), rules.Chutes);
        }
    }

    [Test]
    public void LegAdvancesEveryFourVatFills()
    {
        var rules = new HeistSortRules();
        int fills = 0, legs = 0;
        rules.VatFilled += _ => fills++;
        rules.LegAdvanced += (_, __) => legs++;
        SortCorrect(rules, 1, rules.CorrectsToFillVat * 3);
        Assert.AreEqual(3, fills);
        Assert.AreEqual(0, legs);
        SortCorrect(rules, 0, rules.CorrectsToFillVat);
        Assert.AreEqual(4, fills);
        Assert.AreEqual(1, legs);
        Assert.AreEqual(2, rules.Leg);
    }

    [Test]
    public void CorrectSortPaysFillsVatAndBuildsStreak()
    {
        var rules = new HeistSortRules();
        Assert.AreEqual(SortResult.Correct, rules.Catch(ItemKind.Gem, 1, 1));
        Assert.AreEqual(HeistSortRules.StartingBalance + rules.CashPerSort, rules.Balance, 0.001);
        Assert.AreEqual(1, rules.Craft(1));
        Assert.Greater(rules.Vat(1), 0f);
        Assert.AreEqual(1, rules.Streak);
    }

    [Test]
    public void StreakTiersMultiplyCash()
    {
        var rules = new HeistSortRules();
        rules.CorrectsToFillVat = 1000;
        SortCorrect(rules, 0, HeistSortRules.TierStreak1);
        Assert.AreEqual(1, rules.Tier);
        double before = rules.Balance;
        rules.Catch(ItemKind.Gem, 0, 0);
        Assert.AreEqual(before + rules.CashPerSort * 2, rules.Balance, 0.001);
    }

    [Test]
    public void WrongSortBreaksStreakDrainsVatAndRaisesHeat()
    {
        var rules = new HeistSortRules();
        SortCorrect(rules, 0, 2);
        float vat = rules.Vat(0);
        Assert.AreEqual(SortResult.Wrong, rules.Catch(ItemKind.Gem, 1, 0));
        Assert.AreEqual(0, rules.Streak);
        Assert.Less(rules.Vat(0), vat);
        Assert.AreEqual(HeistSortRules.MisSortHeat, rules.Heat, 0.001f);
    }

    [Test]
    public void BombInBinCostsLootAndHeat()
    {
        var rules = new HeistSortRules();
        SortCorrect(rules, 0, 2);
        SortCorrect(rules, 1, 1);
        Assert.AreEqual(SortResult.Bomb, rules.Catch(ItemKind.Bomb, 0, 0));
        Assert.AreEqual(1, rules.Craft(0));
        Assert.AreEqual(0, rules.Craft(1));
        Assert.AreEqual(HeistSortRules.HazardHeat, rules.Heat, 0.001f);
    }

    [Test]
    public void BombTrashedOrMissedIsHarmless()
    {
        var rules = new HeistSortRules();
        rules.Trash(ItemKind.Bomb);
        rules.Miss(ItemKind.Bomb);
        Assert.AreEqual(0f, rules.Heat);
    }

    [Test]
    public void GoldPaysOnEitherSideOrTrashed()
    {
        var rules = new HeistSortRules();
        Assert.AreEqual(SortResult.Gold, rules.Catch(ItemKind.Gold, 0, 1));
        rules.Trash(ItemKind.Gold);
        Assert.AreEqual(4, rules.Craft(0));
        Assert.AreEqual(HeistSortRules.StartingBalance + rules.CashPerGold * 2, rules.Balance, 0.001);
    }

    [Test]
    public void GemTrashedOrMissedRaisesHeat()
    {
        var rules = new HeistSortRules();
        rules.Trash(ItemKind.Gem);
        rules.Miss(ItemKind.Gem);
        Assert.AreEqual(HeistSortRules.ErrorHeat * 2, rules.Heat, 0.001f);
    }

    [Test]
    public void HeatBleedsOnlyAfterASuccess()
    {
        var rules = new HeistSortRules();
        rules.Miss(ItemKind.Gem);
        rules.Tick(1f);
        Assert.AreEqual(HeistSortRules.ErrorHeat, rules.Heat, 0.001f);
        rules.Catch(ItemKind.Gem, 0, 0);
        rules.Tick(1f);
        Assert.AreEqual(HeistSortRules.ErrorHeat - HeistSortRules.BleedPerSecond, rules.Heat, 0.001f);
    }

    [Test]
    public void MaxHeatBustsLootButKeepsTheLeg()
    {
        var rules = new HeistSortRules();
        int perLeg = rules.VatFillsPerLeg * rules.CorrectsToFillVat;
        SortCorrect(rules, 0, perLeg + 2);
        int busts = 0;
        rules.Busted += () => busts++;
        rules.Catch(ItemKind.Bomb, 0, 0);
        rules.Catch(ItemKind.Bomb, 0, 0);
        rules.Catch(ItemKind.Bomb, 0, 0);
        Assert.AreEqual(1, busts);
        Assert.AreEqual(0f, rules.Heat);
        Assert.AreEqual(0, rules.Craft(0));
        Assert.AreEqual(0f, rules.Vat(0));
        Assert.AreEqual(2, rules.Leg);
    }

    [Test]
    public void LettersSpellTheWordAndPayTheBonus()
    {
        var rules = new HeistSortRules();
        Assert.AreEqual("HEIST", rules.ActiveWord);
        string completed = null;
        rules.WordCompleted += (word, _) => completed = word;
        foreach (char c in "HEIST")
            Assert.AreEqual(SortResult.Letter, rules.Catch(ItemKind.Letter, 0, 1, c));
        Assert.AreEqual("HEIST", completed);
        Assert.AreEqual("VAULT", rules.ActiveWord);
        Assert.AreEqual(HeistSortRules.StartingBalance + 25000d, rules.Balance, 0.001);
    }

    [Test]
    public void PickLetterOnlyOffersMissingLetters()
    {
        var rules = new HeistSortRules();
        foreach (char c in "HEIS") rules.Catch(ItemKind.Letter, 0, 0, c);
        for (float roll = 0f; roll < 1f; roll += 0.1f)
            Assert.AreEqual('T', rules.PickLetter(roll));
    }

    [Test]
    public void FlickDirectionReadsSwipes()
    {
        Assert.AreEqual(-1, HeistSortRules.FlickDirection(Vector2.zero, new Vector2(-200, 10), 80));
        Assert.AreEqual(1, HeistSortRules.FlickDirection(Vector2.zero, new Vector2(200, -10), 80));
        Assert.AreEqual(2, HeistSortRules.FlickDirection(Vector2.zero, new Vector2(10, 200), 80));
        Assert.AreEqual(0, HeistSortRules.FlickDirection(Vector2.zero, new Vector2(10, -200), 80));
        Assert.AreEqual(0, HeistSortRules.FlickDirection(Vector2.zero, new Vector2(30, 0), 80));
    }

    [Test]
    public void KindRollMatchesTheChances()
    {
        var rules = new HeistSortRules();
        Assert.AreEqual(ItemKind.Bomb, rules.KindForRoll(0.05f));
        Assert.AreEqual(ItemKind.Letter, rules.KindForRoll(0.2f));
        Assert.AreEqual(ItemKind.Gold, rules.KindForRoll(0.3f));
        Assert.AreEqual(ItemKind.Gem, rules.KindForRoll(0.9f));
    }
}
