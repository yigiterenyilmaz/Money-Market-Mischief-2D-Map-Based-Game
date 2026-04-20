using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CarColorEntry
{
    public Color color = Color.white;
    [Range(0f, 10f)] public float spawnWeight = 1f;
}

public class RoadTrafficSystem : MonoBehaviour
{
    public static RoadTrafficSystem Instance { get; private set; }

    [Header("References")]
    public MapPainter    mapPainter;
    public RoadGenerator roadGenerator;

    [Header("Car Sprite")]
    public Sprite carSprite;
    [Tooltip("Night variant of the car sprite (headlights on). Leave empty to skip crossfade.")]
    public Sprite carSpriteNight;
    [Tooltip("Headlight-only sprite (pre-colored lights on transparent bg). Rendered untinted, fades in at night.")]
    public Sprite carSpriteHeadlights;
    public string carSortingLayer = "Default";
    public int    carSortingOrder = 20;
    public float  pixelsPerUnit   = 100f;

    [Header("Spawn Rates")]
    [Range(0.5f, 5f)] public float highwayCarDensity = 2f;
    [Range(0.1f, 3f)] public float branchCarDensity  = 0.8f;

    [Header("Car Appearance")]
    [Range(0.01f, 2f)]  public float carScale          = 0.05f;
    [Range(0f,  0.3f)]  public float carScaleVariation = 0.1f;
    public List<CarColorEntry> carColors = new List<CarColorEntry>();

    [Header("Speed")]
    [Range(0.01f, 5f)]  public float baseSpeed      = 0.5f;
    [Range(0f,   0.5f)] public float speedVariation = 0.2f;

    [Header("Rotation")]
    [Range(1,   40)]  public int   directionLookahead   = 10;
    [Range(1f, 30f)]  public float directionSmoothSpeed = 10f;
    [Range(0f, 720f)] public float flipRotationSpeed    = 0f;

    // -------------------------------------------------------------------------

    private struct JunctionEntry { public int pathIndex; public int pixelIndex; }

    private class Car
    {
        public int   pathIndex;
        public float position;
        public float speedInPixels;
        public Color color;
        public float scaleFactor;
        public GameObject     go;
        public SpriteRenderer daySR;
        public SpriteRenderer nightSR;       // null if no night sprite assigned
        public SpriteRenderer headlightSR;   // null if no headlight sprite assigned
        public Quaternion     currentRotation;
        public Vector2        smoothedDir;
        public Vector3        smoothedWorldPos;
        public float          fadeAlpha;      // 0=görünmez, 1=tamamen görünür
        public bool           isFadingOut;    // yok olma geçişinde mi
        public bool           isFadingIn;     // belirme geçişinde mi
        public bool           pendingRespawn; // fade-out bittikten sonra yeniden spawn olacak mı
        public float          switchCooldown; // yol değiştirdikten sonra tekrar değiştiremez (piksel)
        public int            previousPathIndex; // son geldiği yol — hemen geri dönmeyi engeller
        //Kavsak gecisi: cubic bezier ile yumusak donus. Transition aktifken path ilerleme durur.
        public bool           transitioning;
        public float          transT;
        public float          transDuration;
        public Vector3        transP0, transP1, transP2, transP3;
        public int            pendingPathIndex;
        public float          pendingPosition;
        public float          pendingSpeed;
        public Vector2        pendingEndDir;
    }

    private List<List<Vector2Int>>    allPaths       = new List<List<Vector2Int>>();
    private List<Car>                 activeCars     = new List<Car>();
    private Queue<GameObject>         pool           = new Queue<GameObject>();
    private int                       highwayPathCount;
    private List<List<JunctionEntry>> junctionGroups = new List<List<JunctionEntry>>();
    private Transform carParent;
    private float     mapHalfW, mapHalfH, worldUnitsPerPixel;
    private bool      active = false;

    private HashSet<Vector2Int> brokenRoadTiles = new HashSet<Vector2Int>();

    // crossfade tracking
    private float prevLightingRatio = -1f;

