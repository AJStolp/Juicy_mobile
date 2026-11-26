using NUnit.Framework;

// The keypad has a glyph and a color for every clue symbol, a key for every
// clue a place can hold, and a lamp per key.
public class HackKeypadTests
{
    [Test]
    public void EverySymbolHasAGlyphAndColor()
    {
        Assert.AreEqual(ClueRules.SymbolPool, AlleyHack.Glyphs.Length);
        Assert.AreEqual(ClueRules.SymbolPool, AlleyHack.SymbolColors.Length);
        Assert.LessOrEqual(AlleyHack.Keys, ClueRules.SymbolPool);
        Assert.AreEqual(AlleyHack.Keys, AlleyHack.Lamps);
    }
}
