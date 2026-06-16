using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Ship subsystem: navigation grid, spawn/depart state machine, movement,
// bow-wave foam, docking helpers, and A* pathfinding on the nav grid.
public partial class MapDecorPlacer
{
    // Downsampled water navigation grid for pathfinding
    private bool[,] navGrid;       // true = navigable water
    private int[,]  navLandDist;   // per-cell minimum land distance (for cost biasing)
    private int     navW, navH;    // dimensions of the nav grid

    // Asenkron A* pathfinding — ana thread'i bloklamadan yol hesaplama
    private Coroutine spawnCoroutine;     // aktif spawn coroutine'i (aynı anda tek)
    private Coroutine departCoroutine;    // aktif ayrılış coroutine'i
    private bool      isSpawnPathPending; // spawn için yol hesaplanıyor mu
    private bool      isDepartPathPending; // ayrılış için yol hesaplanıyor mu

    // A* hesaplama sırasında frame başına işlenecek maksimum iterasyon
    private const int ASTAR_ITERATIONS_PER_FRAME = 200;

    // =========================================================================
    // SHIP NAVIGATION GRID (downsampled)
    // =========================================================================

    /// <summary>
    /// Builds a coarse navigation grid for A* pathfinding.
    /// Each nav cell is shipPathGridStep × shipPathGridStep tiles.
    /// A cell is navigable only if EVERY tile in the cell is water AND
    /// every tile has at least shipLandClearance distance from any land.
    /// Also stores per-cell minimum land distance for A* cost biasing
    /// so ships strongly prefer routes that keep well away from shore.
    /// </summary>
    void BuildNavGrid(MapGenerator map)
    {
        int step = Mathf.Max(1, shipPathGridStep);
        navW = Mathf.CeilToInt((float)map.width / step);
        navH = Mathf.CeilToInt((float)map.height / step);
        navGrid = new bool[navW, navH];
        navLandDist = new int[navW, navH]; // minimum land distance in each cell

        // Build full-resolution land distance field via BFS
        int maxBfsDist = shipLandClearance * 3 + step; // propagate well beyond clearance for cost biasing
        int[,] landDist = new int[map.width, map.height];
        Queue<Vector2Int> bfsQueue = new Queue<Vector2Int>();

        for (int x = 0; x < map.width; x++)
        for (int y = 0; y < map.height; y++)
        {
            if (map.IsLand(x, y))
            {
                landDist[x, y] = 0;
                bfsQueue.Enqueue(new Vector2Int(x, y));
            }
            else
            {
                landDist[x, y] = int.MaxValue;
            }
        }

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };
        while (bfsQueue.Count > 0)
        {
            var pos = bfsQueue.Dequeue();
            int d = landDist[pos.x, pos.y];
            if (d >= maxBfsDist) continue;
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) continue;
                if (landDist[nx, ny] <= d + 1) continue;
                landDist[nx, ny] = d + 1;
                bfsQueue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        // Build nav grid: check EVERY tile in the cell, not just center
        for (int gx = 0; gx < navW; gx++)
        for (int gy = 0; gy < navH; gy++)
        {
            int startX = gx * step;
            int startY = gy * step;
            int endX   = Mathf.Min(startX + step, map.width);
            int endY   = Mathf.Min(startY + step, map.height);

            bool passable = true;
            int minDist   = int.MaxValue;

            for (int tx = startX; tx < endX && passable; tx++)
            for (int ty = startY; ty < endY && passable; ty++)
            {
                if (map.IsLand(tx, ty))
                {
                    passable = false;
                    minDist  = 0;
                    break;
                }
                int d = landDist[tx, ty];
                if (d < minDist) minDist = d;
                if (d < shipLandClearance) passable = false;
            }

            navGrid[gx, gy]     = passable;
            navLandDist[gx, gy] = minDist;
        }

