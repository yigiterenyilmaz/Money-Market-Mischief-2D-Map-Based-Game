using System;
using System.Collections.Generic;
using UnityEngine;

public class UndergroundMapManager : MonoBehaviour
{
    public static UndergroundMapManager Instance { get; private set; }

    [Header("References")]
    public MapGenerator mapGenerator;
    public MapPainter   mapPainter;

    [Header("Underground Colors")]
    [Tooltip("Base color for undiscovered land underground.")]
    public Color undiscoveredDark  = new Color(0.18f, 0.12f, 0.07f);
    public Color undiscoveredLight = new Color(0.26f, 0.18f, 0.10f);

    [Tooltip("Color for discovered (researched) land underground.")]
    public Color discoveredDark  = new Color(0.45f, 0.34f, 0.20f);
    public Color discoveredLight = new Color(0.58f, 0.45f, 0.28f);

    [Tooltip("Color for discovered petroleum tiles.")]
    public Color petroleumColor = new Color(0.02f, 0.02f, 0.02f);

    [Tooltip("Water color in underground view.")]
    public Color undergroundWater = new Color(0.06f, 0.08f, 0.14f);

    [Header("Noise")]
    [Range(0.01f, 0.1f)] public float undergroundNoiseScale = 0.04f;

    [Header("Discovery Blend")]
    [Tooltip("What fraction of the circle radius is solid discovered (no blend). Rest fades out.")]
    [Range(0.3f, 0.95f)] public float blendSolidCore = 0.7f;

    [Header("Fog")]
    [Tooltip("Fog color for the underground view edges.")]
    public Color undergroundFogColor = new Color(0.12f, 0.10f, 0.08f);

    public enum ViewMode { Surface, Underground }
    public ViewMode CurrentView => currentView;

    public static event Action<ViewMode> OnViewModeChanged;

