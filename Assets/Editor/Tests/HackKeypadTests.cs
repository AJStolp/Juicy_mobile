using NUnit.Framework;

// The keypad labels its ladder lamps from ClueRules' ladder, and has a glyph
// and a color for every symbol in the pool.
public class HackKeypadTests
{
    [Test]
    public void LadderOfThreeLabelsEachRung()
    {
        Assert.AreEqual("$500", AlleyHack.RungLabel(3, 0));
        Assert.AreEqual("BRIBE", AlleyHack.RungLabel(3, 1));
        Assert.AreEqual("DRILL\nVAULT", AlleyHack.RungLabel(3, 2));
    }

    [Test]
    public void LadderOfFourLabelsEachRung()
    {
        Assert.AreEqual("$500", AlleyHack.RungLabel(4, 0));
        Assert.AreEqual("$1000", AlleyHack.RungLabel(4, 1));
        Assert.AreEqual("BRIBE", AlleyHack.RungLabel(4, 2));
        Assert.AreEqual("DRILL\nVAULT", AlleyHack.RungLabel(4, 3));
    }

    [Test]
    public void EverySymbolHasAGlyphAndColor()
    {
        Assert.AreEqual(ClueRules.SymbolPool, AlleyHack.Glyphs.Length);
        Assert.AreEqual(ClueRules.SymbolPool, AlleyHack.SymbolColors.Length);
        Assert.GreaterOrEqual(AlleyHack.Keys, ClueRules.CluesPerHackLater);
        Assert.LessOrEqual(AlleyHack.Keys, ClueRules.SymbolPool);
    }
}