        // Corridor width enforcement: erode narrow passages.
        // A cell is only truly navigable if it has at least shipMinCorridorWidth
        // contiguous open cells in BOTH the X and Y axes (centered on itself).
        // This prevents ships from threading through 1-cell-wide gaps between
        // land masses or between land and the map edge.
        if (shipMinCorridorWidth > 1)
        {
            bool[,] eroded = new bool[navW, navH];
            int halfCorridor = shipMinCorridorWidth / 2;

            for (int gx = 0; gx < navW; gx++)
            for (int gy = 0; gy < navH; gy++)
            {
                if (!navGrid[gx, gy]) { eroded[gx, gy] = false; continue; }

                // Check horizontal span
                bool hOk = true;
                for (int dx = -halfCorridor; dx <= halfCorridor && hOk; dx++)
                {
                    int nx = gx + dx;
                    if (nx < 0 || nx >= navW || !navGrid[nx, gy]) hOk = false;
                }

                // Check vertical span
                bool vOk = true;
                for (int dy = -halfCorridor; dy <= halfCorridor && vOk; dy++)
                {
                    int ny = gy + dy;
                    if (ny < 0 || ny >= navH || !navGrid[gx, ny]) vOk = false;
                }

                eroded[gx, gy] = hOk && vOk;
            }

            navGrid = eroded;
        }
    }

    // =========================================================================
    // SHIP SPAWNING & UPDATE
    // =========================================================================

    void UpdateShips(float lightingRatio)
    {
        if (cachedMap == null || ports.Count == 0) return;
        if (shipSpritesDay == null || shipSpritesDay.Count == 0) return;

        // Spawn timer — asenkron A* ile spawn et
        shipSpawnTimer -= Time.deltaTime;
        if (shipSpawnTimer <= 0f)
        {
            shipSpawnTimer = shipSpawnInterval;
            if (activeShips.Count < maxActiveShips && !isSpawnPathPending)
            {
                if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
                spawnCoroutine = StartCoroutine(TrySpawnShipAsync());
            }
        }

        // Update each ship
        for (int i = activeShips.Count - 1; i >= 0; i--)
        {
            ShipInstance ship = activeShips[i];

            switch (ship.state)
            {
                case ShipState.Arriving:
                    MoveAlongPath(ship);
                    if (ship.pathIndex >= ship.path.Count)
                    {
                        ship.state     = ShipState.Waiting;
                        ship.waitTimer = Random.Range(shipWaitTimeRange.x, shipWaitTimeRange.y);
                    }
                    break;

                case ShipState.Waiting:
                    ship.waitTimer -= Time.deltaTime;
                    if (ship.waitTimer <= 0f && !isDepartPathPending)
                    {
                        // Ayrılış: asenkron A* ile yol hesapla
                        if (departCoroutine != null) StopCoroutine(departCoroutine);
                        departCoroutine = StartCoroutine(DepartShipAsync(ship));
                    }
                    break;

                case ShipState.Departing:
                    MoveAlongPath(ship);
                    if (ship.pathIndex >= ship.path.Count)
                        ship.state = ShipState.Done;
                    break;

                case ShipState.Done:
                    break;
            }

            // Cleanup finished ships
            if (ship.state == ShipState.Done)
            {
                decorObjects.Remove(ship.go);
                if (ship.go != null) Destroy(ship.go);
                activeShips.RemoveAt(i);
            }
        }

        //köpük sprite'larını güncelle
        UpdateFoamSprites();
    }

    // =========================================================================
    // BOW WAVE — geminin burun ucundan nokta nokta köpük
    // =========================================================================

    private List<FoamDot> foamDots = new List<FoamDot>();
    private Queue<GameObject> foamPool = new Queue<GameObject>();
    private Sprite foamDotSprite;

    Sprite GetFoamDotSprite()
    {
        if (foamDotSprite != null) return foamDotSprite;
        int size = 6;
        Texture2D tex = new Texture2D(size, size);
        float c = (size - 1) * 0.5f;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        foamDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return foamDotSprite;
    }

    void CreateBowWave(ShipInstance ship) { }

    void UpdateFoamSprites()
    {
        float dt = Time.deltaTime;

        //hareket eden gemilerden burun ucunda köpük dot'ları spawn
        for (int i = 0; i < activeShips.Count; i++)
        {
            ShipInstance ship = activeShips[i];
            if (ship.go == null) continue;
            if (ship.state != ShipState.Arriving && ship.state != ShipState.Departing) continue;

            ship.wakeTimer -= dt;
            if (ship.wakeTimer > 0f) continue;
            ship.wakeTimer = 0.015f;

            float bowAngle = (ship.currentAngle + 90f) * Mathf.Deg2Rad;
            Vector2 fwd = new Vector2(Mathf.Cos(bowAngle), Mathf.Sin(bowAngle));
            Vector2 right = new Vector2(-fwd.y, fwd.x);
            Vector3 shipPos = ship.go.transform.position;

            //burun ucunun iki kenarından dot spawn — yana doğru sürüklenecek
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 dotPos = shipPos
                    + new Vector3(fwd.x, fwd.y, 0f) * 0.15f * ship.scale
                    + new Vector3(right.x, right.y, 0f) * side * 0.055f * ship.scale;
                dotPos.z = shipPos.z - 0.05f; //geminin ALTINDA değil ÜSTÜNDE
                dotPos.x += Random.Range(-0.005f, 0.005f);
                dotPos.y += Random.Range(-0.005f, 0.005f);

                //yana doğru sürüklenme hızı
                Vector3 sideVel = new Vector3(right.x, right.y, 0f) * side * 0.08f;
                SpawnFoamDot(dotPos, sideVel);
            }
        }

        //dot'ları güncelle — yana sürüklen + fade out
        for (int i = foamDots.Count - 1; i >= 0; i--)
        {
            FoamDot fd = foamDots[i];
            fd.lifetime -= dt;

            if (fd.lifetime <= 0f)
            {
                fd.go.SetActive(false);
                foamPool.Enqueue(fd.go);
                foamDots.RemoveAt(i);
                continue;
            }

            //yana sürüklen
            fd.go.transform.position += fd.velocity * dt;
            fd.velocity *= (1f - dt * 2f); //yavaşla

            float t = 1f - (fd.lifetime / fd.maxLifetime);
            Color col = fd.sr.color;
            col.a = 0.7f * (1f - t);
            fd.sr.color = col;
        }
    }

    void SpawnFoamDot(Vector3 pos, Vector3 velocity)
    {
        GameObject go;
        SpriteRenderer sr;

        if (foamPool.Count > 0)
        {
            go = foamPool.Dequeue();
            go.SetActive(true);
            sr = go.GetComponent<SpriteRenderer>();
        }
        else
        {
            go = new GameObject("FoamDot");
            go.transform.SetParent(transform);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetFoamDotSprite();
            sr.sortingOrder = 7; //gemilerin altında, denizin üstünde
        }

        float s = Random.Range(0.08f, 0.13f);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(s, s, 1f);
        sr.color = new Color(1f, 1f, 1f, 0.7f);

        foamDots.Add(new FoamDot
        {
            go = go,
            sr = sr,
            lifetime = 1.5f,
            maxLifetime = 1.5f,
            velocity = velocity
        });
    }

    // =========================================================================
    // KÖPÜK İZİ (WAKE) — geminin değdiği yerlerde oluşan sabit köpük
    // =========================================================================

    void MoveAlongPath(ShipInstance ship)
    {
        if (ship.path == null || ship.path.Count < 2) { ship.pathIndex = ship.path != null ? ship.path.Count : 0; return; }

        float totalSegments = ship.path.Count - 1;
        float distThisFrame = ship.speed * Time.deltaTime;

        //yakındaki gemilere çarpmamak için yavaşla (her 10 frame'de kontrol)
        if (Time.frameCount % 10 == 0)
        {
            float minShipDist = 0.15f;
            ship.proximitySlowdown = 1f;
            for (int s = 0; s < activeShips.Count; s++)
            {
                ShipInstance other = activeShips[s];
                if (other == ship || other.go == null) continue;
                float d = Vector3.Distance(ship.go.transform.position, other.go.transform.position);
                if (d < minShipDist)
                {
                    ship.proximitySlowdown = Mathf.Clamp01(d / minShipDist);
                    break;
                }
            }
        }
        distThisFrame *= ship.proximitySlowdown;

        // Estimate segment length for t-step conversion
        while (distThisFrame > 0f && ship.pathIndex < totalSegments)
        {
            Vector3 segStart = ship.path[ship.pathIndex];
            Vector3 segEnd   = ship.path[ship.pathIndex + 1];
            float segLen     = Vector3.Distance(segStart, segEnd);
            if (segLen < 0.0001f) { ship.pathIndex++; ship.segmentT = 0f; continue; }

            float remainingInSeg = (1f - ship.segmentT) * segLen;
            if (distThisFrame < remainingInSeg)
            {
                ship.segmentT += distThisFrame / segLen;
                distThisFrame = 0f;
            }
            else
            {
                distThisFrame -= remainingInSeg;
                ship.pathIndex++;
                ship.segmentT = 0f;
            }
        }

        if (ship.pathIndex >= totalSegments)
        {
            ship.go.transform.position = ship.path[ship.path.Count - 1];
            ship.pathIndex = ship.path.Count; // signal done
            return;
        }

        //lineer interpolasyon + yumuşak dönüş
        Vector3 pos = Vector3.Lerp(ship.path[ship.pathIndex], ship.path[ship.pathIndex + 1], ship.segmentT);
        ship.go.transform.position = pos;

        //hareket yönüne göre yumuşak dönüş
        Vector3 dir = ship.path[ship.pathIndex + 1] - ship.path[ship.pathIndex];
        if (dir.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            ship.currentAngle = Mathf.MoveTowardsAngle(ship.currentAngle, targetAngle, shipTurnSpeed * Time.deltaTime);
            ship.go.transform.rotation = Quaternion.Euler(0f, 0f, ship.currentAngle);
        }
    }

    /// <summary>Catmull-Rom spline position at parameter t ∈ [0,1].</summary>
    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>Catmull-Rom spline tangent (derivative) at parameter t ∈ [0,1].</summary>
    Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        return 0.5f * (
            (-p0 + p2) +
            (4f * p0 - 10f * p1 + 8f * p2 - 2f * p3) * t +
            (-3f * p0 + 9f * p1 - 9f * p2 + 3f * p3) * t2);
    }

    void RotateShipToward(ShipInstance ship, Vector3 direction)
    {
        // Kept for initial facing setup; runtime rotation now uses smooth lerp in MoveAlongPath
        if (direction.sqrMagnitude < 0.0001f) return;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        ship.currentAngle = angle - 90f;
        ship.go.transform.rotation = Quaternion.Euler(0f, 0f, ship.currentAngle);
    }

    /// <summary>
    /// Asenkron gemi spawn — A* hesaplamasını birden fazla frame'e yayar.
    /// </summary>
    IEnumerator TrySpawnShipAsync()
    {
        isSpawnPathPending = true;

        if (ports.Count == 0) { isSpawnPathPending = false; yield break; }

        //aynı limana giden veya bekleyen başka gemi yoksa spawn et
        int portIdx = Random.Range(0, ports.Count);

        //bu limanda zaten gemi var mı kontrol et
        bool portOccupied = false;
        for (int s = 0; s < activeShips.Count; s++)
        {
            if (activeShips[s].portIndex == portIdx &&
                (activeShips[s].state == ShipState.Arriving || activeShips[s].state == ShipState.Waiting))
            {
                portOccupied = true;
                break;
            }
        }

        //meşgulse diğer limanı dene
        if (portOccupied)
        {
            portIdx = (portIdx + 1) % ports.Count;
            for (int s = 0; s < activeShips.Count; s++)
            {
                if (activeShips[s].portIndex == portIdx &&
                    (activeShips[s].state == ShipState.Arriving || activeShips[s].state == ShipState.Waiting))
                { isSpawnPathPending = false; yield break; } //iki liman da meşgul
            }
        }

        PortData port = ports[portIdx];

        Vector3 origin = GetRandomOceanEdgePoint();

        //dock noktası — limanın deniz tarafında güvenli mesafede
        Vector3 dockPoint = GetSafeDockPoint(port);

        //A* ile dock noktasına git — asenkron hesaplama
        List<Vector3> path = null;
        yield return StartCoroutine(FindShipPathAsync(origin, dockPoint, (result) => { path = result; }));

        if (path == null || path.Count == 0) { isSpawnPathPending = false; yield break; }

        // Coroutine sırasında max ship sayısı aşılmış olabilir
        if (activeShips.Count >= maxActiveShips) { isSpawnPathPending = false; yield break; }

        // Create ship GO
        int spriteIdx = Random.Range(0, shipSpritesDay.Count);
        Sprite daySprite = shipSpritesDay[spriteIdx];
        if (daySprite == null) { isSpawnPathPending = false; yield break; }

        float scale = Random.Range(shipScaleRange.x, shipScaleRange.y);
        float baseA = Random.Range(0.85f, 1f);
        int sortOrder = 8;

        GameObject go = new GameObject("Ship");
        go.transform.SetParent(transform);
        go.transform.position   = origin;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer daySR = go.AddComponent<SpriteRenderer>();
        daySR.sprite       = daySprite;
        daySR.sortingOrder = sortOrder;
        daySR.color        = new Color(1f, 1f, 1f, baseA);

        SpriteRenderer nightSR = null;
        if (shipSpritesNight != null && spriteIdx < shipSpritesNight.Count &&
            shipSpritesNight[spriteIdx] != null)
        {
            GameObject nightGo = new GameObject("ShipNight");
            nightGo.transform.SetParent(go.transform, false);
            // Gece sprite'ını gündüz tabanına hizala (ışık taşması bounding box'ı büyütür).
            Vector3 align = ComputeDayNightAlign(daySprite, shipSpritesNight[spriteIdx]);
            nightGo.transform.localPosition = new Vector3(align.x, align.y, 0f);
            nightGo.transform.localScale    = Vector3.one;
            nightGo.transform.localRotation = Quaternion.identity;

            nightSR              = nightGo.AddComponent<SpriteRenderer>();
            nightSR.sprite       = shipSpritesNight[spriteIdx];
            nightSR.sortingOrder = sortOrder + 1;
            nightSR.color        = new Color(1f, 1f, 1f, 0f);
        }

        // Apply current day/night ratio immediately — gunduz sabit opak, gece ustte fade.
        float ratio = (dayNight != null) ? dayNight.LightingRatio : 0f;
        daySR.color = new Color(1f, 1f, 1f, baseA);
        if (nightSR != null)
            nightSR.color = new Color(1f, 1f, 1f, baseA * ratio);

        // Set initial facing angle from first path direction
        float initAngle = 0f;
        if (path.Count > 1)
        {
            Vector3 dir = path[1] - path[0];
            if (dir.sqrMagnitude > 0.001f)
            {
                initAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, initAngle);
            }
        }

        decorObjects.Add(go);
        activeShips.Add(new ShipInstance
        {
            go            = go,
            dayRenderer   = daySR,
            nightRenderer = nightSR,
            baseAlpha     = baseA,
            scale         = scale,
            state         = ShipState.Arriving,
            path          = path,
            pathIndex     = 0,
            segmentT      = 0f,
            waitTimer     = 0f,
            portIndex     = portIdx,
            speed         = shipSpeed * Random.Range(0.8f, 1.2f),
            currentAngle  = initAngle
        });

        //bow wave köpük izi
        CreateBowWave(activeShips[activeShips.Count - 1]);

        isSpawnPathPending = false;
    }

    /// <summary>
    /// Asenkron gemi ayrılışı — A* hesaplamasını birden fazla frame'e yayar.
    /// </summary>
    IEnumerator DepartShipAsync(ShipInstance ship)
    {
        isDepartPathPending = true;

        PortData depPort = ports[ship.portIndex];
        Vector2 seaDir = depPort.seaDirection;

        //deniz yönünde güvenli bir noktaya çık
        Vector3 seaPoint = ship.go.transform.position + new Vector3(seaDir.x, seaDir.y, 0f) * 0.5f;
        seaPoint = PushToWater(seaPoint, seaDir);

        Vector3 exitPoint = GetRandomOceanEdgePoint();

        //A* ile mevcut konumdan okyanus kenarına — asenkron
        Vector3 shipPos = ship.go.transform.position;
        List<Vector3> toExit = null;
        yield return StartCoroutine(FindShipPathAsync(shipPos, exitPoint, (result) => { toExit = result; }));

        // Gemi beklerken yok edilmiş olabilir
        if (ship.go == null || ship.state != ShipState.Waiting) { isDepartPathPending = false; yield break; }

        List<Vector3> depPath = new List<Vector3>();
        if (toExit != null && toExit.Count > 0)
            depPath.AddRange(toExit);

        if (depPath.Count > 0)
        {
            ship.path      = depPath;
            ship.pathIndex = 0;
            ship.segmentT  = 0f;
            ship.state     = ShipState.Departing;
        }
        else
        {
            ship.state = ShipState.Done;
        }

        isDepartPathPending = false;
    }

    /// <summary>
    /// Returns a random world-space point on the ocean edge of the map.
    /// </summary>
    Vector3 GetRandomOceanEdgePoint()
    {
        float margin = 0.1f; // small offset inside the map boundary
        int side = Random.Range(0, 4);
        float wx, wy;

        switch (side)
        {
            case 0: // left
                wx = transform.position.x - cachedHalfW - margin;
                wy = transform.position.y + Random.Range(-cachedHalfH * 0.8f, cachedHalfH * 0.8f);
                break;
            case 1: // right
                wx = transform.position.x + cachedHalfW + margin;
                wy = transform.position.y + Random.Range(-cachedHalfH * 0.8f, cachedHalfH * 0.8f);
                break;
            case 2: // bottom
                wx = transform.position.x + Random.Range(-cachedHalfW * 0.8f, cachedHalfW * 0.8f);
                wy = transform.position.y - cachedHalfH - margin;
                break;
            default: // top
                wx = transform.position.x + Random.Range(-cachedHalfW * 0.8f, cachedHalfW * 0.8f);
                wy = transform.position.y + cachedHalfH + margin;
                break;
        }

        return new Vector3(wx, wy, spriteZ);
    }

    /// <summary>
    /// Geminin limana yanaşması için 3 waypoint döner:
    /// 1. Manevra noktası — denizde, kıyıdan uzakta, geniş dönüş için
    /// 2. Hizalama noktası — kıyıya paralel hizalanma başlangıcı
    /// 3. Yanaşma noktası — limanın hemen yanında, kıyıya paralel
    /// </summary>
    /// <summary>
    /// Yer şekillerine bakarak en uygun yaklaşma rotasını belirler.
    /// Kıyının her iki yönünü de dener, açık su olan tarafı seçer.
    /// dockPoint: geminin yanaşacağı son nokta (kıyıya paralel)
    /// approachPoint: geminin A* ile ulaşacağı yaklaşma noktası (dock'tan kıyı boyunca uzakta)
    /// </summary>
    bool FindBestApproach(PortData port, out Vector3 dockPoint, out Vector3 approachPoint)
    {
        dockPoint = GetSafeDockPoint(port);
        approachPoint = dockPoint;

        Vector2 shore = port.shoreDirection;
        float approachDist = 0.8f;

        //her iki yönü de dene — hangisi açık su ise oradan yaklaş
        Vector3 candidateA = dockPoint + new Vector3(shore.x, shore.y, 0f) * approachDist;
        Vector3 candidateB = dockPoint + new Vector3(-shore.x, -shore.y, 0f) * approachDist;

        bool aIsWater = IsPointInWater(candidateA) && !SegmentCrossesLand(dockPoint, candidateA);
        bool bIsWater = IsPointInWater(candidateB) && !SegmentCrossesLand(dockPoint, candidateB);

        if (aIsWater && bIsWater)
        {
            //ikisi de açık — rastgele seç
            approachPoint = Random.value > 0.5f ? candidateA : candidateB;
            return true;
        }
        else if (aIsWater)
        {
            approachPoint = candidateA;
            return true;
        }
        else if (bIsWater)
        {
            approachPoint = candidateB;
            return true;
        }

        //her iki yön de karaya çarpıyor — daha kısa mesafe dene
        float shortDist = 0.4f;
        candidateA = dockPoint + new Vector3(shore.x, shore.y, 0f) * shortDist;
        candidateB = dockPoint + new Vector3(-shore.x, -shore.y, 0f) * shortDist;

        aIsWater = IsPointInWater(candidateA) && !SegmentCrossesLand(dockPoint, candidateA);
        bIsWater = IsPointInWater(candidateB) && !SegmentCrossesLand(dockPoint, candidateB);

        if (aIsWater)
        {
            approachPoint = candidateA;
            return true;
        }
        if (bIsWater)
        {
            approachPoint = candidateB;
            return true;
        }

        return false; //hiçbir yön uygun değil
    }

    /// <summary>
    /// Verilen world pozisyonun suda olup olmadığını kontrol eder.
    /// </summary>
    bool IsPointInWater(Vector3 worldPos)
    {
        if (cachedMap == null) return true;
        int tileX = Mathf.RoundToInt((worldPos.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
        int tileY = Mathf.RoundToInt((worldPos.y - transform.position.y + cachedHalfH) * pixelsPerUnit);
        if (tileX < 0 || tileX >= cachedMap.width || tileY < 0 || tileY >= cachedMap.height) return true;
        return !cachedMap.IsLand(tileX, tileY);
    }

    /// <summary>
    /// Limanın deniz tarafında, karadan güvenli mesafede bir dock noktası bulur.
    /// Deniz yönünde adım adım ilerleyip su olan en yakın noktayı döner.
    /// </summary>
    Vector3 GetSafeDockPoint(PortData port)
    {
        Vector3 pos = port.worldPos;
        Vector2 sea = port.seaDirection;

        //deniz yönünde adım adım ilerle, suya ulaşana kadar
        for (int i = 1; i <= 30; i++)
        {
            Vector3 candidate = pos + new Vector3(sea.x, sea.y, 0f) * (i * 0.05f);
            int tileX = Mathf.RoundToInt((candidate.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
            int tileY = Mathf.RoundToInt((candidate.y - transform.position.y + cachedHalfH) * pixelsPerUnit);

            if (cachedMap != null && tileX >= 0 && tileX < cachedMap.width && tileY >= 0 && tileY < cachedMap.height
                && !cachedMap.IsLand(tileX, tileY))
            {
                //suya ulaştıktan sonra biraz daha aç
                return candidate + new Vector3(sea.x, sea.y, 0f) * 0.15f;
            }
        }

        //fallback
        return pos + new Vector3(sea.x, sea.y, 0f) * 0.3f;
    }

    List<Vector3> GetDockingWaypoints(PortData port)
    {
        Vector3 portW = port.worldPos;
        Vector2 sea = port.seaDirection;
        Vector2 shore = port.shoreDirection;

        //rastgele sağdan veya soldan yanaşsın
        if (Random.value > 0.5f) shore = -shore;

        float dockOffset = 0.15f;
        float alignDist = 0.8f;
        float maneuverDist = 0.6f;

        Vector3 dockPoint = portW + new Vector3(sea.x, sea.y, 0f) * dockOffset;
        Vector3 alignPoint = dockPoint + new Vector3(shore.x, shore.y, 0f) * alignDist;
        Vector3 maneuverPoint = alignPoint + new Vector3(sea.x, sea.y, 0f) * maneuverDist;

        //karaya düşen waypoint'leri denize it
        alignPoint = PushToWater(alignPoint, sea);
        maneuverPoint = PushToWater(maneuverPoint, sea);

        return new List<Vector3> { maneuverPoint, alignPoint, dockPoint };
    }

    /// <summary>
    /// Geminin ayrılması için 3 waypoint döner (yanaşmanın tersi):
    /// Limandan kıyıya paralel ayrılıp denize açılır.
    /// </summary>
    List<Vector3> GetUndockingWaypoints(PortData port)
    {
        Vector3 portW = port.worldPos;
        Vector2 sea = port.seaDirection;
        Vector2 shore = port.shoreDirection;

        if (Random.value > 0.5f) shore = -shore;

        float dockOffset = 0.15f;
        float departDist = 0.8f;
        float seaDist = 0.6f;

        Vector3 dockPoint = portW + new Vector3(sea.x, sea.y, 0f) * dockOffset;
        Vector3 departPoint = dockPoint + new Vector3(shore.x, shore.y, 0f) * departDist;
        Vector3 seaPoint = departPoint + new Vector3(sea.x, sea.y, 0f) * seaDist;

        //karaya düşen waypoint'leri denize it
        departPoint = PushToWater(departPoint, sea);
        seaPoint = PushToWater(seaPoint, sea);

        return new List<Vector3> { dockPoint, departPoint, seaPoint };
    }

    /// <summary>
    /// Eğer verilen world pozisyon karaya düşüyorsa, deniz yönünde iterek suya taşır.
    /// </summary>
    Vector3 PushToWater(Vector3 worldPos, Vector2 seaDir)
    {
        if (cachedMap == null) return worldPos;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            int tileX = Mathf.RoundToInt((worldPos.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
            int tileY = Mathf.RoundToInt((worldPos.y - transform.position.y + cachedHalfH) * pixelsPerUnit);

            if (tileX < 0 || tileX >= cachedMap.width || tileY < 0 || tileY >= cachedMap.height)
                return worldPos;

            if (!cachedMap.IsLand(tileX, tileY))
                return worldPos;

            worldPos += new Vector3(seaDir.x, seaDir.y, 0f) * 0.1f;
        }

        Debug.LogWarning("MapDecorPlacer: PushToWater başarısız — waypoint hâlâ karada!");
        return worldPos;
    }

    // =========================================================================
    // A* PATHFINDING ON NAV GRID
    // =========================================================================

    /// <summary>
    /// Finds a path in world-space from 'from' to 'to', navigating around the island.
    /// Uses A* on the downsampled navGrid, simplifies collinear points, then applies
    /// Chaikin corner-cutting subdivision to produce smooth, round sailing curves.
    /// </summary>
    List<Vector3> FindShipPath(Vector3 from, Vector3 to)
    {
        if (cachedMap == null || navGrid == null) return null;

        int step = Mathf.Max(1, shipPathGridStep);

        // Convert world → tile → nav grid coords
        Vector2Int fromTile = WorldToTile(from);
        Vector2Int toTile   = WorldToTile(to);

        int fromGx = Mathf.Clamp(fromTile.x / step, 0, navW - 1);
        int fromGy = Mathf.Clamp(fromTile.y / step, 0, navH - 1);
        int toGx   = Mathf.Clamp(toTile.x / step, 0, navW - 1);
        int toGy   = Mathf.Clamp(toTile.y / step, 0, navH - 1);

        // Snap start/end to nearest navigable cell if they're on land
        fromGx = FindNearestNavigable(fromGx, fromGy, out fromGy);
        int tempToGy = toGy;
        toGx   = FindNearestNavigable(toGx, tempToGy, out toGy);

        if (fromGx < 0 || toGx < 0) return null;

        // A* search
        List<Vector2Int> gridPath = AStarSearch(fromGx, fromGy, toGx, toGy);
        if (gridPath == null || gridPath.Count == 0) return null;

        // Convert grid path → world positions
        List<Vector3> worldPath = new List<Vector3>();
        worldPath.Add(from);

        for (int i = 0; i < gridPath.Count; i++)
        {
            int tileX = gridPath[i].x * step + step / 2;
            int tileY = gridPath[i].y * step + step / 2;
            tileX = Mathf.Clamp(tileX, 0, cachedMap.width - 1);
            tileY = Mathf.Clamp(tileY, 0, cachedMap.height - 1);

            float wx = transform.position.x + (tileX / pixelsPerUnit) - cachedHalfW;
            float wy = transform.position.y + (tileY / pixelsPerUnit) - cachedHalfH;
            worldPath.Add(new Vector3(wx, wy, spriteZ));
        }

        worldPath.Add(to);

        // 1. Simplify: remove collinear points
        worldPath = SimplifyPath(worldPath);

        // Chaikin smoothing devre dışı — karadan geçen rotalar yaratıyordu.
        // CatmullRom spline hareket sırasında yeterli yumuşaklık sağlıyor.

        return worldPath;
    }

    /// <summary>
    /// FindShipPath'in asenkron versiyonu — A* hesaplamasını frame'lere yayar.
    /// Sonucu callback ile döner.
    /// </summary>
    IEnumerator FindShipPathAsync(Vector3 from, Vector3 to, System.Action<List<Vector3>> callback)
    {
        if (cachedMap == null || navGrid == null) { callback(null); yield break; }

        int step = Mathf.Max(1, shipPathGridStep);

        // Convert world → tile → nav grid coords
        Vector2Int fromTile = WorldToTile(from);
        Vector2Int toTile   = WorldToTile(to);

        int fromGx = Mathf.Clamp(fromTile.x / step, 0, navW - 1);
        int fromGy = Mathf.Clamp(fromTile.y / step, 0, navH - 1);
        int toGx   = Mathf.Clamp(toTile.x / step, 0, navW - 1);
        int toGy   = Mathf.Clamp(toTile.y / step, 0, navH - 1);

        // Snap start/end to nearest navigable cell if they're on land
        fromGx = FindNearestNavigable(fromGx, fromGy, out fromGy);
        int tempToGy = toGy;
        toGx   = FindNearestNavigable(toGx, tempToGy, out toGy);

        if (fromGx < 0 || toGx < 0) { callback(null); yield break; }

        // A* search — asenkron
        List<Vector2Int> gridPath = null;
        yield return StartCoroutine(AStarSearchAsync(fromGx, fromGy, toGx, toGy, (result) => { gridPath = result; }));

        if (gridPath == null || gridPath.Count == 0) { callback(null); yield break; }

        // Convert grid path → world positions
        List<Vector3> worldPath = new List<Vector3>();
        worldPath.Add(from);

        for (int i = 0; i < gridPath.Count; i++)
        {
            int tileX = gridPath[i].x * step + step / 2;
            int tileY = gridPath[i].y * step + step / 2;
            tileX = Mathf.Clamp(tileX, 0, cachedMap.width - 1);
            tileY = Mathf.Clamp(tileY, 0, cachedMap.height - 1);

            float wx = transform.position.x + (tileX / pixelsPerUnit) - cachedHalfW;
            float wy = transform.position.y + (tileY / pixelsPerUnit) - cachedHalfH;
            worldPath.Add(new Vector3(wx, wy, spriteZ));
        }

        worldPath.Add(to);

        // Simplify: remove collinear points
        worldPath = SimplifyPath(worldPath);

        callback(worldPath);
    }

    /// <summary>
    /// A* aramasının asenkron versiyonu — frame başına ASTAR_ITERATIONS_PER_FRAME iterasyon işler.
    /// </summary>
    IEnumerator AStarSearchAsync(int sx, int sy, int ex, int ey, System.Action<List<Vector2Int>> callback)
    {
        // Directions: 8-connected for smoother paths
        int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        float[] cost8 = { 1f, 1f, 1f, 1f, 1.414f, 1.414f, 1.414f, 1.414f };

        int maxIterations = navW * navH; // safety cap
        float[,] gScore = new float[navW, navH];
        int[,] cameFromX = new int[navW, navH];
        int[,] cameFromY = new int[navW, navH];
        bool[,] closed   = new bool[navW, navH];

        for (int x = 0; x < navW; x++)
        for (int y = 0; y < navH; y++)
        {
            gScore[x, y] = float.MaxValue;
            cameFromX[x, y] = -1;
            cameFromY[x, y] = -1;
        }

        gScore[sx, sy] = 0f;

        var open = new SortedList<float, Vector2Int>(new DuplicateKeyComparer());
        float h0 = Heuristic(sx, sy, ex, ey);
        open.Add(h0, new Vector2Int(sx, sy));

        int iterations = 0;
        int frameIterations = 0;

        while (open.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            frameIterations++;

            // Frame başına iterasyon limitine ulaşınca sonraki frame'e geç
            if (frameIterations >= ASTAR_ITERATIONS_PER_FRAME)
            {
                frameIterations = 0;
                yield return null;
            }

            var current = open.Values[0];
            open.RemoveAt(0);

            int cx = current.x, cy = current.y;
            if (cx == ex && cy == ey)
            {
                callback(ReconstructPath(cameFromX, cameFromY, sx, sy, ex, ey));
                yield break;
            }

            if (closed[cx, cy]) continue;
            closed[cx, cy] = true;

            for (int i = 0; i < 8; i++)
            {
                int nx = cx + dx8[i], ny = cy + dy8[i];
                if (nx < 0 || nx >= navW || ny < 0 || ny >= navH) continue;
                if (!navGrid[nx, ny] || closed[nx, ny]) continue;

                // For diagonals, ensure both adjacent axis-aligned cells are navigable
                if (i >= 4)
                {
                    if (!navGrid[cx + dx8[i], cy] || !navGrid[cx, cy + dy8[i]])
                        continue;
                }

                float tentG = gScore[cx, cy] + cost8[i];

                // Shore avoidance: cells closer to land are penalized
                if (shipShoreAvoidanceWeight > 0f && navLandDist != null)
                {
                    int ld = navLandDist[nx, ny];
                    if (ld < shipLandClearance * 3)
                    {
                        float proximity = 1f - ((float)ld / (shipLandClearance * 3));
                        tentG += proximity * proximity * shipShoreAvoidanceWeight * cost8[i];
                    }
                }

                if (tentG >= gScore[nx, ny]) continue;

                gScore[nx, ny]    = tentG;
                cameFromX[nx, ny] = cx;
                cameFromY[nx, ny] = cy;
                float f = tentG + Heuristic(nx, ny, ex, ey);
                open.Add(f, new Vector2Int(nx, ny));
            }
        }

        callback(null); // no path found
    }

    Vector2Int WorldToTile(Vector3 worldPos)
    {
        float localX = worldPos.x - transform.position.x + cachedHalfW;
        float localY = worldPos.y - transform.position.y + cachedHalfH;
        int tx = Mathf.RoundToInt(localX * pixelsPerUnit);
        int ty = Mathf.RoundToInt(localY * pixelsPerUnit);
        return new Vector2Int(
            Mathf.Clamp(tx, 0, cachedMap.width - 1),
            Mathf.Clamp(ty, 0, cachedMap.height - 1));
    }

    int FindNearestNavigable(int gx, int gy, out int outGy)
    {
        // Check the cell itself first
        if (gx >= 0 && gx < navW && gy >= 0 && gy < navH && navGrid[gx, gy])
        { outGy = gy; return gx; }

        // BFS outward to find nearest navigable cell
        int searchRadius = Mathf.Max(navW, navH) / 2;
        for (int r = 1; r <= searchRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // only perimeter
                int nx = gx + dx, ny = gy + dy;
                if (nx < 0 || nx >= navW || ny < 0 || ny >= navH) continue;
                if (navGrid[nx, ny])
                { outGy = ny; return nx; }
            }
        }

        outGy = gy;
        return -1;
    }

    List<Vector2Int> AStarSearch(int sx, int sy, int ex, int ey)
    {
        // Directions: 8-connected for smoother paths
        int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        float[] cost8 = { 1f, 1f, 1f, 1f, 1.414f, 1.414f, 1.414f, 1.414f };

        int maxIterations = navW * navH; // safety cap
        float[,] gScore = new float[navW, navH];
        int[,] cameFromX = new int[navW, navH];
        int[,] cameFromY = new int[navW, navH];
        bool[,] closed   = new bool[navW, navH];

        for (int x = 0; x < navW; x++)
        for (int y = 0; y < navH; y++)
        {
            gScore[x, y] = float.MaxValue;
            cameFromX[x, y] = -1;
            cameFromY[x, y] = -1;
        }

        gScore[sx, sy] = 0f;

        // Min-heap approximation using a sorted list (good enough for the small nav grid)
        var open = new SortedList<float, Vector2Int>(new DuplicateKeyComparer());
        float h0 = Heuristic(sx, sy, ex, ey);
        open.Add(h0, new Vector2Int(sx, sy));

        int iterations = 0;
        while (open.Count > 0 && iterations < maxIterations)
        {
            iterations++;
            var currentKey = open.Keys[0];
            var current    = open.Values[0];
            open.RemoveAt(0);

            int cx = current.x, cy = current.y;
            if (cx == ex && cy == ey)
                return ReconstructPath(cameFromX, cameFromY, sx, sy, ex, ey);

            if (closed[cx, cy]) continue;
            closed[cx, cy] = true;

            for (int i = 0; i < 8; i++)
            {
                int nx = cx + dx8[i], ny = cy + dy8[i];
                if (nx < 0 || nx >= navW || ny < 0 || ny >= navH) continue;
                if (!navGrid[nx, ny] || closed[nx, ny]) continue;

                // For diagonals, ensure both adjacent axis-aligned cells are navigable
                if (i >= 4)
                {
                    if (!navGrid[cx + dx8[i], cy] || !navGrid[cx, cy + dy8[i]])
                        continue;
                }

                float tentG = gScore[cx, cy] + cost8[i];

                // Shore avoidance: cells closer to land are penalized
                // navLandDist stores the minimum land distance for each cell
                if (shipShoreAvoidanceWeight > 0f && navLandDist != null)
                {
                    int ld = navLandDist[nx, ny];
                    if (ld < shipLandClearance * 3)
                    {
                        // Inverse: closer to shore = higher penalty
                        float proximity = 1f - ((float)ld / (shipLandClearance * 3));
                        tentG += proximity * proximity * shipShoreAvoidanceWeight * cost8[i];
                    }
                }

                if (tentG >= gScore[nx, ny]) continue;

                gScore[nx, ny]    = tentG;
                cameFromX[nx, ny] = cx;
                cameFromY[nx, ny] = cy;
                float f = tentG + Heuristic(nx, ny, ex, ey);
                open.Add(f, new Vector2Int(nx, ny));
            }
        }

        return null; // no path found
    }

    float Heuristic(int ax, int ay, int bx, int by)
    {
        // Octile distance for 8-connected grid
        int dx = Mathf.Abs(ax - bx), dy = Mathf.Abs(ay - by);
        return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
    }

    List<Vector2Int> ReconstructPath(int[,] cameFromX, int[,] cameFromY,
                                     int sx, int sy, int ex, int ey)
    {
        var path = new List<Vector2Int>();
        int cx = ex, cy = ey;
        while (cx != sx || cy != sy)
        {
            path.Add(new Vector2Int(cx, cy));
            int px = cameFromX[cx, cy], py = cameFromY[cx, cy];
            if (px < 0)
            {
                // Should not happen, but safety
                path.Clear();
                return path;
            }
            cx = px; cy = py;
        }
        path.Add(new Vector2Int(sx, sy));
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Removes intermediate waypoints that are roughly collinear to produce smoother paths.
    /// </summary>
    List<Vector3> SimplifyPath(List<Vector3> path)
    {
        if (path.Count <= 2) return path;

        var simplified = new List<Vector3> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
            Vector3 prev = simplified[simplified.Count - 1];
            Vector3 next = path[i + 1];
            Vector3 curr = path[i];

            // Keep waypoints where direction changes noticeably (lower threshold = keep more)
            // 0.95 daha muhafazakar — kıyıdan kaçınan waypoint'leri korur
            Vector3 d1 = (curr - prev).normalized;
            Vector3 d2 = (next - curr).normalized;
            if (Vector3.Dot(d1, d2) < 0.95f)
                simplified.Add(curr);
        }
        simplified.Add(path[path.Count - 1]);
        return simplified;
    }

    /// <summary>
    /// Chaikin corner-cutting: for each segment A→B, insert two new points
    /// at 25% and 75% along the segment.  Preserves first and last points.
    /// Each pass doubles point count and progressively rounds all corners.
    /// </summary>
    List<Vector3> ChaikinSmooth(List<Vector3> path)
    {
        if (path.Count <= 2) return path;

        var smooth = new List<Vector3>();
        smooth.Add(path[0]); // keep start

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = path[i];
            Vector3 b = path[i + 1];

            // Skip cutting the very first and last segments to anchor endpoints
            if (i == 0)
            {
                smooth.Add(ValidateWaypoint(Vector3.Lerp(a, b, 0.75f), a));
            }
            else if (i == path.Count - 2)
            {
                smooth.Add(ValidateWaypoint(Vector3.Lerp(a, b, 0.25f), a));
            }
            else
            {
                smooth.Add(ValidateWaypoint(Vector3.Lerp(a, b, 0.25f), a));
                smooth.Add(ValidateWaypoint(Vector3.Lerp(a, b, 0.75f), b));
            }
        }

        smooth.Add(path[path.Count - 1]); // keep end
        return smooth;
    }

    /// <summary>
    /// Path'teki ardışık waypoint'ler arasında karadan geçen segment varsa,
    /// o segmenti A* ile yeniden hesaplar.
    /// </summary>
    List<Vector3> ValidateSegments(List<Vector3> path)
    {
        if (cachedMap == null || path.Count < 2) return path;

        var validated = new List<Vector3> { path[0] };

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 a = path[i];
            Vector3 b = path[i + 1];

            if (SegmentCrossesLand(a, b))
            {
                //bu segment karadan geçiyor — orta noktayı denize iterek kır
                Vector3 mid = Vector3.Lerp(a, b, 0.5f);
                //karaya en uzak yönü bul — a ve b'nin ortasından denize doğru it
                Vector3 outward = (mid - transform.position).normalized;
                Vector2 pushDir = new Vector2(outward.x, outward.y).normalized;
                mid = PushToWater(mid, pushDir);

                //hâlâ geçiyorsa segmenti atla (ship zaten CatmullRom ile yumuşatacak)
                if (!SegmentCrossesLand(a, mid) && !SegmentCrossesLand(mid, b))
                    validated.Add(mid);
            }

            validated.Add(b);
        }

        return validated;
    }

    /// <summary>
    /// İki nokta arasındaki düz çizginin karadan geçip geçmediğini kontrol eder.
    /// </summary>
    bool SegmentCrossesLand(Vector3 a, Vector3 b)
    {
        if (cachedMap == null) return false;

        float dist = Vector3.Distance(a, b);
        int steps = Mathf.Max(3, Mathf.CeilToInt(dist / 0.1f)); //her 0.1 birimde bir kontrol

        for (int s = 1; s < steps; s++)
        {
            float t = (float)s / steps;
            Vector3 point = Vector3.Lerp(a, b, t);

            int tileX = Mathf.RoundToInt((point.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
            int tileY = Mathf.RoundToInt((point.y - transform.position.y + cachedHalfH) * pixelsPerUnit);

            if (tileX >= 0 && tileX < cachedMap.width && tileY >= 0 && tileY < cachedMap.height
                && cachedMap.IsLand(tileX, tileY))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Smoothing sonrası oluşan noktanın karaya düşüp düşmediğini kontrol eder.
    /// Karaya düştüyse orijinal waypoint'e geri döner.
    /// </summary>
    Vector3 ValidateWaypoint(Vector3 candidate, Vector3 fallback)
    {
        if (cachedMap == null) return candidate;
        int tileX = Mathf.RoundToInt((candidate.x - transform.position.x + cachedHalfW) * pixelsPerUnit);
        int tileY = Mathf.RoundToInt((candidate.y - transform.position.y + cachedHalfH) * pixelsPerUnit);
        if (tileX < 0 || tileX >= cachedMap.width || tileY < 0 || tileY >= cachedMap.height)
            return candidate;
        if (cachedMap.IsLand(tileX, tileY))
            return fallback; //karaya düştü, orijinal noktaya dön
        return candidate;
    }

    public int ActiveShipCount => activeShips.Count;
}
