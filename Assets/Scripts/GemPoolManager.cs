using System.Collections.Generic;
using UnityEngine;

public class GemPoolManager : MonoBehaviour
{
    [SerializeField] private GameObject gemPrefab;
    [SerializeField] private int prewarmCount = 32;
    [SerializeField] private Color[] palette =
    {
        new Color(0.90f, 0.16f, 0.22f),
        new Color(0.20f, 0.45f, 0.95f),
        new Color(0.16f, 0.78f, 0.42f),
        new Color(0.96f, 0.76f, 0.22f)
    };

    readonly Queue<GemView> available = new Queue<GemView>(32);

    void Awake()
    {
        if (gemPrefab == null) gemPrefab = CreateFallbackPrefab();

        for (int i = 0; i < prewarmCount; i++)
        {
            GemView gem = CreateInstance();
            gem.gameObject.SetActive(false);
            available.Enqueue(gem);
        }
    }

    public GemView Rent(int colorId)
    {
        GemView gem = available.Count > 0 ? available.Dequeue() : CreateInstance();
        Color color = palette[Mathf.Abs(colorId) % palette.Length];
        gem.ApplyColor(colorId, color);
        gem.gameObject.SetActive(true);
        return gem;
    }

    public void Return(GemView gem)
    {
        if (gem == null) return;
        gem.gameObject.SetActive(false);
        gem.transform.SetParent(transform, false);
        available.Enqueue(gem);
    }

    GemView CreateInstance()
    {
        GameObject gemObject = Instantiate(gemPrefab, transform);
        gemObject.name = "Gem";
        GemView gem = gemObject.GetComponent<GemView>();
        if (gem == null) gem = gemObject.AddComponent<GemView>();
        return gem;
    }

    GameObject CreateFallbackPrefab()
    {
        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        template.name = "GemTemplate";
        Collider collider = template.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        template.transform.SetParent(transform, false);
        return template;
    }
}
