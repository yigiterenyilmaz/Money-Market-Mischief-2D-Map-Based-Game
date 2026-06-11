using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yol kenarlarinda sokak lambalari yerlestiren ve gece/gunduz gecisinde
/// isiklandirma saglayan sistem. Lamba sprite'lari runtime'da piksel bazli uretilir.
/// Her bolge icin farkli isik rengi destekler.
/// </summary>
public class StreetLampPlacer : MonoBehaviour
{
    public static StreetLampPlacer Instance { get; private set; }

    [Header("References")]
    public MapPainter    mapPainter;
    public RoadGenerator roadGenerator;

    [Header("Placement")]
    [Tooltip("Highway uzerinde kac piksel arayla lamba konur.")]
    [Range(10, 100)] public int highwayLampInterval = 40;
    [Tooltip("Branch yollar uzerinde kac piksel arayla lamba konur.")]
    [Range(10, 100)] public int branchLampInterval = 50;
    [Tooltip("Lambanin yol kenarindan piksel uzakligi.")]
    [Range(1, 25)] public int roadEdgeOffset = 10;

    [Header("Appearance")]
    [Tooltip("Lamba diregi sprite olcegi.")]
    [Range(0.001f, 0.1f)] public float lampScale = 0.00125f;
    [Tooltip("Isik halo sprite olcegi (direge gore carpan).")]
    [Range(0.1f, 4f)] public float haloScaleMultiplier = 0.15f;
    [Tooltip("Halo saydamlik carpani — 1=renkteki alpha aynen, 0.3=cok saydam.")]
    [Range(0.05f, 1f)] public float haloAlphaMultiplier = 0.5f;
    [Tooltip("Halo sprite cozunurlugu (radius). Yuksek deger = yumusak gradient.")]
    [Range(4, 32)] public int haloSpriteRadius = 16;
    [Tooltip("Direk rengi.")]
    public Color poleColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    [Header("Light Colors — Per Region")]
    [Tooltip("Highway isik rengi.")]
    public Color highwayLightColor = new Color(1f, 0.95f, 0.7f, 0.6f);
    [Tooltip("Agricultural (Forest/Biome 1) isik rengi.")]
    public Color agriculturalLightColor = new Color(1f, 0.85f, 0.4f, 0.5f);
    [Tooltip("Cities (Desert/Biome 2) isik rengi.")]
    public Color citiesLightColor = new Color(1f, 0.95f, 0.8f, 0.7f);
    [Tooltip("Industrial (Mountains/Biome 3) isik rengi.")]
    public Color industrialLightColor = new Color(1f, 0.7f, 0.3f, 0.6f);
    [Tooltip("Urban (Plains/Biome 4) isik rengi.")]
    public Color urbanLightColor = new Color(0.9f, 0.9f, 1f, 0.65f);

    [Header("Sorting")]
    public string sortingLayer = "Default";
    public int    sortingOrder = 15;

    // -------------------------------------------------------------------------

    private float pixelsPerUnit;
    private float halfW, halfH;

    private List<LampData> lamps = new List<LampData>();
    private Transform lampParent;
    private float prevLightingRatio = -1f;

    // Runtime uretilen sprite'lar — bolge basina
    private Sprite poleDaySprite;
    private Dictionary<int, Sprite> poleNightSprites = new Dictionary<int, Sprite>();
    private Dictionary<int, Sprite> haloSprites      = new Dictionary<int, Sprite>();

    private struct LampData
    {
        public GameObject     go;
        public SpriteRenderer poleDaySR;
        public SpriteRenderer poleNightSR;
        public SpriteRenderer haloSR;
        public Color          lightColor; // bu lambanin isik rengi (bolgeye gore)
    }