    private ViewMode currentView = ViewMode.Surface;
    private bool[,]  discoveredMap;
    private Texture2D undergroundTexture;
    private Sprite    surfaceSprite;
    private Sprite    undergroundSprite;
    private bool      ready;
    private int       mapW, mapH;
    private float     noiseSeed;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        PetroleumBedGenerator.OnPetroleumGenerated += OnReady;
    }

    void OnDisable()
    {
        PetroleumBedGenerator.OnPetroleumGenerated -= OnReady;
    }

    void OnReady()
    {
        if (mapGenerator == null || mapPainter == null) return;
        mapW = mapGenerator.width;
        mapH = mapGenerator.height;
        discoveredMap = new bool[mapW, mapH];
        noiseSeed = UnityEngine.Random.Range(0f, 9999f);

        if (mapPainter.mapRenderer != null && mapPainter.mapRenderer.sprite != null)
            surfaceSprite = mapPainter.mapRenderer.sprite;

        BuildUndergroundTexture();
        ready = true;
        currentView = ViewMode.Surface;
    }

    // === TEXTURE GENERATION ===

    void BuildUndergroundTexture()
    {
        if (undergroundTexture != null) Destroy(undergroundTexture);
        undergroundTexture = new Texture2D(mapW, mapH, TextureFormat.RGBA32, false);
        undergroundTexture.filterMode = FilterMode.Point;

        Color[] pixels = new Color[mapW * mapH];

        for (int x = 0; x < mapW; x++)
        {
            for (int y = 0; y < mapH; y++)
            {
                Color c;
                if (!mapGenerator.IsLand(x, y))
                    c = undergroundWater;
                else
                    c = GetUndergroundLandColor(x, y, false, false);

                float fog = mapGenerator.GetFog(x, y);
                if (fog > 0f)
                    c = Color.Lerp(c, undergroundFogColor, fog);

                pixels[x + y * mapW] = c;
            }
        }

        undergroundTexture.SetPixels(pixels);
        undergroundTexture.Apply();

        undergroundSprite = Sprite.Create(
            undergroundTexture,
            new Rect(0, 0, mapW, mapH),
            new Vector2(0.5f, 0.5f), 100f);
        undergroundSprite.name = "UndergroundMap";
    }

    [Header("Treasure")]
    [Tooltip("Color for discovered treasure tiles on the underground map.")]
    public Color treasureColor = new Color(0.85f, 0.72f, 0.15f);

    Color GetUndergroundLandColor(int x, int y, bool discovered, bool hasPetroleum, bool hasTreasure = false)
    {
        if (hasTreasure && discovered)
            return treasureColor;
        if (hasPetroleum && discovered)
            return petroleumColor;

        float n1 = Mathf.PerlinNoise(x * undergroundNoiseScale + noiseSeed,
                                      y * undergroundNoiseScale + noiseSeed);
        float n2 = Mathf.PerlinNoise(x * undergroundNoiseScale * 2.5f + noiseSeed + 500f,
                                      y * undergroundNoiseScale * 2.5f + noiseSeed + 500f) * 0.4f;
        float n = (n1 + n2) / 1.4f;

        if (discovered)
            return Color.Lerp(discoveredDark, discoveredLight, n);
        else
            return Color.Lerp(undiscoveredDark, undiscoveredLight, n);
    }

    // === DISCOVERY ===

    /// <summary>
    /// Stores the highest blend value per tile (0 = undiscovered, 1 = fully discovered).
    /// Persists across multiple reveals so overlapping circles look correct.
    /// </summary>
    private float[,] blendMap;

    public void RevealCircle(Vector2Int center, int radius)
    {
        if (!ready) return;
        if (blendMap == null) blendMap = new float[mapW, mapH];

        var bedGen = PetroleumBedGenerator.Instance;
        var treasureGen = TreasureGenerator.Instance;

        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            float distSq = dx * dx + dy * dy;
            if (distSq > radius * radius) continue;

            int px = center.x + dx, py = center.y + dy;
            if (px < 0 || px >= mapW || py < 0 || py >= mapH) continue;
            if (!mapGenerator.IsLand(px, py)) continue;

            float dist = Mathf.Sqrt(distSq);
            float normDist = dist / radius;

            float t;
            if (normDist <= blendSolidCore)
                t = 1f;
            else
                t = 1f - ((normDist - blendSolidCore) / (1f - blendSolidCore));

            t = Mathf.Clamp01(t);

            if (t <= blendMap[px, py]) continue;
            blendMap[px, py] = t;

            if (t >= 0.99f)
            {
                discoveredMap[px, py] = true;

                // Discover treasure at this tile if present
                if (treasureGen != null && treasureGen.IsGenerated)
                    treasureGen.DiscoverTreasure(new Vector2Int(px, py));
            }

            bool hasPetroleum = bedGen != null && bedGen.IsGenerated && bedGen.HasPetroleum(px, py);
            bool hasTreasure  = treasureGen != null && treasureGen.IsGenerated && treasureGen.HasTreasure(px, py);

            Color undiscovered = GetUndergroundLandColor(px, py, false, false, false);
            Color discovered   = GetUndergroundLandColor(px, py, true, hasPetroleum, hasTreasure);
            Color c = Color.Lerp(undiscovered, discovered, t);

            float fog = mapGenerator.GetFog(px, py);
            if (fog > 0f)
                c = Color.Lerp(c, undergroundFogColor, fog);

            undergroundTexture.SetPixel(px, py, c);
        }

        undergroundTexture.Apply();

        // Notify treasure system to refresh sprites for newly discovered treasures
        if (TreasureSystem.Instance != null)
            TreasureSystem.Instance.OnTilesRevealed();
    }

    public bool IsDiscovered(int x, int y)
    {
        if (!ready || x < 0 || x >= mapW || y < 0 || y >= mapH) return false;
        return discoveredMap[x, y];
    }

    // === VIEW TOGGLE ===

    public void ToggleView()
    {
        if (!ready) return;
        SetView(currentView == ViewMode.Surface ? ViewMode.Underground : ViewMode.Surface);
    }

    public void SetView(ViewMode mode)
    {
        if (!ready) return;
        currentView = mode;

        bool isSurface = (mode == ViewMode.Surface);

        if (mapPainter.mapRenderer != null)
            mapPainter.mapRenderer.sprite = isSurface ? surfaceSprite : undergroundSprite;

        var decorPlacer = mapPainter.GetComponent<MapDecorPlacer>();
        if (decorPlacer != null)
            decorPlacer.SetDecorVisible(isSurface);

        if (PetroleumSystem.Instance != null)
            PetroleumSystem.Instance.SetPumpsVisible(isSurface);

        OnViewModeChanged?.Invoke(mode);
    }

    public void RefreshSurfaceSprite()
    {
        if (mapPainter != null && mapPainter.mapRenderer != null && mapPainter.mapRenderer.sprite != null)
        {
            if (currentView == ViewMode.Surface)
                surfaceSprite = mapPainter.mapRenderer.sprite;
        }
    }

    // === DEBUG ===

    public void DebugRevealAll()
    {
        if (!ready) return;
        var bedGen = PetroleumBedGenerator.Instance;
        var treasureGen = TreasureGenerator.Instance;

        if (treasureGen != null && treasureGen.IsGenerated)
            treasureGen.DebugDiscoverAll();

        for (int x = 0; x < mapW; x++)
        for (int y = 0; y < mapH; y++)
        {
            if (!mapGenerator.IsLand(x, y)) continue;
            discoveredMap[x, y] = true;

            bool hasPetroleum = bedGen != null && bedGen.IsGenerated && bedGen.HasPetroleum(x, y);
            bool hasTreasure  = treasureGen != null && treasureGen.IsGenerated && treasureGen.HasTreasure(x, y);
            Color c = GetUndergroundLandColor(x, y, true, hasPetroleum, hasTreasure);

            float fog = mapGenerator.GetFog(x, y);
            if (fog > 0f)
                c = Color.Lerp(c, undergroundFogColor, fog);

            undergroundTexture.SetPixel(x, y, c);
        }

        undergroundTexture.Apply();

        if (TreasureSystem.Instance != null)
            TreasureSystem.Instance.OnTilesRevealed();
    }

    public bool IsReady => ready;
}