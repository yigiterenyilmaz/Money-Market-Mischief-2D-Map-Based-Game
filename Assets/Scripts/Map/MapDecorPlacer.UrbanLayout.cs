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
}