    // -------------------------------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        RoadGenerator.OnRoadsGenerated += HandleRoadsGenerated;
        UndergroundMapManager.OnViewModeChanged += OnViewModeChanged;
    }

    void OnDisable()
    {
        RoadGenerator.OnRoadsGenerated -= HandleRoadsGenerated;
        UndergroundMapManager.OnViewModeChanged -= OnViewModeChanged;
    }

    void HandleRoadsGenerated() { Initialize(); }

    void OnViewModeChanged(UndergroundMapManager.ViewMode mode)
    {
        bool visible = mode == UndergroundMapManager.ViewMode.Surface;
        foreach (var lamp in lamps)
            if (lamp.go != null) lamp.go.SetActive(visible);
    }

    // -------------------------------------------------------------------------
    // BIOME → COLOR MAPPING
    // -------------------------------------------------------------------------

    // biome: 0=highway, 1=Forest/Agricultural, 2=Desert/Cities, 3=Mountains/Industrial, 4=Plains/Urban
    Color GetLightColorForBiome(int biome)
    {
        switch (biome)
        {
            case 1:  return agriculturalLightColor;
            case 2:  return citiesLightColor;
            case 3:  return industrialLightColor;
            case 4:  return urbanLightColor;
            default: return highwayLightColor;
        }
    }

    // -------------------------------------------------------------------------
    // SPRITE GENERATION
    // -------------------------------------------------------------------------

    void GenerateSprites()
    {
        poleNightSprites.Clear();
        haloSprites.Clear();

        // Gun diregi — tum bolgeler icin ayni
        poleDaySprite = CreatePoleSprite(poleColor, Color.clear);

        // Her bolge icin gece diregi ve halo sprite'i uret (0=highway, 1-4=biyomlar)
        for (int b = 0; b <= 4; b++)
        {
            Color lc = GetLightColorForBiome(b);
            poleNightSprites[b] = CreatePoleSprite(poleColor, lc);
            haloSprites[b]      = CreateHaloSprite(haloSpriteRadius, lc);
        }
    }

    /// <summary>
    /// Lamba diregi sprite'i olusturur.
    /// nightHeadColor alpha > 0 ise gece versiyonu (kafa isik renginde), degilse gun versiyonu.
    /// </summary>
    Sprite CreatePoleSprite(Color pole, Color nightHeadColor)
    {
        // Top-down gorunum: 2x2 piksel kare
        int s = 2;
        Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool isNight = nightHeadColor.a > 0f;
        Color col = isNight
            ? new Color(nightHeadColor.r, nightHeadColor.g, nightHeadColor.b, 1f)
            : pole;

        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, col);

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 1f);
    }

    Sprite CreateHaloSprite(int radius, Color color)
    {
        int size = radius * 2 + 1;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float t = Mathf.Clamp01(dist / radius);
                // Yumusak ucsuz gradient — kenarlar tamamen saydam
                float falloff = (1f - t) * (1f - t);
                float alpha = color.a * haloAlphaMultiplier * falloff;
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 1f);
    }

    // -------------------------------------------------------------------------
    // INIT
    // -------------------------------------------------------------------------

    void Initialize()
    {
        if (roadGenerator == null || mapPainter == null)
        { Debug.LogError("StreetLampPlacer: references not assigned."); return; }

        ClearLamps();
        GenerateSprites();

        if (lampParent == null)
        {
            var go = new GameObject("StreetLamps");
            go.transform.SetParent(transform);
            lampParent = go.transform;
        }

        MapGenerator mapGen = mapPainter.mapGenerator;
        pixelsPerUnit = 100f;
        halfW = mapGen.width  * 0.5f / pixelsPerUnit;
        halfH = mapGen.height * 0.5f / pixelsPerUnit;

        // Highway lambalari (biome=0 → highway rengi)
        var highways = roadGenerator.GetHighwaySegments();
        if (highways != null)
            foreach (var seg in highways)
                PlaceLampsAlongPath(seg, highwayLampInterval, mapGen, true);

        // Branch lambalari (biyom bazli renk)
        var branches = roadGenerator.GetBranchPaths();
        if (branches != null)
            foreach (var seg in branches)
                PlaceLampsAlongPath(seg, branchLampInterval, mapGen, false);

        // Baslangic crossfade uygula
        var dn = DayNightCycle.Instance;
        if (dn != null) ApplyCrossfade(dn.LightingRatio);

        // Underground kontrolu
        if (UndergroundMapManager.Instance != null &&
            UndergroundMapManager.Instance.CurrentView == UndergroundMapManager.ViewMode.Underground)
        {
            foreach (var lamp in lamps)
                if (lamp.go != null) lamp.go.SetActive(false);
        }

        Debug.Log($"StreetLampPlacer: {lamps.Count} lamba yerlestirildi.");
    }

    // -------------------------------------------------------------------------
    // PLACEMENT
    // -------------------------------------------------------------------------

    void PlaceLampsAlongPath(List<Vector2Int> path, int interval, MapGenerator map, bool isHighway)
    {
        if (path == null || path.Count < interval) return;

        bool placeRight = true;

        for (int i = interval / 2; i < path.Count - 1; i += interval)
        {
            Vector2Int current = path[i];

            // Yonu hesapla
            int lookAhead  = Mathf.Min(i + 5, path.Count - 1);
            int lookBehind = Mathf.Max(i - 5, 0);
            Vector2 dir = new Vector2(
                path[lookAhead].x - path[lookBehind].x,
                path[lookAhead].y - path[lookBehind].y
            ).normalized;

            if (dir == Vector2.zero) continue;

            // Perpendikular yon
            Vector2 perp = placeRight
                ? new Vector2(-dir.y, dir.x)
                : new Vector2(dir.y, -dir.x);

            int lampX = current.x + Mathf.RoundToInt(perp.x * roadEdgeOffset);
            int lampY = current.y + Mathf.RoundToInt(perp.y * roadEdgeOffset);

            if (lampX < 0 || lampX >= map.width || lampY < 0 || lampY >= map.height)
                continue;

            if (!map.IsLand(lampX, lampY))
                continue;

            // Biyom tespit — highway ise 0, branch ise lambanin bulundugu yerdeki biyom
            int biome = isHighway ? 0 : map.GetBiome(lampX, lampY);

            CreateLamp(lampX, lampY, biome);
            placeRight = !placeRight;
        }
    }

    void CreateLamp(int tileX, int tileY, int biome)
    {
        SpriteRenderer sr = mapPainter.mapRenderer;
        Vector3 anchor = sr != null ? sr.transform.position : Vector3.zero;

        float wx = anchor.x + (tileX / pixelsPerUnit) - halfW;
        float wy = anchor.y + (tileY / pixelsPerUnit) - halfH;
        float wz = anchor.z - 1.5f;

        Color lc = GetLightColorForBiome(biome);

        GameObject go = new GameObject("Lamp");
        go.transform.SetParent(lampParent);
        go.transform.position = new Vector3(wx, wy, wz);
        go.transform.localScale = new Vector3(lampScale, lampScale, 1f);

        // Gun diregi
        SpriteRenderer poleDaySR = go.AddComponent<SpriteRenderer>();
        poleDaySR.sprite           = poleDaySprite;
        poleDaySR.sortingLayerName = sortingLayer;
        poleDaySR.sortingOrder     = sortingOrder;
        poleDaySR.color            = Color.white;

        // Gece diregi (isikli kafa — bolge renginde)
        GameObject nightPoleGo = new GameObject("NightPole");
        nightPoleGo.transform.SetParent(go.transform, false);
        nightPoleGo.transform.localPosition = Vector3.zero;
        nightPoleGo.transform.localScale    = Vector3.one;
        SpriteRenderer poleNightSR = nightPoleGo.AddComponent<SpriteRenderer>();
        poleNightSR.sprite           = poleNightSprites.ContainsKey(biome) ? poleNightSprites[biome] : poleNightSprites[0];
        poleNightSR.sortingLayerName = sortingLayer;
        poleNightSR.sortingOrder     = sortingOrder + 1;
        poleNightSR.color            = new Color(1f, 1f, 1f, 0f);

        // Isik halosu (bolge renginde)
        GameObject haloGo = new GameObject("Halo");
        haloGo.transform.SetParent(go.transform, false);
        haloGo.transform.localPosition = Vector3.zero;
        haloGo.transform.localScale    = Vector3.one * haloScaleMultiplier;
        SpriteRenderer haloSR = haloGo.AddComponent<SpriteRenderer>();
        haloSR.sprite           = haloSprites.ContainsKey(biome) ? haloSprites[biome] : haloSprites[0];
        haloSR.sortingLayerName = sortingLayer;
        haloSR.sortingOrder     = sortingOrder + 2;
        haloSR.color            = new Color(1f, 1f, 1f, 0f);

        lamps.Add(new LampData
        {
            go          = go,
            poleDaySR   = poleDaySR,
            poleNightSR = poleNightSR,
            haloSR      = haloSR,
            lightColor  = lc,
        });
    }

    // -------------------------------------------------------------------------
    // UPDATE — CROSSFADE
    // -------------------------------------------------------------------------

    void Update()
    {
        if (lamps.Count == 0) return;

        var dn = DayNightCycle.Instance;
        if (dn == null) return;

        float ratio = dn.LightingRatio;
        if (Mathf.Abs(ratio - prevLightingRatio) < 0.005f) return;
        prevLightingRatio = ratio;

        ApplyCrossfade(ratio);
    }

    void ApplyCrossfade(float ratio)
    {
        for (int i = 0; i < lamps.Count; i++)
        {
            var lamp = lamps[i];
            if (lamp.poleDaySR == null) continue;

            // Gun diregi sabit opak — altta silueti doldurur, arka plan sizmaz.
            lamp.poleDaySR.color = new Color(1f, 1f, 1f, 1f);

            // Gece diregi (isikli) ustte — ratio ile fade in/out.
            if (lamp.poleNightSR != null)
                lamp.poleNightSR.color = new Color(1f, 1f, 1f, ratio);

            // Isik halosu — gece beliriyor, lambanin kendi bolge rengiyle
            if (lamp.haloSR != null)
                lamp.haloSR.color = new Color(1f, 1f, 1f, ratio);
        }
    }

    // -------------------------------------------------------------------------
    // CLEANUP
    // -------------------------------------------------------------------------

    void ClearLamps()
    {
        foreach (var lamp in lamps)
            if (lamp.go != null) Destroy(lamp.go);
        lamps.Clear();
    }
}
