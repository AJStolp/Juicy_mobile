using NUnit.Framework;
using UnityEngine;

// A missed item now lands on the street and plays out there before it is
// recycled. These drive the director's item loop without a scene to show the
// miss still costs exactly what HeistSortRules.Miss charges, once.
public class AlleyMissTests
{
    const float Step = 1f / 30f;
    static readonly float[] ColumnX = { -1.05f, -0.35f, 0.35f, 1.05f };

    GameObject root;
    AlleyChuteDirector director;
    HeistSortRules rules;
    int landed;

    [SetUp]
    public void Build()
    {
        root = new GameObject("Miss_Test");
        var drops = new Transform[ColumnX.Length];
        for (int i = 0; i < drops.Length; i++)
        {
            drops[i] = new GameObject("Drop_0" + (i + 1)).transform;
            drops[i].SetParent(root.transform, false);
            drops[i].position = new Vector3(ColumnX[i], 3.15f, 0.15f);
        }

        var template = new GameObject("Item_Template").AddComponent<AlleyItem>();
        template.transform.SetParent(root.transform, false);
        GameObject gem = Model(template.transform, "Gem", 0.26f);
        template.Wire(gem, Model(template.transform, "Bomb", 0.34f), Model(template.transform, "Gold", 0.16f),
            Model(template.transform, "Letter", 0.28f), null, gem.AddComponent<GemColorController>());

        director = new GameObject("Director").AddComponent<AlleyChuteDirector>();
        director.transform.SetParent(root.transform, false);
        director.Configure(drops, new AlleyBin[0], template);
        rules = new HeistSortRules();
        director.Wire(rules, new VaultProgress());
        landed = 0;
        director.MissLanded += _ => landed++;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    static GameObject Model(Transform parent, string name, float size)
    {
        GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(model.GetComponent<Collider>());
        model.name = name;
        model.transform.SetParent(parent, false);
        model.transform.localScale = Vector3.one * size;
        return model;
    }

    AlleyItem DropUnswiped(ItemKind kind, int colorId, int chute)
    {
        AlleyItem item = director.Drop(kind, colorId, '\0', chute);
        for (int i = 0; i < 1000 && item.State == AlleyItem.Phase.Falling; i++) director.TickItems(Step);
        return item;
    }

    void Run(float seconds)
    {
        for (float t = 0f; t < seconds; t += Step) director.TickItems(Step);
    }

    [Test]
    public void MissedGemCostsExactlyWhatRulesMissChargesOnce()
    {
        var expected = new HeistSortRules();
        expected.Catch(ItemKind.Gem, 0, 0);
        expected.Miss(ItemKind.Gem);
        rules.Catch(ItemKind.Gem, 0, 0);

        AlleyItem item = DropUnswiped(ItemKind.Gem, 1, 0);

        Assert.AreEqual(AlleyItem.Phase.Landed, item.State);
        Assert.AreEqual(1, director.Misses);
        Assert.AreEqual(1, landed);
        Assert.AreEqual(expected.Heat, rules.Heat, 0.0001f);
        Assert.AreEqual(expected.Balance, rules.Balance, 0.0001);
        Assert.AreEqual(expected.Streak, rules.Streak);

        // Playing out the shatter on the street charges nothing more.
        Run(2f);
        Assert.AreEqual(AlleyItem.Phase.Idle, item.State);
        Assert.AreEqual(1, director.Misses);
        Assert.AreEqual(1, landed);
        Assert.AreEqual(expected.Heat, rules.Heat, 0.0001f);
        Assert.AreEqual(expected.Balance, rules.Balance, 0.0001);
    }

    [Test]
    public void MissedGemsFromEveryChuteLandInFrontOfTheChests()
    {
        // The chests stand at z -0.3, about 0.55 wide; their fronts are nearer
        // the camera than -0.55.
        for (int chute = 0; chute < ColumnX.Length; chute++)
        {
            AlleyItem item = DropUnswiped(ItemKind.Gem, 0, chute);
            Assert.AreEqual(AlleyItem.Phase.Landed, item.State, "chute " + chute);
            Assert.Less(item.transform.position.z, -0.55f, "chute " + chute);
            Assert.LessOrEqual(Mathf.Abs(item.transform.position.x), Mathf.Abs(ColumnX[chute]), "chute " + chute);
            Run(2f);
        }
        Assert.AreEqual(ColumnX.Length, director.Misses);
        Assert.AreEqual(HeistSortRules.ErrorHeat * ColumnX.Length, rules.Heat, 0.0001f);
    }

    [Test]
    public void MissedGoldLandsOnTheStreetAndCostsNothing()
    {
        AlleyItem item = DropUnswiped(ItemKind.Gold, 0, 3);
        Assert.AreEqual(AlleyItem.Phase.Landed, item.State);
        Assert.Less(item.transform.position.z, -0.55f);
        Assert.AreEqual(1, director.Misses);
        Assert.AreEqual(0f, rules.Heat);
        Assert.AreEqual(HeistSortRules.StartingBalance, rules.Balance, 0.0001);
        Run(2f);
        Assert.AreEqual(AlleyItem.Phase.Idle, item.State);
    }

    [Test]
    public void MissedDynamiteStillGoesStraightAwayAndCostsNothing()
    {
        AlleyItem item = DropUnswiped(ItemKind.Bomb, 0, 0);
        Assert.AreEqual(AlleyItem.Phase.Idle, item.State);
        Assert.AreEqual(1, director.Misses);
        Assert.AreEqual(0, landed);
        Assert.AreEqual(0f, rules.Heat);
    }
}
