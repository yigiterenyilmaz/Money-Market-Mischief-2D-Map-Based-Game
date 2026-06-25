using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yol kenarlarinda sokak lambalari yerlestiren sistem. Lamba DIREGI sprite'tir
/// (Inspector'dan atanir); ISIK ise runtime'da piksel bazli (halo) URETILIR — bolgeye
/// gore renklenir.
///
/// Gece/gunduz gecisi CROSSFADE DEGILDIR: dusk/dawn'in tam ortasinda (LightingRatio 0.5)
/// isik ANINDA acilir/kapanir.
///
/// Lambalar bina/araba gibi STATIK flat golge alir (MapDecorPlacer golge davranisi birebir).
/// </summary>
public class StreetLampPlacer : MonoBehaviour
{
    public static StreetLampPlacer Instance { get; private set; }

    [Header("References")]
    public MapPainter      mapPainter;
    public RoadGenerator   roadGenerator;
    [Tooltip("Golge parametreleri + bina cakisma kontrolu icin gerekli.")]
    public MapDecorPlacer  mapDecorPlacer;

    [Header("Lamp Sprite")]
    [Tooltip("Lamba diregi sprite'i.")]
    public Sprite lampSprite;

    [Header("Placement")]
    [Tooltip("Highway uzerinde kac piksel arayla lamba konur.")]
    [Range(10, 100)] public int highwayLampInterval = 40;
    [Tooltip("Branch yollar uzerinde kac piksel arayla lamba konur.")]
    [Range(10, 100)] public int branchLampInterval = 50;
    [Tooltip("Lambanin yol kenarindan piksel uzakligi.")]
    [Range(1, 25)] public int roadEdgeOffset = 10;
    [Tooltip("Lambanin binalardan minimum uzakligi (dunya birimi). Bina cakismasi bu kadar " +
             "yariçap icinde ise lamba atlanir.")]
    [Range(0f, 0.5f)] public float buildingClearance = 0.06f;

    [Header("Appearance")]
    [Tooltip("Lamba diregi sprite olcegi.")]
    [Range(0.001f, 0.2f)] public float lampScale = 0.01f;
    [Tooltip("Isik halo sprite olcegi (direge gore carpan).")]
    [Range(0.05f, 8f)] public float haloScaleMultiplier = 0.4f;
    [Tooltip("Halo saydamlik carpani — 1=renkteki alpha aynen, 0.3=cok saydam.")]
    [Range(0.05f, 1f)] public float haloAlphaMultiplier = 0.5f;
    [Tooltip("Halo sprite cozunurlugu (radius). Yuksek deger = yumusak gradient.")]
    [Range(4, 32)] public int haloSpriteRadius = 16;
    [Tooltip("Isigin (halo) dunya birimi cinsinden yukari kaydirilmasi — arabalardan yukarida dursun.")]
    [Range(0f, 0.2f)] public float lightVerticalOffset = 0.04f;
    [Tooltip("Halo dikey basiklik orani. 1=daire, <1=dikey kisa oval.")]
    [Range(0.2f, 1f)] public float haloVerticalSquash = 0.5f;
    [Tooltip("Lambalarin acilip-kapanma esigi yayilimı. 0=hepsi ayni anda (dusk/dawn ortasi), " +
             ">0=her lamba 0.5 etrafinda rastgele bir esikte acilir → lambalar tek tek yanar.")]
    [Range(0f, 0.45f)] public float lightSwitchSpread = 0.25f;

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
    [Tooltip("Lamba diregi sprite'inin sorting order'i — decor/bina seviyesinde.")]
    public int    sortingOrder = 15;
    [Tooltip("Isik halosunun sorting order'i. Isik TUM binalarin ustunde kalsin diye maksimuma " +
             "yakin tutulur. Binalar Y'ye gore (10 + wy*-100) siralanir; buyuk haritalarda alt " +
             "kenardaki binalar 1000+ deger alabildiginden bu deger cok yuksek (max 32767) olmali.")]
    public int    haloSortingOrder = 30000;

    // -------------------------------------------------------------------------

    private float pixelsPerUnit;
    private float halfW, halfH;
    private int   effectiveHaloSortingOrder;

    private List<LampData> lamps = new List<LampData>();
    private Transform lampParent;
    private bool  hasShadows  = false;
    private DayNightCycle dayNight;

    // Runtime uretilen halo sprite'lari — bolge basina
    private Dictionary<int, Sprite> haloSprites = new Dictionary<int, Sprite>();

