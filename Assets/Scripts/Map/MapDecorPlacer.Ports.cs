using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Port placement: ocean-mask flood fill, shore-candidate selection, two-port
// separation, instantiation, and port public getters.
public partial class MapDecorPlacer
{
    /// <summary>
    /// Finds city tiles adjacent to water (shore tiles) and places ports.
    /// Only spawns if the city region is NOT at the map edge (i.e. not touching
    /// the island boundary defined by fog / safe zone).
    /// </summary>
    void PlacePorts(MapGenerator map, float halfW, float halfH)
    {
        if (portSpritesDay == null || portSpritesDay.Count == 0) return;

        // 0. Build ocean mask: flood-fill from map edges through water tiles.
        bool[,] isOcean = BuildOceanMask(map);

        // 1. Collect ocean-shore candidates per biome, in priority order:
        //    Cities (2) → Industrial (3) → Urban (4) → Agricultural (1)
        int[] biomePriority = { 2, 3, 4, 1 };
        int edgeMargin = 8;

        var candidatesByBiome = new Dictionary<int, List<Vector2Int>>();
        foreach (int b in biomePriority)
            candidatesByBiome[b] = new List<Vector2Int>();

        for (int x = edgeMargin; x < map.width - edgeMargin; x++)
        for (int y = edgeMargin; y < map.height - edgeMargin; y++)
        {
            if (!map.IsLand(x, y)) continue;
            if (map.GetFog(x, y) > 0.4f) continue;

            int biome = map.GetBiome(x, y);
            if (!candidatesByBiome.ContainsKey(biome)) continue;

            // Must be adjacent to ocean
            if (!IsAdjacentToOcean(map, x, y, isOcean)) continue;

            // Must have enough backing of same biome inland
            if (!HasRegionBacking(map, x, y, biome)) continue;

            candidatesByBiome[biome].Add(new Vector2Int(x, y));
        }

        // 2. Try to place both ports in the highest-priority biome that works.
        //    "Works" = we can find 2 candidates with portMinSeparation Euclidean distance.
        //    If a biome can't satisfy both, fall through to the next.
        List<Vector2Int> portTiles = null;

        foreach (int biome in biomePriority)
        {
            var candidates = candidatesByBiome[biome];
            if (candidates.Count < 2) continue;

            portTiles = TryPickTwoPorts(candidates);
            if (portTiles != null)
            {
                Debug.Log($"MapDecorPlacer: Both ports placed in biome {biome}.");
                break;
            }
        }

        // 3. If no single biome could hold both, try mixed: place first in highest
        //    priority available, second in next available biome that satisfies distance.
        if (portTiles == null)
        {
            portTiles = TryPickTwoPortsMixed(candidatesByBiome, biomePriority);
            if (portTiles != null)
                Debug.Log("MapDecorPlacer: Ports placed across different biomes.");
        }

        if (portTiles == null || portTiles.Count < 2)
        {
            Debug.Log("MapDecorPlacer: Could not place 2 ports with required separation.");
            return;
        }

        // 4. Instantiate the 2 port GameObjects
        foreach (var tile in portTiles)
            InstantiatePort(tile, halfW, halfH);

        Debug.Log($"MapDecorPlacer: Placed {ports.Count} port(s), separation={TileDistance(portTiles[0], portTiles[1]):F0} tiles.");
    }

    /// <summary>
    /// Tries to pick 2 candidates from a single list with at least portMinSeparation Euclidean distance.
    /// Shuffles and greedily searches.
    /// </summary>
    List<Vector2Int> TryPickTwoPorts(List<Vector2Int> candidates)
    {
        ShuffleList(candidates);

        // Try each candidate as first port, find a second that's far enough
        for (int i = 0; i < Mathf.Min(candidates.Count, 80); i++)
        {
            Vector2Int first = candidates[i];
            for (int j = i + 1; j < candidates.Count; j++)
            {
                if (TileDistance(first, candidates[j]) >= portMinSeparation)
                    return new List<Vector2Int> { first, candidates[j] };
            }
        }
        return null;
    }

