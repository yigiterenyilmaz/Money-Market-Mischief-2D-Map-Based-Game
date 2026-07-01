using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Urban BUILDING placement over the urban biome (biome 1). Unlike the city core (biome 2,
// MapDecorPlacer.CityLayout) and the road-lined industrial zone (biome 3), the urban region is
// filled with a SPARSE SCATTER of small structures/houses — buildings are dropped at random
// tiles, kept far apart, and the fill is intentionally LOW DENSITY so the region reads as open
// countryside dotted with settlements rather than a packed city. Buildings reuse the city-building
// machinery (CreateCityBuildingObject) so they get day/night crossfade + dynamic shadows, and they
// avoid roads (TryFindRoadFreePosition) and the shoreline (HasShoreBuffer).
public partial class MapDecorPlacer
{
    // -------------------------------------------------------------------------
    // URBAN SCATTER — serialized tuning (declared here; Unity serializes public
    // fields of a partial class regardless of which file they live in).
    // -------------------------------------------------------------------------

    [Header("Urban Layout — Seyrek Bina Dağılımı")]
    [Tooltip("Urban binalarını yerleştir (biome 1). Kapalıysa hiç bina konmaz.")]
    public bool urbanPlaceBuildings = true;

    [Tooltip("Urban binalarının ölçek aralığı (min, max).")]
    public Vector2 urbanScaleRange = new Vector2(0.35f, 0.55f);

    [Tooltip("Doluluk yoğunluğu. SEYREK olması istendiği için düşük tutun. " +
             "1.0 = yoğun, 0.1 = çok seyrek.")]
    [Range(0.02f, 1f)] public float urbanFillDensity = 0.15f;

    [Tooltip("Yerleşim denemesi çarpanı. Yüksek = boşluklar daha çok doldurulmaya çalışılır.")]
    [Range(1, 8)] public int urbanScatterRate = 2;

    [Tooltip("Minimum bina aralığı (dünya birimi, yarıçap). Büyük = binalar daha seyrek/uzak. " +
             "Seyrek görünüm için sprite yarıçapından büyük tutun.")]
    [Range(0.1f, 4f)] public float urbanSpacing = 0.8f;

    [Header("Urban Layout — Ağaç/Doğa Dağılımı")]
    [Tooltip("Urban ağaçlarını/doğa öğelerini yerleştir (biome 1). Kapalıysa hiç ağaç konmaz.")]
    public bool urbanPlaceNature = true;

    [Tooltip("Urban ağaçlarının ölçek aralığı (min, max).")]
    public Vector2 urbanNatureScaleRange = new Vector2(0.3f, 0.5f);

    [Tooltip("Ağaç doluluk yoğunluğu. 1.0 = yoğun (arazinin çoğunu doldurur), 0.1 = çok seyrek.")]
    [Range(0.02f, 1f)] public float urbanNatureFillDensity = 1f;

    [Tooltip("Ağaç yerleşim denemesi çarpanı. Yüksek = boşluklar daha çok doldurulmaya çalışılır.")]
    [Range(1, 8)] public int urbanNatureScatterRate = 8;

    [Tooltip("Minimum ağaç aralığı (dünya birimi). Küçük = ağaçlar daha sık/iç içe. NOT: bir tile = " +
             "1/pixelsPerUnit dünya birimi (genelde 0.01), bu yüzden yoğun orman için çok küçük " +
             "değerler gerekir (0.02–0.06).")]
    [Range(0.01f, 4f)] public float urbanNatureSpacing = 0.05f;

    // -------------------------------------------------------------------------
    // URBAN SCATTER FILL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Urban binalarını biome 1 tile'larına SEYREK dağılımla yerleştirir. Şehir çekirdeğinin
    /// aksine ızgara/sokak yapısı yoktur; binalar rastgele tile'lara, birbirinden uzak (urbanSpacing)
    /// ve düşük yoğunlukta (urbanFillDensity) dağıtılır. Yola değen ve kıyıya yakın adaylar elenir.
    /// Tüm ayarlar yukarıda + MapDecorPlacer'da serileştirilir.
    /// </summary>
    void PlaceUrbanLayout(MapGenerator map, BiomePaintSettings settings,
                          List<Vector2Int> tiles, float halfW, float halfH)
    {
        if (!urbanPlaceBuildings) return;
        if (tiles == null || tiles.Count == 0) return;

        if (settings.urbanBuildings == null || settings.urbanBuildings.Count == 0)
        {
            Debug.Log("MapDecorPlacer: urban layout atlandı — urbanBuildings boş.");
            return;
        }

        // null sprite'lı entry'leri ele — geçerli indeksleri topla (dengeli seçim için)
        var valid = new List<int>();
        for (int i = 0; i < settings.urbanBuildings.Count; i++)
            if (settings.urbanBuildings[i].daySprite != null)
                valid.Add(i);
        if (valid.Count == 0)
        {
            Debug.LogWarning("MapDecorPlacer: urban layout — hiçbir urbanBuildings entry'sinde daySprite yok.");
            return;
        }

        Vector2 sr    = urbanScaleRange == Vector2.zero ? new Vector2(0.35f, 0.55f) : urbanScaleRange;
        float density = Mathf.Clamp01(urbanFillDensity);
        int rate      = Mathf.Clamp(urbanScatterRate, 1, 8);
        float overlapR = Mathf.Max(0.1f, urbanSpacing);
        int attempts  = Mathf.Max(1, Mathf.RoundToInt(tiles.Count * density * rate));

        int placed = 0;
        int[] spriteCounts = new int[valid.Count];

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2Int t = tiles[Random.Range(0, tiles.Count)];
            int tx = t.x, ty = t.y;

            if (!map.IsLand(tx, ty)) continue;
            if (map.GetBiome(tx, ty) != 1) continue;
            if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) continue;

            int pick      = PickBalancedSpriteIndex(spriteCounts);
            var entry     = settings.urbanBuildings[valid[pick]];
            Sprite daySprite = entry.daySprite;

            float scale = Random.Range(sr.x, sr.y);

            if (!TryFindRoadFreePosition(tx, ty, daySprite, scale, 8, cityMinRoadDistance,
                                         out int newTx, out int newTy))
                continue;
            if (newTx != tx || newTy != ty)
            {
                if (!map.IsLand(newTx, newTy)) continue;
                if (map.GetBiome(newTx, newTy) != 1) continue;
                if (cityShoreBuffer > 0 && !HasShoreBuffer(map, newTx, newTy)) continue;
            }
            tx = newTx; ty = newTy;

            float effRadius = ComputeBuildingRadius(daySprite, scale, overlapR);
            float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
            float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

            if (IsDenseOverlapping(wx, wy, effRadius)) continue;
            AddDense(wx, wy, effRadius);

            float baseA   = 1f;
            int sortOrder = 10 + (int)(wy * -100f);

            var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
                daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder, entry.isIsometric);
            go.name = "UrbanBuilding";

            AttachBuildingAnimators(daySR, nightSR, entry);

            decorObjects.Add(go);
            cityBuildings.Add(new BuildingData
            {
                go            = go,
                dayRenderer   = daySR,
                nightRenderer = nightSR,
                shadow        = shadow,
                tileX         = tx,
                tileY         = ty,
                isBroken      = false,
                isSpecial     = false,
                spriteIndex   = valid[pick],
                brokenIndex   = -1,
                baseAlpha     = baseA,
            });
            spriteCounts[pick]++;
            placed++;
        }

        Debug.Log($"MapDecorPlacer: urban layout (seyrek) — attempts={attempts}, placed={placed}, " +
                  $"density={density:F2}, spacing={overlapR:F2}");
    }

    // -------------------------------------------------------------------------
    // URBAN NATURE (TREES) SCATTER
    // -------------------------------------------------------------------------

    /// <summary>
    /// Urban ağaçlarını/doğa öğelerini biome 1 tile'larına seyrek dağılımla yerleştirir. Binaların
    /// arasındaki boşlukları doldurur; bina makinesini (CreateCityBuildingObject) tekrar kullanır,
    /// böylece gündüz/gece crossfade + dinamik gölge alır. Yola değen ve kıyıya yakın adaylar elenir.
    /// </summary>
    void PlaceUrbanNature(MapGenerator map, BiomePaintSettings settings,
                          List<Vector2Int> tiles, float halfW, float halfH)
    {
        if (!urbanPlaceNature) return;
        if (tiles == null || tiles.Count == 0) return;

        if (settings.urbanNature == null || settings.urbanNature.Count == 0)
        {
            Debug.Log("MapDecorPlacer: urban nature atlandı — urbanNature boş.");
            return;
        }

        // null sprite'lı entry'leri ele — geçerli indeksleri topla (dengeli seçim için)
        var valid = new List<int>();
        for (int i = 0; i < settings.urbanNature.Count; i++)
            if (settings.urbanNature[i].daySprite != null)
                valid.Add(i);
        if (valid.Count == 0)
        {
            Debug.LogWarning("MapDecorPlacer: urban nature — hiçbir urbanNature entry'sinde daySprite yok.");
            return;
        }

        Vector2 sr    = urbanNatureScaleRange == Vector2.zero ? new Vector2(0.3f, 0.5f) : urbanNatureScaleRange;
        float density = Mathf.Clamp01(urbanNatureFillDensity);
        int rate      = Mathf.Clamp(urbanNatureScatterRate, 1, 8);
        float overlapR = Mathf.Max(0.01f, urbanNatureSpacing);
        int attempts  = Mathf.Max(1, Mathf.RoundToInt(tiles.Count * density * rate));

        int placed = 0;
        int[] spriteCounts = new int[valid.Count];

        // Ağaçlar SADECE birbirleriyle aralıklansın — binalarla DEĞİL. Global denseOccupied'a karşı
        // test edersek, binaların (sparse görünüm için şişirilmiş) dev yarıçapları tüm bölgeyi
        // kaplar ve hiçbir ağaç sığmaz. Bunun yerine ağaçlara özel hafif bir spatial hash tutuyoruz;
        // ağaçların binaların yanında/arkasında olması doğaldır.
        float cell = Mathf.Max(0.05f, overlapR);
        var treeGrid = new Dictionary<Vector2Int, List<Vector2>>();
        Vector2Int TreeCell(float x, float y)
            => new Vector2Int(Mathf.FloorToInt(x / cell), Mathf.FloorToInt(y / cell));
        bool TreeOverlap(float x, float y)
        {
            Vector2Int c = TreeCell(x, y);
            for (int cx = c.x - 1; cx <= c.x + 1; cx++)
            for (int cy = c.y - 1; cy <= c.y + 1; cy++)
            {
                if (!treeGrid.TryGetValue(new Vector2Int(cx, cy), out var list)) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    float dx = x - list[k].x, dy = y - list[k].y;
                    if (dx * dx + dy * dy < overlapR * overlapR) return true;
                }
            }
            return false;
        }
        void AddTree(float x, float y)
        {
            Vector2Int c = TreeCell(x, y);
            if (!treeGrid.TryGetValue(c, out var list)) { list = new List<Vector2>(); treeGrid[c] = list; }
            list.Add(new Vector2(x, y));
        }

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2Int t = tiles[Random.Range(0, tiles.Count)];
            int tx = t.x, ty = t.y;

            if (!map.IsLand(tx, ty)) continue;
            if (map.GetBiome(tx, ty) != 1) continue;
            // Ağaçlar araziyi yoğun doldursun: bina filtreleri (kıyı tamponu + sprite-footprint yol
            // boşluğu) uygulanmaz — onlar adayların ~%98'ini eler. Sadece DOĞRUDAN yol tile'ının
            // üstüne ağaç koymayalım; yola bitişik/yakın ağaçlar (ağaçlı sokaklar) doğaldır.
            if (RoadGenerator.Instance != null && RoadGenerator.Instance.IsRoad(tx, ty)) continue;

            int pick      = PickBalancedSpriteIndex(spriteCounts);
            var entry     = settings.urbanNature[valid[pick]];
            Sprite daySprite = entry.daySprite;

            float scale = Random.Range(sr.x, sr.y);

            float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
            float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

            // Sadece diğer ağaçlara karşı aralık kontrolü — binalara karşı DEĞİL (yukarıdaki nota bak).
            if (TreeOverlap(wx, wy)) continue;
            AddTree(wx, wy);

            float baseA   = 1f;
            int sortOrder = 10 + (int)(wy * -100f);

            var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
                daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder, entry.isIsometric);
            go.name = "UrbanTree";

            AttachBuildingAnimators(daySR, nightSR, entry);

            decorObjects.Add(go);
            cityBuildings.Add(new BuildingData
            {
                go            = go,
                dayRenderer   = daySR,
                nightRenderer = nightSR,
                shadow        = shadow,
                tileX         = tx,
                tileY         = ty,
                isBroken      = false,
                isSpecial     = false,
                spriteIndex   = valid[pick],
                brokenIndex   = -1,
                baseAlpha     = baseA,
            });
            spriteCounts[pick]++;
            placed++;
        }

        Debug.Log($"MapDecorPlacer: urban nature (ağaç) — attempts={attempts}, placed={placed}, " +
                  $"density={density:F2}, spacing={overlapR:F2}");
    }
}