    private class LampData
    {
        public GameObject     go;
        public SpriteRenderer poleSR;
        public SpriteRenderer haloSR;
        public MapDecorPlacer.ShadowHandle shadow;
        public float          onThreshold; // bu lamba LightingRatio bu degeri gecince acilir
        public bool           isOn;        // mevcut acik/kapali durum
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
        // Yollar DEGIL, DEKOR yerlestikten sonra basla — bina footprint'lerini gorebilmek icin
        // (OnDecorPlaced, road generation + bina yerlesiminden SONRA tetiklenir).
        MapDecorPlacer.OnDecorPlaced += HandleDecorPlaced;
        UndergroundMapManager.OnViewModeChanged += OnViewModeChanged;
    }

    void OnDisable()
    {
        MapDecorPlacer.OnDecorPlaced -= HandleDecorPlaced;
        UndergroundMapManager.OnViewModeChanged -= OnViewModeChanged;
    }

    void HandleDecorPlaced() { Initialize(); }

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
    // HALO SPRITE GENERATION
    // -------------------------------------------------------------------------

    void GenerateSprites()
    {
        haloSprites.Clear();
        for (int b = 0; b <= 4; b++)
            haloSprites[b] = CreateHaloSprite(haloSpriteRadius, GetLightColorForBiome(b));
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

        if (lampSprite == null)
        { Debug.LogError("StreetLampPlacer: lampSprite not assigned."); return; }

        hasShadows = mapDecorPlacer != null;
        if (!hasShadows)
            Debug.LogWarning("StreetLampPlacer: mapDecorPlacer atanmadi — lambalar golgesiz ve " +
                             "bina cakisma kontrolu olmadan yerlestirilecek.");

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

        // Halo TUM binalarin ustunde kalmali. Binalar sortOrder = 20 + wy*-100 ile siralanir;
        // en alt kenardaki bina en yuksek degeri alir (~20 + (|anchorY|+halfH)*100 + gece overlay).
        // Serilesmis haloSortingOrder kucuk kalmis olabilir (eski sahne) — bu yuzden harita
        // boyutundan gereken minimumu hesaplayip ikisinin BUYUGUNU kullaniyoruz (32767 max).
        SpriteRenderer mapSR = mapPainter.mapRenderer;
        float anchorY = mapSR != null ? mapSR.transform.position.y : 0f;
        int maxBuildingOrder = 20 + Mathf.CeilToInt((Mathf.Abs(anchorY) + halfH) * pixelsPerUnit) + 2;
        effectiveHaloSortingOrder = Mathf.Min(32767, Mathf.Max(haloSortingOrder, maxBuildingOrder + 10));

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

        // Baslangic durumunu uygula
        dayNight = DayNightCycle.Instance;
        ApplyLights(dayNight != null ? dayNight.LightingRatio : 0f, force: true);

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

            // Yol noktasi gercek (eylem yapilabilir) kara degilse — ornegin su uzerindeki
            // kopru/bridge — bu noktada lamba KOYMA. Boylece lambalar denizde gozukmez.
            if (!map.IsActionableLand(current.x, current.y))
                continue;

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

            // Lamba tile'i gercek kara olmali (su / deniz kayasi degil).
            if (!map.IsActionableLand(lampX, lampY))
                continue;

            // Dunya konumu — bina cakisma kontrolu icin.
            Vector3 world = TileToWorld(lampX, lampY);

            // Bina footprint'ine cok yakinsa lamba KOYMA (binalarin ustunde/dibinde olmasin).
            if (hasShadows && mapDecorPlacer.IsNearBuilding(world.x, world.y, buildingClearance))
                continue;

            // Biyom tespit — highway ise 0, branch ise lambanin bulundugu yerdeki biyom
            int biome = isHighway ? 0 : map.GetBiome(lampX, lampY);

            CreateLamp(world, biome);
            placeRight = !placeRight;
        }
    }

    Vector3 TileToWorld(int tileX, int tileY)
    {
        SpriteRenderer sr = mapPainter.mapRenderer;
        Vector3 anchor = sr != null ? sr.transform.position : Vector3.zero;
        float wx = anchor.x + (tileX / pixelsPerUnit) - halfW;
        float wy = anchor.y + (tileY / pixelsPerUnit) - halfH;
        float wz = anchor.z - 1.5f;
        return new Vector3(wx, wy, wz);
    }

    void CreateLamp(Vector3 world, int biome)
    {
        GameObject go = new GameObject("Lamp");
        go.transform.SetParent(lampParent);
        go.transform.position = world;
        go.transform.localScale = new Vector3(lampScale, lampScale, 1f);

        // Isik (halo) cocugunu yukari kaydirmak icin lokal offset.
        // localPosition parent (lampScale) ile carpildigindan dunya birimini boluyoruz.
        float lightLocalY = lampScale > 0f ? lightVerticalOffset / lampScale : 0f;

        // Lamba diregi sprite — decor seviyesinde siralanir.
        SpriteRenderer poleSR = go.AddComponent<SpriteRenderer>();
        poleSR.sprite           = lampSprite;
        poleSR.sortingLayerName = sortingLayer;
        poleSR.sortingOrder     = sortingOrder;
        poleSR.color            = Color.white;

        // Isik halosu (URETILEN, bolge renginde) — TUM binalarin ustunde siralanir
        // (isik havada asili kalmasin / bina ortmesin). Gece acilir, gunduz kapanir.
        GameObject haloGo = new GameObject("Halo");
        haloGo.transform.SetParent(go.transform, false);
        haloGo.transform.localPosition = new Vector3(0f, lightLocalY, 0f);
        // Dikey basik oval: y eksenini haloVerticalSquash ile kisalt.
        haloGo.transform.localScale = new Vector3(
            haloScaleMultiplier,
            haloScaleMultiplier * haloVerticalSquash,
            1f);
        SpriteRenderer haloSR = haloGo.AddComponent<SpriteRenderer>();
        haloSR.sprite           = haloSprites.ContainsKey(biome) ? haloSprites[biome] : haloSprites[0];
        haloSR.sortingLayerName = sortingLayer;
        haloSR.sortingOrder     = effectiveHaloSortingOrder;
        haloSR.color            = Color.white;
        haloGo.SetActive(false); // ApplyLights acar/kapar

        // Golge — bina flat golge (Projection/Trace) mantigi birebir. Sprite siluetinden uretilir;
        // lamba GO'su (lampScale) altinda durdugu icin olcek otomatik miras alinir.
        MapDecorPlacer.ShadowHandle shadow =
            hasShadows ? mapDecorPlacer.CreateFlatShadow(go, lampSprite, sortingOrder, sortingLayer) : null;

        // Her lamba 0.5 etrafinda rastgele bir esikte acilsin → lambalar ayni anda degil,
        // dusk/dawn boyunca tek tek yanip soner. Esik [0.05, 0.95] araliginda tutulur.
        float threshold = Mathf.Clamp(0.5f + Random.Range(-lightSwitchSpread, lightSwitchSpread), 0.05f, 0.95f);

        lamps.Add(new LampData
        {
            go          = go,
            poleSR      = poleSR,
            haloSR      = haloSR,
            shadow      = shadow,
            onThreshold = threshold,
            isOn        = false,
        });
    }

    // -------------------------------------------------------------------------
    // SHADOW  (MapDecorPlacer ortak flat gölge — Projection/Trace, bina ile birebir)
    // -------------------------------------------------------------------------

    // Gölgeler MapDecorPlacer.CreateFlatShadow ile lamba GO'su altinda uretilir; her frame burada
    // ortak ComputeShadowSunParams + UpdateFlatShadow ile bina gölgeleriyle ayni güneşe gore güncellenir.
    void UpdateShadows()
    {
        if (!hasShadows) return;
        if (dayNight == null) dayNight = DayNightCycle.Instance;
        if (dayNight == null) return;

        MapDecorPlacer.ShadowSunParams p = mapDecorPlacer.ComputeShadowSunParams(dayNight.SunProgress);

        for (int i = 0; i < lamps.Count; i++)
        {
            var sh = lamps[i].shadow;
            if (sh == null || sh.transform == null) continue;

            // Lamba gizliyse (underground vb.) gölgeyi de gizle.
            if (lamps[i].go == null || !lamps[i].go.activeSelf)
            {
                sh.transform.gameObject.SetActive(false);
                continue;
            }

            mapDecorPlacer.UpdateFlatShadow(sh, p);
        }
    }

    // -------------------------------------------------------------------------
    // UPDATE — LIGHT ON/OFF (crossfade DEGIL — dusk/dawn ortasinda anlik gecis)
    // -------------------------------------------------------------------------

    void Update()
    {
        if (lamps.Count == 0) return;

        if (dayNight == null) dayNight = DayNightCycle.Instance;
        if (dayNight == null) return;

        // LightingRatio dusk boyunca 0→1, dawn boyunca 1→0 (SmoothStep). Her lamba kendi
        // onThreshold'unu gecince acilir/kapanir → lambalar tek tek yanip soner.
        ApplyLights(dayNight.LightingRatio, force: false);

        UpdateShadows();
    }

    void ApplyLights(float ratio, bool force)
    {
        for (int i = 0; i < lamps.Count; i++)
        {
            var lamp = lamps[i];
            bool on = ratio >= lamp.onThreshold;
            if (!force && on == lamp.isOn) continue;
            lamp.isOn = on;
            if (lamp.haloSR != null) lamp.haloSR.gameObject.SetActive(on);
        }
    }

    // -------------------------------------------------------------------------
    // CLEANUP
    // -------------------------------------------------------------------------

    void ClearLamps()
    {
        foreach (var lamp in lamps)
        {
            // Gölge artik lamba GO'sunun cocugu — lamba yok edilince o da gider.
            if (lamp.go != null) Destroy(lamp.go);
        }
        lamps.Clear();
    }
}