    // -------------------------------------------------------------------------
    // LIFECYCLE
    // -------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        RoadGenerator.OnRoadsGenerated          += HandleRoadsGenerated;
        UndergroundMapManager.OnViewModeChanged += OnViewModeChanged;
    }

    void OnDisable()
    {
        RoadGenerator.OnRoadsGenerated          -= HandleRoadsGenerated;
        UndergroundMapManager.OnViewModeChanged -= OnViewModeChanged;
    }

    void HandleRoadsGenerated() { StartCoroutine(InitAfterFrame()); }
    IEnumerator InitAfterFrame() { yield return null; Initialize(); }

    void OnViewModeChanged(UndergroundMapManager.ViewMode mode)
        => SetTrafficVisible(mode == UndergroundMapManager.ViewMode.Surface);

    // -------------------------------------------------------------------------
    // INIT
    // -------------------------------------------------------------------------

    void Initialize()
    {
        if (roadGenerator == null || mapPainter == null)
        { Debug.LogError("RoadTrafficSystem: references not assigned."); return; }
        if (carSprite == null)
        { Debug.LogError("RoadTrafficSystem: carSprite not assigned."); return; }

        StopTraffic();
        ReturnAllCarsToPool();

        if (carParent == null)
        {
            var c = new GameObject("CarTraffic");
            c.transform.SetParent(transform);
            carParent = c.transform;
        }

        MapGenerator mapGen = mapPainter.mapGenerator;
        mapHalfW           = mapGen.width  * 0.5f / pixelsPerUnit;
        mapHalfH           = mapGen.height * 0.5f / pixelsPerUnit;
        worldUnitsPerPixel = 1f / pixelsPerUnit;

        allPaths.Clear();
        activeCars.Clear();

        var highways = roadGenerator.GetHighwaySegments();
        if (highways != null)
            foreach (var seg in highways)
                if (seg != null && seg.Count >= 10) allPaths.Add(seg);

        highwayPathCount = allPaths.Count;

        var branches = roadGenerator.GetBranchPaths();
        if (branches != null)
            foreach (var seg in branches)
                if (seg != null && seg.Count >= 10) allPaths.Add(seg);

        BuildJunctions();
        SpawnCars();

        if (UndergroundMapManager.Instance != null &&
            UndergroundMapManager.Instance.CurrentView == UndergroundMapManager.ViewMode.Underground)
            SetTrafficVisible(false);
        else
            active = true;
    }

    void SpawnCars()
    {
        bool hasNight      = carSpriteNight      != null;
        bool hasHeadlights = carSpriteHeadlights != null;

        for (int p = 0; p < allPaths.Count; p++)
        {
            float density = (p < highwayPathCount) ? highwayCarDensity : branchCarDensity;
            int count = Mathf.Max(1, Mathf.RoundToInt(allPaths[p].Count / 100f * density));
            for (int c = 0; c < count; c++)
            {
                float pos      = Random.Range(0f, allPaths[p].Count - 1f);
                float worldSpd = baseSpeed * Random.Range(1f - speedVariation, 1f + speedVariation);
                float pixelSpd = worldSpd / worldUnitsPerPixel;
                if (Random.value > 0.5f) pixelSpd = -pixelSpd;

                Color col   = PickCarColor();
                float scale = carScale * Random.Range(1f - carScaleVariation, 1f + carScaleVariation);
                int   idx   = Mathf.Clamp(Mathf.RoundToInt(pos), 0, allPaths[p].Count - 1);

                Car car = new Car
                {
                    pathIndex        = p,
                    position         = pos,
                    speedInPixels    = pixelSpd,
                    color            = col,
                    scaleFactor      = scale,
                    smoothedDir      = GetDirectionAtIndex(p, idx, pixelSpd),
                    smoothedWorldPos = InterpolatedWorldPos(p, pos),
                    fadeAlpha        = 1f,
                    previousPathIndex = -1
                };
                car.currentRotation = DirToRotation(car.smoothedDir);

                car.go = GetFromPool();

                // --- Day renderer (first child or root SpriteRenderer) ---
                car.daySR = car.go.GetComponent<SpriteRenderer>();
                car.daySR.sprite           = carSprite;
                car.daySR.color            = col;
                car.daySR.sortingLayerName = carSortingLayer;
                car.daySR.sortingOrder     = carSortingOrder;

                // --- Night overlay renderer (child, tinted with car color) ---
                if (hasNight)
                {
                    Transform nightChild = car.go.transform.Find("NightOverlay");
                    if (nightChild == null)
                    {
                        GameObject nightGo = new GameObject("NightOverlay");
                        nightGo.transform.SetParent(car.go.transform, false);
                        nightGo.transform.localPosition = Vector3.zero;
                        nightGo.transform.localScale    = Vector3.one;
                        nightGo.transform.localRotation = Quaternion.identity;
                        car.nightSR = nightGo.AddComponent<SpriteRenderer>();
                    }
                    else
                    {
                        car.nightSR = nightChild.GetComponent<SpriteRenderer>();
                    }

                    car.nightSR.sprite           = carSpriteNight;
                    car.nightSR.color            = new Color(col.r, col.g, col.b, 0f);
                    car.nightSR.sortingLayerName = carSortingLayer;
                    car.nightSR.sortingOrder     = carSortingOrder + 1;
                }
                else
                {
                    car.nightSR = null;
                }

                // --- Headlight overlay (child, UNTINTED, fades in at night) ---
                if (hasHeadlights)
                {
                    Transform hlChild = car.go.transform.Find("HeadlightOverlay");
                    if (hlChild == null)
                    {
                        GameObject hlGo = new GameObject("HeadlightOverlay");
                        hlGo.transform.SetParent(car.go.transform, false);
                        hlGo.transform.localPosition = Vector3.zero;
                        hlGo.transform.localScale    = Vector3.one;
                        hlGo.transform.localRotation = Quaternion.identity;
                        car.headlightSR = hlGo.AddComponent<SpriteRenderer>();
                    }
                    else
                    {
                        car.headlightSR = hlChild.GetComponent<SpriteRenderer>();
                    }

                    car.headlightSR.sprite           = carSpriteHeadlights;
                    car.headlightSR.color            = new Color(1f, 1f, 1f, 0f); // untinted, start invisible
                    car.headlightSR.sortingLayerName = carSortingLayer;
                    car.headlightSR.sortingOrder     = carSortingOrder + 2; // on top of both day & night
                }
                else
                {
                    car.headlightSR = null;
                }

                car.go.transform.localScale = new Vector3(scale, scale, 1f);
                car.go.transform.position   = car.smoothedWorldPos;
                car.go.transform.rotation   = car.currentRotation;
                car.go.SetActive(true);

                activeCars.Add(car);
            }
        }

        // Apply initial crossfade
        var dn = DayNightCycle.Instance;
        if (dn != null) ApplyCarCrossfade(dn.LightingRatio);
    }

    Color PickCarColor()
    {
        if (carColors == null || carColors.Count == 0) return Color.white;
        float total = 0f;
        foreach (var e in carColors) total += Mathf.Max(0f, e.spawnWeight);
        if (total <= 0f) return carColors[0].color;
        float roll = Random.Range(0f, total), cum = 0f;
        foreach (var e in carColors) { cum += e.spawnWeight; if (roll <= cum) return e.color; }
        return carColors[carColors.Count - 1].color;
    }

    // -------------------------------------------------------------------------
    // UPDATE
    // -------------------------------------------------------------------------

    void Update()
    {
        if (!active || activeCars.Count == 0) return;

        // --- Smooth crossfade ---
        UpdateCarCrossfade();

        float dt = Time.deltaTime;
        float fadeSpeed = 2f; //saniyede alpha değişim hızı (0.5sn tam geçiş)

        for (int i = activeCars.Count - 1; i >= 0; i--)
        {
            Car car = activeCars[i];

            //fade-out işleme
            if (car.isFadingOut)
            {
                car.fadeAlpha = Mathf.Max(0f, car.fadeAlpha - fadeSpeed * dt);
                ApplyCarAlpha(car);

                if (car.fadeAlpha <= 0f)
                {
                    car.isFadingOut = false;

                    if (car.pendingRespawn)
                    {
                        //rastgele başka bir branch yolun ucundan fade-in ile spawn
                        car.pendingRespawn = false;

                        //rastgele bir branch yolun çıkmaz ucundan fade-in ile spawn
                        var deadEnd = FindRandomDeadEnd(car.pathIndex);
                        if (deadEnd.pathIndex >= 0)
                        {
                            car.pathIndex = deadEnd.pathIndex;
                            car.position = deadEnd.pixelIndex;
                            float spd = baseSpeed / worldUnitsPerPixel * Random.Range(0.8f, 1.2f);
                            //çıkmaz uçtan içeri doğru git
                            car.speedInPixels = deadEnd.pixelIndex == 0 ? spd : -spd;

                            car.color = PickCarColor();
                            car.daySR.color = car.color;
                            car.previousPathIndex = -1;
                            car.switchCooldown = 0f;

                            car.smoothedWorldPos = InterpolatedWorldPos(car.pathIndex, car.position);
                            car.go.transform.position = car.smoothedWorldPos;
                            car.isFadingIn = true;
                        }
                    }
                }

                activeCars[i] = car;
                continue;
            }

            //fade-in işleme
            if (car.isFadingIn)
            {
                car.fadeAlpha = Mathf.Min(1f, car.fadeAlpha + fadeSpeed * dt);
                ApplyCarAlpha(car);
                if (car.fadeAlpha >= 1f)
                    car.isFadingIn = false;
            }

            //Kavsak gecis animasyonu — cubic bezier ile pozisyon+yon. Transition bitince yeni yola yerlesir.
            if (car.transitioning)
            {
                car.transT += dt / Mathf.Max(0.001f, car.transDuration);
                float t = Mathf.Clamp01(car.transT);
                float u = 1f - t;
                Vector3 pos = u*u*u*car.transP0 + 3f*u*u*t*car.transP1
                            + 3f*u*t*t*car.transP2 + t*t*t*car.transP3;
                Vector3 deriv = 3f*u*u*(car.transP1 - car.transP0)
                              + 6f*u*t*(car.transP2 - car.transP1)
                              + 3f*t*t*(car.transP3 - car.transP2);
                Vector2 dir = new Vector2(deriv.x, deriv.y);
                if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
                else dir = car.pendingEndDir;

                car.smoothedWorldPos = pos;
                car.smoothedDir = dir;
                car.currentRotation = DirToRotation(dir);
                car.go.transform.position = pos;
                car.go.transform.rotation = car.currentRotation;

                if (t >= 1f)
                {
                    car.transitioning = false;
                    car.pathIndex     = car.pendingPathIndex;
                    car.position      = car.pendingPosition;
                    car.speedInPixels = car.pendingSpeed;
                    car.smoothedDir   = car.pendingEndDir;
                }

                activeCars[i] = car;
                continue;
            }

            if (Mathf.Approximately(car.speedInPixels, 0f))
            {
                activeCars[i] = car;
                continue;
            }

            List<Vector2Int> path = allPaths[car.pathIndex];
            car.position += car.speedInPixels * dt;

            // ---- LOOKAHEAD: stop if a broken tile is coming up ahead ----
            if (brokenRoadTiles.Count > 0)
            {
                int curIdx = Mathf.Clamp(Mathf.RoundToInt(car.position), 0, path.Count - 1);
                int lookDir  = car.speedInPixels >= 0 ? 1 : -1;
                int lookStart = curIdx + lookDir;
                int lookEnd   = curIdx + lookDir * (directionLookahead + 8);
                int lo        = Mathf.Clamp(Mathf.Min(lookStart, lookEnd), 0, path.Count - 1);
                int hi        = Mathf.Clamp(Mathf.Max(lookStart, lookEnd), 0, path.Count - 1);

                bool crackAhead = false;
                for (int pi = lo; pi <= hi; pi++)
                    if (brokenRoadTiles.Contains(path[pi])) { crackAhead = true; break; }

                if (crackAhead)
                {
                    car.speedInPixels = 0f;
                    activeCars[i]     = car;
                    continue;
                }
            }

            bool didFlip = false;

            //yol değiştirme cooldown'unu azalt
            if (car.switchCooldown > 0f)
                car.switchCooldown -= Mathf.Abs(car.speedInPixels) * dt;

            int currentIdx = Mathf.Clamp(Mathf.RoundToInt(car.position), 0, path.Count - 1);
            if (car.switchCooldown <= 0f && car.pathIndex < highwayPathCount && Random.value < 0.002f)
            {
                var midConns = FindConnections(car.pathIndex, currentIdx, 5);
                //önceki yolu filtrele
                midConns.RemoveAll(e => e.pathIndex == car.previousPathIndex);
                if (midConns.Count > 0)
                {
                    var target    = midConns[Random.Range(0, midConns.Count)];
                    var currentPx = path[currentIdx];
                    float absSpd  = Mathf.Abs(car.speedInPixels);
                    float newPos  = FindNearestPixelOnPath(allPaths[target.pathIndex], currentPx);
                    float newSpd  = AlignSpeedAwayFromJunction(target.pathIndex, Mathf.RoundToInt(newPos), currentPx, absSpd);
                    car.previousPathIndex = car.pathIndex;
                    car.switchCooldown = 150f;
                    BeginCarTransition(car, target.pathIndex, newPos, newSpd);
                    activeCars[i] = car;
                    continue;
                }
            }

            int  pathLen      = path.Count;
            bool reachedEnd   = car.position >= pathLen - 1;
            bool reachedStart = car.position < 0;

            if (reachedEnd || reachedStart)
            {
                int endIdx      = reachedStart ? 0 : pathLen - 1;
                var connections = FindConnections(car.pathIndex, endIdx, 3);
                float turnChance = car.pathIndex < highwayPathCount ? 0.8f : 0.5f;

                //önceki yolu filtrele — hemen geri dönmeyi engelle (cooldown aktifken filtrele)
                var unfilteredCount = connections.Count;
                if (car.switchCooldown > 0f)
                    connections.RemoveAll(e => e.pathIndex == car.previousPathIndex);

                //branch arabalar kavşakta her zaman yol değiştirir, highway arabalar %80 şansla
                bool shouldSwitch;
                if (car.pathIndex >= highwayPathCount)
                    shouldSwitch = connections.Count > 0; //branch: her zaman geç
                else
                    shouldSwitch = connections.Count > 0 && Random.value < turnChance; //highway: şansla

                if (shouldSwitch && car.switchCooldown <= 0f)
                {
                    var target    = connections[Random.Range(0, connections.Count)];
                    var currentPx = path[endIdx];
                    float absSpd  = Mathf.Abs(car.speedInPixels);
                    float newPos  = FindNearestPixelOnPath(allPaths[target.pathIndex], currentPx);
                    float newSpd  = AlignSpeedAwayFromJunction(target.pathIndex, Mathf.RoundToInt(newPos), currentPx, absSpd);
                    car.previousPathIndex = car.pathIndex;
                    car.switchCooldown = 150f;
                    BeginCarTransition(car, target.pathIndex, newPos, newSpd);
                    activeCars[i] = car;
                    continue;
                }
                else if (unfilteredCount > 0 || car.pathIndex < highwayPathCount)
                {
                    //kavşak var ama dönmedi veya ana otoban — geri dön
                    if (reachedEnd)   { car.position = pathLen - 1; car.speedInPixels = -Mathf.Abs(car.speedInPixels); }
                    else              { car.position = 0;           car.speedInPixels =  Mathf.Abs(car.speedInPixels); }
                }
                else
                {
                    //çıkmaz yolun sonunda — fade-out başlat, sonra yeniden spawn
                    if (!car.isFadingOut)
                    {
                        car.isFadingOut = true;
                        car.pendingRespawn = true;
                        car.speedInPixels = 0f;
                        if (reachedEnd)   car.position = pathLen - 1;
                        else              car.position = 0;
                    }
                }
                didFlip = true;
            }

            Vector3 targetWorld = InterpolatedWorldPos(car.pathIndex, car.position);
            if (didFlip)
                car.smoothedWorldPos = targetWorld;
            else
                car.smoothedWorldPos = Vector3.Lerp(car.smoothedWorldPos, targetWorld, dt * 8f);
            car.go.transform.position = car.smoothedWorldPos;

            int     finalIdx = Mathf.Clamp(Mathf.RoundToInt(car.position), 0, path.Count - 1);
            Vector2 rawDir   = GetDirectionAtIndex(car.pathIndex, finalIdx, car.speedInPixels);
            if (didFlip)
            {
                car.smoothedDir     = rawDir;
                car.currentRotation = DirToRotation(rawDir);
            }
            else
            {
                car.smoothedDir = Vector2.Lerp(car.smoothedDir, rawDir, dt * directionSmoothSpeed).normalized;
                if (car.smoothedDir == Vector2.zero) car.smoothedDir = rawDir;
                car.currentRotation = DirToRotation(car.smoothedDir);
            }
            car.go.transform.rotation = car.currentRotation;

            activeCars[i] = car;
        }
    }

    // -------------------------------------------------------------------------
    // DAY / NIGHT CROSSFADE
    // -------------------------------------------------------------------------

    void UpdateCarCrossfade()
    {
        if (carSpriteNight == null && carSpriteHeadlights == null) return;

        var dn = DayNightCycle.Instance;
        if (dn == null) return;

        float ratio = dn.LightingRatio;
        if (Mathf.Abs(ratio - prevLightingRatio) < 0.005f) return;
        prevLightingRatio = ratio;

        ApplyCarCrossfade(ratio);
    }

    void ApplyCarAlpha(Car car)
    {
        if (car.daySR != null)
        {
            Color c = car.daySR.color;
            c.a = car.fadeAlpha * (1f - prevLightingRatio);
            car.daySR.color = c;
        }
        if (car.nightSR != null)
        {
            Color c = car.nightSR.color;
            c.a = car.fadeAlpha * Mathf.Max(0f, prevLightingRatio);
            car.nightSR.color = c;
        }
        if (car.headlightSR != null)
        {
            Color c = car.headlightSR.color;
            c.a = car.fadeAlpha * Mathf.Max(0f, prevLightingRatio);
            car.headlightSR.color = c;
        }
    }

    void ApplyCarCrossfade(float ratio)
    {
        for (int i = 0; i < activeCars.Count; i++)
        {
            Car car = activeCars[i];
            if (car.daySR == null) continue;

            Color col = car.color;

            // Day layer: tinted with car color, fades out as night approaches
            car.daySR.color = new Color(col.r, col.g, col.b, 1f - ratio);

            // Night layer: tinted with car color, fades in as night approaches
            if (car.nightSR != null)
                car.nightSR.color = new Color(col.r, col.g, col.b, ratio);

            // Headlight layer: UNTINTED (white), fades in with LightingRatio
            // Since LightingRatio uses SmoothStep in DayNightCycle, the
            // headlights ramp on smoothly during dusk and off during dawn.
            if (car.headlightSR != null)
                car.headlightSR.color = new Color(1f, 1f, 1f, ratio);
        }
    }

    // -------------------------------------------------------------------------
    // ROAD BREAKING
    // -------------------------------------------------------------------------

    public void OnRoadsBreaking(IReadOnlyCollection<Vector2Int> broken)
    {
        foreach (var t in broken) brokenRoadTiles.Add(t);

        for (int i = 0; i < activeCars.Count; i++)
        {
            Car car  = activeCars[i];
            var path = allPaths[car.pathIndex];

            int curIdx   = Mathf.Clamp(Mathf.RoundToInt(car.position), 0, path.Count - 1);
            int lookDir  = car.speedInPixels >= 0 ? 1 : -1;
            int lookEnd  = curIdx + lookDir * (directionLookahead + 8);
            int lo       = Mathf.Clamp(Mathf.Min(curIdx, lookEnd), 0, path.Count - 1);
            int hi       = Mathf.Clamp(Mathf.Max(curIdx, lookEnd), 0, path.Count - 1);

            for (int pi = lo; pi <= hi; pi++)
            {
                if (!brokenRoadTiles.Contains(path[pi])) continue;
                car.speedInPixels = 0f;
                activeCars[i]     = car;
                break;
            }
        }

        Debug.Log($"RoadTrafficSystem: roads broken, cars facing cracks stopped.");
    }

    public bool IsRoadBroken(int x, int y) => brokenRoadTiles.Contains(new Vector2Int(x, y));

    // -------------------------------------------------------------------------
    // HELPERS
    // -------------------------------------------------------------------------

    Vector3 InterpolatedWorldPos(int pathIndex, float position)
    {
        var path  = allPaths[pathIndex];
        int count = path.Count;
        int idxA  = Mathf.Clamp(Mathf.FloorToInt(position), 0, count - 1);
        int idxB  = Mathf.Clamp(idxA + 1, 0, count - 1);
        float t   = position - Mathf.FloorToInt(position);
        return Vector3.Lerp(TileToWorld(path[idxA]), TileToWorld(path[idxB]), t);
    }

    /// <summary>
    /// Branch yolların çıkmaz uçlarından (bağlantısız) rastgele birini bulur.
    /// excludePathIndex: aynı yoldan spawn etmemek için hariç tutulacak path.
    /// </summary>
    JunctionEntry FindRandomDeadEnd(int excludePathIndex)
    {
        var candidates = new List<JunctionEntry>();

        for (int p = highwayPathCount; p < allPaths.Count; p++)
        {
            if (p == excludePathIndex) continue;

            //yolun başı çıkmaz mı?
            var startConns = FindConnections(p, 0, 3);
            if (startConns.Count == 0)
                candidates.Add(new JunctionEntry { pathIndex = p, pixelIndex = 0 });

            //yolun sonu çıkmaz mı?
            var endConns = FindConnections(p, allPaths[p].Count - 1, 3);
            if (endConns.Count == 0)
                candidates.Add(new JunctionEntry { pathIndex = p, pixelIndex = allPaths[p].Count - 1 });
        }

        if (candidates.Count == 0)
            return new JunctionEntry { pathIndex = -1, pixelIndex = 0 };

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Yol değiştirdikten sonra hız yönünü junction noktasından uzaklaşan yöne göre belirler.
    /// Yeni yolun iki yönünden hangisi junction'dan daha uzağa gidiyorsa onu seçer.
    /// </summary>
    float AlignSpeedAwayFromJunction(int newPathIndex, int posOnNewPath, Vector2Int junctionTile, float absSpeed)
    {
        var newPath = allPaths[newPathIndex];
        int count = newPath.Count;
        int lookAhead = Mathf.Min(30, count / 3);

        int fwdIdx = Mathf.Clamp(posOnNewPath + lookAhead, 0, count - 1);
        int bwdIdx = Mathf.Clamp(posOnNewPath - lookAhead, 0, count - 1);

        //hangi yön junction'dan daha uzağa gidiyor?
        float fwdDist = Vector2Int.Distance(newPath[fwdIdx], junctionTile);
        float bwdDist = Vector2Int.Distance(newPath[bwdIdx], junctionTile);

        return fwdDist >= bwdDist ? absSpeed : -absSpeed;
    }

    //Kavsakta araba yumusak bir kavisle yeni yola geciyor. Cubic bezier kontrol noktalari:
    //P0=baslangic, P1=P0 + startDir*handle (mevcut yon), P2=P3 - endDir*handle (yeni yol yonu), P3=hedef.
    //Aric uzunlugu ~ distance*1.15 ile hizin korunacagi sekilde transDuration hesaplanir.
    void BeginCarTransition(Car car, int newPathIndex, float newPos, float newSpeed)
    {
        var newPath = allPaths[newPathIndex];
        int newIdx = Mathf.Clamp(Mathf.RoundToInt(newPos), 0, newPath.Count - 1);
        Vector3 endWorld = InterpolatedWorldPos(newPathIndex, newPos);
        Vector2 endDir = GetDirectionAtIndex(newPathIndex, newIdx, newSpeed);
        if (endDir.sqrMagnitude > 0.0001f) endDir.Normalize(); else endDir = Vector2.right;

        Vector3 startWorld = car.smoothedWorldPos;
        Vector2 startDir = car.smoothedDir.sqrMagnitude > 0.0001f ? car.smoothedDir.normalized : endDir;

        float distWorld = Vector3.Distance(startWorld, endWorld);
        //Cok yakinsa kivrilmaya gerek yok — direkt yerlestir.
        if (distWorld < worldUnitsPerPixel * 0.5f)
        {
            car.pathIndex     = newPathIndex;
            car.position      = newPos;
            car.speedInPixels = newSpeed;
            car.smoothedWorldPos = endWorld;
            car.smoothedDir   = endDir;
            car.currentRotation = DirToRotation(endDir);
            car.go.transform.position = endWorld;
            car.go.transform.rotation = car.currentRotation;
            return;
        }

        float handleLen = distWorld * 0.45f;
        car.transP0 = startWorld;
        car.transP1 = startWorld + new Vector3(startDir.x, startDir.y, 0f) * handleLen;
        car.transP2 = endWorld   - new Vector3(endDir.x,   endDir.y,   0f) * handleLen;
        car.transP3 = endWorld;
        car.pendingEndDir = endDir;

        //Gercek hizda kat etsin: worldSpeed = piksel hizi * unit/piksel. Arc uzunlugu ~ dist*1.15.
        float worldSpeed = Mathf.Max(0.001f, Mathf.Abs(car.speedInPixels) * worldUnitsPerPixel);
        car.transDuration = Mathf.Max(0.15f, distWorld * 1.15f / worldSpeed);
        car.transT = 0f;
        car.transitioning = true;

        car.pendingPathIndex = newPathIndex;
        car.pendingPosition  = newPos;
        car.pendingSpeed     = newSpeed;
    }

    int FindNearestPixelOnPath(List<Vector2Int> path, Vector2Int fromPixel)
    {
        int   bestIdx  = 0;
        float bestDist = float.MaxValue;
        int   step     = Mathf.Max(1, path.Count / 100);
        for (int i = 0; i < path.Count; i += step)
        {
            float d = Vector2Int.Distance(fromPixel, path[i]);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        int from = Mathf.Max(0, bestIdx - step), to = Mathf.Min(path.Count - 1, bestIdx + step);
        for (int i = from; i <= to; i++)
        {
            float d = Vector2Int.Distance(fromPixel, path[i]);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        return bestIdx;
    }

    Vector2 GetDirectionAtIndex(int pathIndex, int idx, float speed)
    {
        var path  = allPaths[pathIndex];
        int count = path.Count;
        int ahead = speed >= 0 ? directionLookahead : -directionLookahead;
        int ai    = Mathf.Clamp(idx + ahead,  0, count - 1);
        int bi    = Mathf.Clamp(idx - ahead, 0, count - 1);
        if (ai == bi) return Vector2.up;
        Vector2 dir = new Vector2(path[ai].x - path[bi].x, path[ai].y - path[bi].y).normalized;
        return dir == Vector2.zero ? Vector2.up : dir;
    }

    Quaternion DirToRotation(Vector2 dir)
        => Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

    Vector3 TileToWorld(Vector2Int tile)
    {
        SpriteRenderer sr = mapPainter.mapRenderer;
        Vector3 anchor    = sr != null ? sr.transform.position : Vector3.zero;
        return new Vector3(
            anchor.x + (tile.x / pixelsPerUnit) - mapHalfW,
            anchor.y + (tile.y / pixelsPerUnit) - mapHalfH,
            anchor.z - 1f);
    }

    // -------------------------------------------------------------------------
    // POOL
    // -------------------------------------------------------------------------

    GameObject GetFromPool()
    {
        while (pool.Count > 0) { var go = pool.Dequeue(); if (go != null) return go; }
        var newGo = new GameObject("Car");
        newGo.transform.SetParent(carParent);
        newGo.AddComponent<SpriteRenderer>();
        return newGo;
    }

    void ReturnToPool(Car car)
    {
        if (car.go == null) return;
        car.go.SetActive(false);
        pool.Enqueue(car.go);
    }

    void ReturnAllCarsToPool()
    {
        foreach (var car in activeCars) ReturnToPool(car);
        activeCars.Clear();
    }

    // -------------------------------------------------------------------------
    // JUNCTIONS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Yol path'lerinde su boşluklarını tespit eder.
    /// Ardışık pikseller arası mesafe 3'ten fazlaysa, boşluğun iki tarafını
    /// brokenRoadTiles'a ekler — arabalar bu noktada durur.
    /// </summary>
    void DetectPathGaps()
    {
        for (int p = 0; p < allPaths.Count; p++)
        {
            var path = allPaths[p];
            for (int i = 0; i < path.Count - 1; i++)
            {
                float dist = Vector2Int.Distance(path[i], path[i + 1]);
                if (dist > 3f)
                {
                    //büyük atlama = su boşluğu — her iki tarafı da kırık olarak işaretle
                    brokenRoadTiles.Add(path[i]);
                    brokenRoadTiles.Add(path[i + 1]);
                    //etrafındaki birkaç pikseli de ekle ki lookahead yakalasın
                    for (int j = Mathf.Max(0, i - 3); j <= Mathf.Min(path.Count - 1, i + 4); j++)
                        brokenRoadTiles.Add(path[j]);
                }
            }
        }
    }

    void BuildJunctions()
    {
        junctionGroups.Clear();
        float junctionRadius = 8f;

        for (int p = 0; p < allPaths.Count; p++)
        for (int endIdx = 0; endIdx < 2; endIdx++)
        {
            int myPixelIdx     = endIdx == 0 ? 0 : allPaths[p].Count - 1;
            Vector2Int myPixel = allPaths[p][myPixelIdx];

            for (int other = 0; other < allPaths.Count; other++)
            {
                if (other == p) continue;
                int   bestIdx  = -1;
                float bestDist = float.MaxValue;
                int   step     = Mathf.Max(1, allPaths[other].Count / 80);
                for (int i = 0; i < allPaths[other].Count; i += step)
                {
                    float d = Vector2Int.Distance(myPixel, allPaths[other][i]);
                    if (d < bestDist) { bestDist = d; bestIdx = i; }
                }
                int from = Mathf.Max(0, bestIdx - step);
                int to   = Mathf.Min(allPaths[other].Count - 1, bestIdx + step);
                for (int i = from; i <= to; i++)
                {
                    float d = Vector2Int.Distance(myPixel, allPaths[other][i]);
                    if (d < bestDist) { bestDist = d; bestIdx = i; }
                }
                if (bestDist <= junctionRadius)
                {
                    var group = new List<JunctionEntry>();
                    group.Add(new JunctionEntry { pathIndex = p,     pixelIndex = myPixelIdx });
                    group.Add(new JunctionEntry { pathIndex = other, pixelIndex = bestIdx    });
                    junctionGroups.Add(group);
                }
            }
        }
        Debug.Log($"RoadTrafficSystem: {junctionGroups.Count} junctions.");
    }

    List<JunctionEntry> FindConnections(int pathIndex, int pixelIndex, int tolerance)
    {
        var results = new List<JunctionEntry>();
        foreach (var group in junctionGroups)
        {
            bool matched = false;
            foreach (var e in group)
                if (e.pathIndex == pathIndex && Mathf.Abs(e.pixelIndex - pixelIndex) <= tolerance)
                { matched = true; break; }
            if (matched)
            {
                foreach (var e in group)
                {
                    if (e.pathIndex == pathIndex) continue;

                    //iki uç arasında kırık yol var mı kontrol et
                    if (brokenRoadTiles.Count > 0)
                    {
                        Vector2Int myTile = allPaths[pathIndex][Mathf.Clamp(pixelIndex, 0, allPaths[pathIndex].Count - 1)];
                        Vector2Int otherTile = allPaths[e.pathIndex][Mathf.Clamp(e.pixelIndex, 0, allPaths[e.pathIndex].Count - 1)];

                        bool blocked = false;
                        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2Int.Distance(myTile, otherTile)));
                        for (int s = 0; s <= steps; s++)
                        {
                            float t = (float)s / steps;
                            Vector2Int check = new Vector2Int(
                                Mathf.RoundToInt(Mathf.Lerp(myTile.x, otherTile.x, t)),
                                Mathf.RoundToInt(Mathf.Lerp(myTile.y, otherTile.y, t)));
                            if (brokenRoadTiles.Contains(check)) { blocked = true; break; }
                        }
                        if (blocked) continue;
                    }

                    results.Add(e);
                }
            }
        }
        return results;
    }

    // -------------------------------------------------------------------------
    // PUBLIC API
    // -------------------------------------------------------------------------

    public void StopTraffic()
    {
        active = false;
        foreach (var car in activeCars) if (car.go != null) car.go.SetActive(false);
    }

    public void ResumeTraffic()
    {
        if (activeCars.Count == 0) return;
        foreach (var car in activeCars) if (car.go != null) car.go.SetActive(true);
        active = true;
    }

    public void Reinitialize() { StopTraffic(); Initialize(); }

    public void SetTrafficVisible(bool visible)
    {
        if (visible) ResumeTraffic();
        else StopTraffic();
    }
}