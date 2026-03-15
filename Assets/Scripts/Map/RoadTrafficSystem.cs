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
    [Header("References")]
    public MapPainter mapPainter;
    public RoadGenerator roadGenerator;

    [Header("Car Sprite")]
    [Tooltip("Your car sprite. Should point UPWARD (local +Y) in the source image.")]
    public Sprite carSprite;
    public string carSortingLayer = "Default";
    public int carSortingOrder = 20;
    [Tooltip("Pixels per unit — must match MapPainter (default 100).")]
    public float pixelsPerUnit = 100f;

    [Header("Spawn Rates")]
    [Range(0.5f, 5f)] public float highwayCarDensity = 2f;
    [Range(0.1f, 3f)] public float branchCarDensity = 0.8f;

    [Header("Car Appearance")]
    [Range(0.01f, 2f)] public float carScale = 0.05f;
    [Range(0f, 0.3f)] public float carScaleVariation = 0.1f;

    [Header("Car Colors")]
    public List<CarColorEntry> carColors = new List<CarColorEntry>();

    [Header("Speed")]
    [Tooltip("Car speed in world units per second.")]
    [Range(0.01f, 5f)] public float baseSpeed = 0.5f;
    [Range(0f, 0.5f)] public float speedVariation = 0.2f;

    [Header("Rotation")]
    [Tooltip("How many pixels ahead/behind to sample for direction.")]
    [Range(1, 40)] public int directionLookahead = 10;
    [Tooltip("How quickly the direction smooths out. Lower = smoother, higher = snappier.")]
    [Range(1f, 30f)] public float directionSmoothSpeed = 10f;
    [Tooltip("Turning speed for 180 flips at path ends and junction switches. 0 = instant snap.")]
    [Range(0f, 720f)] public float flipRotationSpeed = 0f;

    // -------------------------------------------------------------------------

    private struct JunctionEntry
    {
        public int pathIndex;
        public int pixelIndex;
    }

    private class Car
    {
        public int   pathIndex;
        public float position;        // fractional index along path
        public float speedInPixels;   // path-pixels per second
        public Color color;
        public float scaleFactor;
        public GameObject     go;
        public SpriteRenderer sr;
        public Quaternion     currentRotation;
        public Vector2        smoothedDir;
        public Vector3        smoothedWorldPos; // interpolated world position
    }

    private List<List<Vector2Int>> allPaths   = new List<List<Vector2Int>>();
    private List<Car>              activeCars = new List<Car>();
    private Queue<GameObject>      pool       = new Queue<GameObject>();
    private int                    highwayPathCount;
    private List<List<JunctionEntry>> junctionGroups = new List<List<JunctionEntry>>();
    private Transform carParent;
    private float mapHalfW;
    private float mapHalfH;
    private float worldUnitsPerPixel;
    private bool  active = false;

    // -------------------------------------------------------------------------

    void OnEnable()  { RoadGenerator.OnRoadsGenerated += HandleRoadsGenerated; }
    void OnDisable() { RoadGenerator.OnRoadsGenerated -= HandleRoadsGenerated; }

    void HandleRoadsGenerated() { StartCoroutine(InitAfterFrame()); }

    IEnumerator InitAfterFrame()
    {
        yield return null;
        Initialize();
    }

    // -------------------------------------------------------------------------

    void Initialize()
    {
        if (roadGenerator == null || mapPainter == null)
        {
            Debug.LogError("RoadTrafficSystem: mapPainter or roadGenerator not assigned.");
            return;
        }

        if (carSprite == null)
        {
            Debug.LogError("RoadTrafficSystem: carSprite not assigned.");
            return;
        }

        StopTraffic();
        ReturnAllCarsToPool();

        if (carParent == null)
        {
            GameObject container = new GameObject("CarTraffic");
            container.transform.SetParent(transform);
            carParent = container.transform;
        }

        MapGenerator mapGen = mapPainter.mapGenerator;
        mapHalfW = mapGen.width  * 0.5f / pixelsPerUnit;
        mapHalfH = mapGen.height * 0.5f / pixelsPerUnit;
        worldUnitsPerPixel = 1f / pixelsPerUnit;

        allPaths.Clear();
        activeCars.Clear();

        var highways = roadGenerator.GetHighwaySegments();
        if (highways != null)
            foreach (var seg in highways)
                if (seg != null && seg.Count >= 10)
                    allPaths.Add(seg);

        highwayPathCount = allPaths.Count;

        var branches = roadGenerator.GetBranchPaths();
        if (branches != null)
            foreach (var seg in branches)
                if (seg != null && seg.Count >= 10)
                    allPaths.Add(seg);

        BuildJunctions();
        SpawnCars();

        active = true;
    }

    void SpawnCars()
    {
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

                int idx = Mathf.Clamp(Mathf.RoundToInt(pos), 0, allPaths[p].Count - 1);
                Vector2 initialDir   = GetDirectionAtIndex(p, idx, pixelSpd);
                Vector3 initialWorld = InterpolatedWorldPos(p, pos);

                Car car = new Car
                {
                    pathIndex        = p,
                    position         = pos,
                    speedInPixels    = pixelSpd,
                    color            = col,
                    scaleFactor      = scale,
                    smoothedDir      = initialDir,
                    currentRotation  = DirToRotation(initialDir),
                    smoothedWorldPos = initialWorld
                };

                car.go = GetFromPool();
                car.sr = car.go.GetComponent<SpriteRenderer>();
                car.sr.sprite           = carSprite;
                car.sr.color            = col;
                car.sr.sortingLayerName = carSortingLayer;
                car.sr.sortingOrder     = carSortingOrder;
                car.go.transform.localScale = new Vector3(scale, scale, 1f);
                car.go.transform.position   = initialWorld;
                car.go.transform.rotation   = car.currentRotation;
                car.go.SetActive(true);

                activeCars.Add(car);
            }
        }
    }

    Color PickCarColor()
    {
        if (carColors == null || carColors.Count == 0) return Color.white;

        float totalWeight = 0f;
        foreach (var entry in carColors) totalWeight += Mathf.Max(0f, entry.spawnWeight);
        if (totalWeight <= 0f) return carColors[0].color;

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        foreach (var entry in carColors)
        {
            cumulative += Mathf.Max(0f, entry.spawnWeight);
            if (roll <= cumulative) return entry.color;
        }

        return carColors[carColors.Count - 1].color;
    }

    // -------------------------------------------------------------------------

    void Update()
    {
        if (!active || activeCars.Count == 0) return;

        float dt = Time.deltaTime;

        for (int i = 0; i < activeCars.Count; i++)
        {
            Car car  = activeCars[i];
            List<Vector2Int> path = allPaths[car.pathIndex];

            car.position += car.speedInPixels * dt;

            bool didFlip = false;

            // Mid-path junction
            int currentIdx = Mathf.Clamp(Mathf.RoundToInt(car.position), 0, path.Count - 1);
            if (car.pathIndex < highwayPathCount && Random.value < 0.002f)
            {
                var midConnections = FindConnections(car.pathIndex, currentIdx, 5);
                if (midConnections.Count > 0)
                {
                    JunctionEntry target = midConnections[Random.Range(0, midConnections.Count)];
                    Vector2Int currentPx = path[currentIdx];

                    car.pathIndex     = target.pathIndex;
                    car.position      = FindNearestPixelOnPath(allPaths[car.pathIndex], currentPx);
                    path              = allPaths[car.pathIndex];

                    int newLen = path.Count;
                    if (car.position < newLen * 0.3f)      car.speedInPixels =  Mathf.Abs(car.speedInPixels);
                    else if (car.position > newLen * 0.7f) car.speedInPixels = -Mathf.Abs(car.speedInPixels);

                    didFlip = true;
                }
            }

            int pathLen       = path.Count;
            bool reachedEnd   = car.position >= pathLen - 1;
            bool reachedStart = car.position < 0;

            if (reachedEnd || reachedStart)
            {
                int endIdx = reachedStart ? 0 : pathLen - 1;
                var connections = FindConnections(car.pathIndex, endIdx, 3);

                bool  isOnHighway = car.pathIndex < highwayPathCount;
                float turnChance  = isOnHighway ? 0.8f : 0.5f;

                if (connections.Count > 0 && Random.value < turnChance)
                {
                    JunctionEntry target = connections[Random.Range(0, connections.Count)];
                    Vector2Int currentPx = path[endIdx];

                    car.pathIndex     = target.pathIndex;
                    car.position      = FindNearestPixelOnPath(allPaths[car.pathIndex], currentPx);
                    path              = allPaths[car.pathIndex];

                    int newLen = allPaths[car.pathIndex].Count;
                    if (car.position < newLen * 0.3f)      car.speedInPixels =  Mathf.Abs(car.speedInPixels);
                    else if (car.position > newLen * 0.7f) car.speedInPixels = -Mathf.Abs(car.speedInPixels);
                }
                else
                {
                    if (reachedEnd)   { car.position = pathLen - 1; car.speedInPixels = -Mathf.Abs(car.speedInPixels); }
                    else              { car.position = 0;           car.speedInPixels =  Mathf.Abs(car.speedInPixels); }
                }

                didFlip = true;
            }

            // Sub-pixel interpolated world position
            Vector3 targetWorldPos = InterpolatedWorldPos(car.pathIndex, car.position);

            if (didFlip)
            {
                // Snap position on flip so there's no lerp lag across a junction
                car.smoothedWorldPos = targetWorldPos;
            }
            else
            {
                // Smooth world position to eliminate per-pixel stepping jitter
                car.smoothedWorldPos = Vector3.Lerp(car.smoothedWorldPos, targetWorldPos, dt * directionSmoothSpeed * 2f);
            }

            car.go.transform.position = car.smoothedWorldPos;

            // Direction smoothing
            int finalIdx = Mathf.Clamp(Mathf.RoundToInt(car.position), 0, path.Count - 1);
            Vector2 rawDir = GetDirectionAtIndex(car.pathIndex, finalIdx, car.speedInPixels);

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

    /// <summary>
    /// Linearly interpolates world position between floor and ceil path indices
    /// using the fractional part of position, giving smooth sub-pixel movement.
    /// </summary>
    Vector3 InterpolatedWorldPos(int pathIndex, float position)
    {
        List<Vector2Int> path = allPaths[pathIndex];
        int count = path.Count;

        int   idxA = Mathf.Clamp(Mathf.FloorToInt(position), 0, count - 1);
        int   idxB = Mathf.Clamp(idxA + 1,                   0, count - 1);
        float t    = position - Mathf.FloorToInt(position);

        Vector3 worldA = TileToWorld(path[idxA]);
        Vector3 worldB = TileToWorld(path[idxB]);

        return Vector3.Lerp(worldA, worldB, t);
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

        int from = Mathf.Max(0, bestIdx - step);
        int to   = Mathf.Min(path.Count - 1, bestIdx + step);
        for (int i = from; i <= to; i++)
        {
            float d = Vector2Int.Distance(fromPixel, path[i]);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }

        return bestIdx;
    }

    Vector2 GetDirectionAtIndex(int pathIndex, int idx, float speed)
    {
        List<Vector2Int> path = allPaths[pathIndex];
        int count = path.Count;

        int ahead = speed >= 0 ? directionLookahead : -directionLookahead;

        int aheadIdx  = Mathf.Clamp(idx + ahead,  0, count - 1);
        int behindIdx = Mathf.Clamp(idx - ahead, 0, count - 1);

        if (aheadIdx == behindIdx) return Vector2.up;

        Vector2 dir = new Vector2(
            path[aheadIdx].x - path[behindIdx].x,
            path[aheadIdx].y - path[behindIdx].y
        ).normalized;

        return dir == Vector2.zero ? Vector2.up : dir;
    }

    Quaternion DirToRotation(Vector2 dir)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        return Quaternion.Euler(0f, 0f, angle);
    }

    Vector3 TileToWorld(Vector2Int tile)
    {
        SpriteRenderer sr = mapPainter.mapRenderer;
        Vector3 anchor = sr != null ? sr.transform.position : Vector3.zero;

        float wx = anchor.x + (tile.x / pixelsPerUnit) - mapHalfW;
        float wy = anchor.y + (tile.y / pixelsPerUnit) - mapHalfH;
        return new Vector3(wx, wy, anchor.z - 1f);
    }

    // -------------------------------------------------------------------------

    GameObject GetFromPool()
    {
        while (pool.Count > 0)
        {
            GameObject go = pool.Dequeue();
            if (go != null) return go;
        }

        GameObject newGo = new GameObject("Car");
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
        foreach (var car in activeCars)
            ReturnToPool(car);
        activeCars.Clear();
    }

    // -------------------------------------------------------------------------

    void BuildJunctions()
    {
        junctionGroups.Clear();
        float junctionRadius = 8f;

        for (int p = 0; p < allPaths.Count; p++)
        {
            for (int endIdx = 0; endIdx < 2; endIdx++)
            {
                int myPixelIdx = (endIdx == 0) ? 0 : allPaths[p].Count - 1;
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
        }

        Debug.Log($"RoadTrafficSystem: {junctionGroups.Count} junction connections found.");
    }

    List<JunctionEntry> FindConnections(int pathIndex, int pixelIndex, int tolerance)
    {
        var results = new List<JunctionEntry>();
        for (int g = 0; g < junctionGroups.Count; g++)
        {
            var group    = junctionGroups[g];
            bool matched = false;
            for (int e = 0; e < group.Count; e++)
            {
                if (group[e].pathIndex == pathIndex &&
                    Mathf.Abs(group[e].pixelIndex - pixelIndex) <= tolerance)
                { matched = true; break; }
            }

            if (matched)
                for (int o = 0; o < group.Count; o++)
                    if (group[o].pathIndex != pathIndex)
                        results.Add(group[o]);
        }
        return results;
    }

    // -------------------------------------------------------------------------

    public void StopTraffic()
    {
        active = false;
        foreach (var car in activeCars)
            if (car.go != null) car.go.SetActive(false);
    }

    public void ResumeTraffic()
    {
        if (activeCars.Count == 0) return;
        foreach (var car in activeCars)
            if (car.go != null) car.go.SetActive(true);
        active = true;
    }

    public void Reinitialize()
    {
        StopTraffic();
        Initialize();
    }

    public void SetTrafficVisible(bool visible)
    {
        if (visible) ResumeTraffic();
        else StopTraffic();
    }
}