using System.Collections.Generic;
using UnityEngine;

public class SortingBoardView : MonoBehaviour
{
    static readonly float[] ColumnViewportX = { 0.16f, 0.387f, 0.613f, 0.84f };
    const float BottomRowViewportY = 0.28f;
    const float TopRowViewportY = 0.74f;
    const float BoardPlaneZ = 0f;

    [SerializeField] private SortingGridManager grid;
    [SerializeField] private GemPoolManager pool;
    [SerializeField] private TubeView[] tubes;
    [SerializeField] private Camera boardCamera;

    readonly List<int> colorBuffer = new List<int>(4);

    public string ThemeName { get; private set; } = "";

    void Awake()
    {
        if (boardCamera == null) boardCamera = Camera.main;
        PositionTubes();
        if (tubes == null) return;
        for (int i = 0; i < tubes.Length; i++)
        {
            if (tubes[i] != null) tubes[i].SetTubeIndex(i);
        }
    }

    void OnEnable()
    {
        if (grid != null)
        {
            grid.BoardLoaded += Rebuild;
            grid.MoveCompleted += OnMoveCompleted;
        }

        WatchTheme();
    }

    void Start()
    {
        WatchTheme();
    }

    void OnDisable()
    {
        if (grid != null)
        {
            grid.BoardLoaded -= Rebuild;
            grid.MoveCompleted -= OnMoveCompleted;
        }

        if (GameCoreManager.Instance != null)
            GameCoreManager.Instance.OnThemeChanged -= ApplyTheme;
    }

    void WatchTheme()
    {
        if (GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.OnThemeChanged -= ApplyTheme;
        GameCoreManager.Instance.OnThemeChanged += ApplyTheme;
        if (GameCoreManager.Instance.ActiveTheme != null)
            ApplyTheme(GameCoreManager.Instance.ActiveTheme);
    }

    public void ApplyTheme(ThemeConfig theme)
    {
        ThemeName = theme != null ? theme.themeName : "";
        if (theme == null || theme.trackMaterial == null || tubes == null) return;
        for (int i = 0; i < tubes.Length; i++)
        {
            if (tubes[i] == null) continue;
            MeshRenderer renderer = tubes[i].GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = theme.trackMaterial;
        }
    }

    public void PositionTubes()
    {
        if (boardCamera == null || tubes == null) return;

        for (int i = 0; i < tubes.Length; i++)
        {
            if (tubes[i] == null) continue;
            int column = i % ColumnViewportX.Length;
            int row = i / ColumnViewportX.Length;
            float viewportY = row == 0 ? BottomRowViewportY : TopRowViewportY;
            tubes[i].transform.position = ViewportOnPlane(boardCamera, ColumnViewportX[column], viewportY, BoardPlaneZ);
        }
    }

    public void Rebuild()
    {
        if (grid == null || pool == null || tubes == null) return;

        for (int i = 0; i < tubes.Length; i++)
        {
            if (tubes[i] == null) continue;
            ClearTube(tubes[i]);
            if (i >= grid.TubeCount) continue;

            grid.CopyColorsBottomToTop(i, colorBuffer);
            for (int g = 0; g < colorBuffer.Count; g++)
            {
                GemView gem = pool.Rent(colorBuffer[g]);
                gem.AttachTo(tubes[i].transform);
                gem.transform.position = tubes[i].GetSlotPosition(g);
            }
        }
    }

    void OnMoveCompleted(bool solved)
    {
        if (!solved || GameCoreManager.Instance == null) return;
        GameCoreManager.Instance.ChangeState(GameCoreManager.GameState.Victory);
    }

    void ClearTube(TubeView tube)
    {
        for (int i = tube.transform.childCount - 1; i >= 0; i--)
        {
            GemView gem = tube.transform.GetChild(i).GetComponent<GemView>();
            if (gem != null) pool.Return(gem);
        }
    }

    public static Vector3 ViewportOnPlane(Camera cam, float viewportX, float viewportY, float planeZ)
    {
        Ray ray = cam.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));
        float denom = ray.direction.z;
        if (Mathf.Abs(denom) < 0.0001f) return cam.transform.position;
        float distance = (planeZ - ray.origin.z) / denom;
        return ray.origin + ray.direction * distance;
    }
}
