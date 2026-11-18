using NUnit.Framework;
using UnityEngine;

public class PlinkoBulbsTests
{
    GameObject root;
    AlleyPlinkoBulbs bulbs;
    Transform[] pins;

    [SetUp]
    public void Build()
    {
        root = new GameObject("Board");
        pins = new Transform[3];
        var renderers = new Renderer[3];
        for (int i = 0; i < pins.Length; i++)
        {
            pins[i] = new GameObject("Pin_0_" + i).transform;
            pins[i].SetParent(root.transform);
            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.transform.SetParent(root.transform);
            renderers[i] = bulb.GetComponent<Renderer>();
        }
        bulbs = root.AddComponent<AlleyPlinkoBulbs>();
        bulbs.Configure(pins, renderers, Color.black, Color.white);
    }

    [TearDown]
    public void Clear()
    {
        Object.DestroyImmediate(root);
    }

    [Test]
    public void AStruckPinLightsOnlyItsOwnBulb()
    {
        bulbs.Flash(pins[1]);
        Assert.AreEqual(0f, bulbs.Level(0));
        Assert.AreEqual(1f, bulbs.Level(1));
        Assert.AreEqual(0f, bulbs.Level(2));
    }

    [Test]
    public void ALitBulbFadesBackToIdle()
    {
        bulbs.Flash(pins[0]);
        bulbs.Fade(0.1f);
        float partway = bulbs.Level(0);
        Assert.Less(partway, 1f);
        Assert.Greater(partway, 0f);
        bulbs.Fade(1f);
        Assert.AreEqual(0f, bulbs.Level(0));
    }

    [Test]
    public void ATransformThatIsNotAPinChangesNothing()
    {
        bulbs.Flash(root.transform);
        for (int i = 0; i < bulbs.Count; i++) Assert.AreEqual(0f, bulbs.Level(i));
    }
}
