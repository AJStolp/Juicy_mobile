using NUnit.Framework;

public class ChuteTurnsTests
{
    [Test]
    public void EveryOpenChuteDropsOnceBeforeAnyDropsAgain()
    {
        var turns = new ChuteTurns(5);
        for (int round = 0; round < 20; round++)
        {
            var seen = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                int chute = turns.Next(4);
                Assert.That(chute, Is.InRange(0, 3));
                Assert.IsFalse(seen[chute], "chute " + chute + " dropped twice in round " + round);
                seen[chute] = true;
            }
        }
    }

    [Test]
    public void NoChuteDropsTwiceInARow()
    {
        foreach (int open in new[] { 2, 3, 4 })
        {
            var turns = new ChuteTurns(11);
            int last = -1;
            for (int i = 0; i < 400; i++)
            {
                int chute = turns.Next(open);
                Assert.AreNotEqual(last, chute, "drop " + i + " with " + open + " chutes open");
                last = chute;
            }
        }
    }

    [Test]
    public void OneChuteAlwaysDropsFromIt()
    {
        var turns = new ChuteTurns(3);
        for (int i = 0; i < 10; i++) Assert.AreEqual(0, turns.Next(1));
    }

    [Test]
    public void ANewlyOpenedChuteDropsInTheNextRound()
    {
        var turns = new ChuteTurns(7);
        turns.Next(2);
        bool newOne = false;
        for (int i = 0; i < 3; i++) newOne |= turns.Next(3) == 2;
        Assert.IsTrue(newOne);
    }

    [Test]
    public void TheSameSeedRepeatsTheChutes()
    {
        var a = new ChuteTurns(42);
        var b = new ChuteTurns(42);
        for (int i = 0; i < 50; i++)
        {
            int open = 1 + i / 12;
            Assert.AreEqual(a.Next(open), b.Next(open));
        }
    }
}