    /// <summary>
    /// Picks first port from highest-priority biome, second from any other biome
    /// that satisfies the distance constraint.
    /// </summary>
    List<Vector2Int> TryPickTwoPortsMixed(Dictionary<int, List<Vector2Int>> candidatesByBiome, int[] biomePriority)
    {
        // Pick first port from highest priority biome that has candidates
        Vector2Int first = Vector2Int.zero;
        bool foundFirst = false;

        foreach (int biome in biomePriority)
        {
            var list = candidatesByBiome[biome];
            if (list.Count > 0)
            {
                first = list[Random.Range(0, list.Count)];
                foundFirst = true;
                break;
            }
        }

        if (!foundFirst) return null;

        // Pick second from any biome (in priority order) that satisfies distance
        foreach (int biome in biomePriority)
        {
            var list = candidatesByBiome[biome];
            ShuffleList(list);
            foreach (var candidate in list)
            {
                if (TileDistance(first, candidate) >= portMinSeparation)
                    return new List<Vector2Int> { first, candidate };
            }
        }

        return null;
    }

    float TileDistance(Vector2Int a, Vector2Int b)
    {
        float dx = a.x - b.x, dy = a.y - b.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    void InstantiatePort(Vector2Int tile, float halfW, float halfH)
    {
        int spriteIdx = Random.Range(0, portSpritesDay.Count);
        Sprite daySprite = portSpritesDay[spriteIdx];
        if (daySprite == null) return;

        float wx = transform.position.x + (tile.x / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (tile.y / pixelsPerUnit) - halfH;

        float scale   = Random.Range(portScaleRange.x, portScaleRange.y);
        float baseA   = Random.Range(0.9f, 1f);
        int sortOrder = 12 + (int)(wy * -100f);

        GameObject go = new GameObject("Port");
        go.transform.SetParent(transform);
        go.transform.position   = new Vector3(wx, wy, spriteZ);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer daySR = go.AddComponent<SpriteRenderer>();
        daySR.sprite       = daySprite;
        daySR.sortingOrder = sortOrder;
        daySR.color        = new Color(1f, 1f, 1f, baseA);

        SpriteRenderer nightSR = null;
        if (portSpritesNight != null && spriteIdx < portSpritesNight.Count &&
            portSpritesNight[spriteIdx] != null)
        {
            GameObject nightGo = new GameObject("PortNight");
            nightGo.transform.SetParent(go.transform, false);
            // Gece sprite'ını gündüz tabanına hizala (ışık taşması bounding box'ı büyütür).
            Vector3 align = ComputeDayNightAlign(daySprite, portSpritesNight[spriteIdx]);
            nightGo.transform.localPosition = new Vector3(align.x, align.y, 0f);
            nightGo.transform.localScale    = Vector3.one;
            nightGo.transform.localRotation = Quaternion.identity;

            nightSR              = nightGo.AddComponent<SpriteRenderer>();
            nightSR.sprite       = portSpritesNight[spriteIdx];
            nightSR.sortingOrder = sortOrder + 1;
            nightSR.color        = new Color(1f, 1f, 1f, 0f);
        }

        decorObjects.Add(go);

        //kıyı yönünü hesapla — tüm su komşularının ortalama yönü
        Vector2 seaDir = Vector2.zero;
        int[] dx8 = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy8 = { 0, 0, 1, -1, 1, -1, 1, -1 };
        for (int d = 0; d < 8; d++)
        {
            int nx = tile.x + dx8[d], ny = tile.y + dy8[d];
            if (cachedMap != null && nx >= 0 && nx < cachedMap.width && ny >= 0 && ny < cachedMap.height && !cachedMap.IsLand(nx, ny))
            {
                seaDir += new Vector2(dx8[d], dy8[d]);
            }
        }
        if (seaDir.sqrMagnitude < 0.001f) seaDir = Vector2.down;
        seaDir = seaDir.normalized;
        //kıyıya paralel yön = deniz yönüne dik (saat yönünde 90 derece)
        Vector2 shoreDir = new Vector2(-seaDir.y, seaDir.x);

        ports.Add(new PortData
        {
            go             = go,
            dayRenderer    = daySR,
            nightRenderer  = nightSR,
            tileX          = tile.x,
            tileY          = tile.y,
            baseAlpha      = baseA,
            worldPos       = new Vector3(wx, wy, spriteZ),
            shoreDirection = shoreDir,
            seaDirection   = seaDir
        });
    }

    bool[,] BuildOceanMask(MapGenerator map)
    {
        bool[,] isOcean = new bool[map.width, map.height];
        Queue<Vector2Int> oceanQueue = new Queue<Vector2Int>();

        for (int x = 0; x < map.width; x++)
        {
            if (!map.IsLand(x, 0))              { isOcean[x, 0] = true;              oceanQueue.Enqueue(new Vector2Int(x, 0)); }
            if (!map.IsLand(x, map.height - 1)) { isOcean[x, map.height - 1] = true; oceanQueue.Enqueue(new Vector2Int(x, map.height - 1)); }
        }
        for (int y = 1; y < map.height - 1; y++)
        {
            if (!map.IsLand(0, y))              { isOcean[0, y] = true;              oceanQueue.Enqueue(new Vector2Int(0, y)); }
            if (!map.IsLand(map.width - 1, y))  { isOcean[map.width - 1, y] = true; oceanQueue.Enqueue(new Vector2Int(map.width - 1, y)); }
        }

        int[] odx = { 1, -1, 0, 0 };
        int[] ody = { 0, 0, 1, -1 };
        while (oceanQueue.Count > 0)
        {
            var pos = oceanQueue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + odx[i], ny = pos.y + ody[i];
                if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) continue;
                if (isOcean[nx, ny] || map.IsLand(nx, ny)) continue;
                isOcean[nx, ny] = true;
                oceanQueue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return isOcean;
    }

    bool IsAdjacentToWater(MapGenerator map, int x, int y)
    {
        return (!map.IsLand(x + 1, y) || !map.IsLand(x - 1, y) ||
                !map.IsLand(x, y + 1) || !map.IsLand(x, y - 1));
    }

    /// <summary>
    /// True if at least one 4-connected neighbor is ocean water (not an inland lake).
    /// </summary>
    bool IsAdjacentToOcean(MapGenerator map, int x, int y, bool[,] isOcean)
    {
        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };
        for (int i = 0; i < 4; i++)
        {
            int nx = x + dx4[i], ny = y + dy4[i];
            if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) continue;
            if (!map.IsLand(nx, ny) && isOcean[nx, ny]) return true;
        }
        return false;
    }

    /// <summary>
    /// Checks that behind this shore tile there are enough tiles of the given biome,
    /// ensuring the port isn't on a thin sliver of coast.
    /// </summary>
    bool HasRegionBacking(MapGenerator map, int x, int y, int biome)
    {
        int count = 0;
        int r = portRegionBackingRadius;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            if (dx * dx + dy * dy > r * r) continue;
            int nx = x + dx, ny = y + dy;
            if (nx < 0 || nx >= map.width || ny < 0 || ny >= map.height) continue;
            if (map.IsLand(nx, ny) && map.GetBiome(nx, ny) == biome) count++;
        }
        int totalInCircle = 0;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
            if (dx * dx + dy * dy <= r * r) totalInCircle++;

        return count >= totalInCircle * 0.5f;
    }

    // =========================================================================
    // PORT PUBLIC GETTERS
    // =========================================================================

    public int PortCount => ports.Count;

    public Vector2Int GetPortTile(int index)
    {
        if (index < 0 || index >= ports.Count) return Vector2Int.zero;
        return new Vector2Int(ports[index].tileX, ports[index].tileY);
    }
}
