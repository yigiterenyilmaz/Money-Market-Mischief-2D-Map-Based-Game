using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Agricultural BUILDING placement over the agricultural biome (biome 4). Like the urban region
// (MapDecorPlacer.UrbanLayout) — and unlike the packed city core (biome 2) or the road-lined
// industrial zone (biome 3) — the farmland is filled with a SPARSE SCATTER of farm structures
// (barns/houses/silos). Buildings are dropped at random tiles, kept far apart, and the fill is
// intentionally LOW DENSITY so the region reads as open fields dotted with farmsteads. Buildings
// reuse the city-building machinery (CreateCityBuildingObject) so they get day/night crossfade +
// dynamic shadows, and they avoid roads (TryFindRoadFreePosition) and the shoreline (HasShoreBuffer).
public partial class MapDecorPlacer
{
    // -------------------------------------------------------------------------
    // AGRICULTURAL SCATTER — serialized tuning (declared here; Unity serializes
    // public fields of a partial class regardless of which file they live in).
    // -------------------------------------------------------------------------

    [Header("Agricultural Layout — Seyrek Bina Dağılımı")]
    [Tooltip("Tarım binalarını yerleştir (biome 4). Kapalıysa hiç bina konmaz.")]
    public bool agriculturalPlaceBuildings = true;

    [Tooltip("Tarım binalarının ölçek aralığı (min, max).")]
    public Vector2 agriculturalScaleRange = new Vector2(0.35f, 0.55f);

    [Tooltip("Doluluk yoğunluğu. SEYREK olması istendiği için düşük tutun. " +
             "1.0 = yoğun, 0.1 = çok seyrek.")]
    [Range(0.02f, 1f)] public float agriculturalFillDensity = 0.1f;

    [Tooltip("Yerleşim denemesi çarpanı. Yüksek = boşluklar daha çok doldurulmaya çalışılır.")]
    [Range(1, 8)] public int agriculturalScatterRate = 2;

    [Tooltip("Minimum bina aralığı (dünya birimi, yarıçap). Büyük = binalar daha seyrek/uzak. " +
             "Seyrek görünüm için sprite yarıçapından büyük tutun.")]
    [Range(0.1f, 4f)] public float agriculturalSpacing = 1.0f;

    // -------------------------------------------------------------------------
    // AGRICULTURAL SCATTER FILL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tarım binalarını biome 4 tile'larına SEYREK dağılımla yerleştirir. Şehir çekirdeğinin
    /// aksine ızgara/sokak yapısı yoktur; binalar rastgele tile'lara, birbirinden uzak
    /// (agriculturalSpacing) ve düşük yoğunlukta (agriculturalFillDensity) dağıtılır. Yola değen
    /// ve kıyıya yakın adaylar elenir. Tüm ayarlar yukarıda + MapDecorPlacer'da serileştirilir.
    /// </summary>
    void PlaceAgriculturalLayout(MapGenerator map, BiomePaintSettings settings,
                                 List<Vector2Int> tiles, float halfW, float halfH)
    {
        if (!agriculturalPlaceBuildings) return;
        if (tiles == null || tiles.Count == 0) return;

        if (settings.agriculturalBuildings == null || settings.agriculturalBuildings.Count == 0)
        {
            Debug.Log("MapDecorPlacer: agricultural layout atlandı — agriculturalBuildings boş.");
            return;
        }

        // null sprite'lı entry'leri ele — geçerli indeksleri topla (dengeli seçim için)
        var valid = new List<int>();
        for (int i = 0; i < settings.agriculturalBuildings.Count; i++)
            if (settings.agriculturalBuildings[i].daySprite != null)
                valid.Add(i);
        if (valid.Count == 0)
        {
            Debug.LogWarning("MapDecorPlacer: agricultural layout — hiçbir agriculturalBuildings entry'sinde daySprite yok.");
            return;
        }

        Vector2 sr    = agriculturalScaleRange == Vector2.zero ? new Vector2(0.35f, 0.55f) : agriculturalScaleRange;
        float density = Mathf.Clamp01(agriculturalFillDensity);
        int rate      = Mathf.Clamp(agriculturalScatterRate, 1, 8);
        float overlapR = Mathf.Max(0.1f, agriculturalSpacing);
        int attempts  = Mathf.Max(1, Mathf.RoundToInt(tiles.Count * density * rate));

        int placed = 0;
        int[] spriteCounts = new int[valid.Count];

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2Int t = tiles[Random.Range(0, tiles.Count)];
            int tx = t.x, ty = t.y;

            if (!map.IsLand(tx, ty)) continue;
            if (map.GetBiome(tx, ty) != 4) continue;
            if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) continue;

            int pick      = PickBalancedSpriteIndex(spriteCounts);
            var entry     = settings.agriculturalBuildings[valid[pick]];
            Sprite daySprite = entry.daySprite;

            float scale = Random.Range(sr.x, sr.y);

            if (!TryFindRoadFreePosition(tx, ty, daySprite, scale, 8, cityMinRoadDistance,
                                         out int newTx, out int newTy))
                continue;
            if (newTx != tx || newTy != ty)
            {
                if (!map.IsLand(newTx, newTy)) continue;
                if (map.GetBiome(newTx, newTy) != 4) continue;
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
            go.name = "AgriculturalBuilding";

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

        Debug.Log($"MapDecorPlacer: agricultural layout (seyrek) — attempts={attempts}, placed={placed}, " +
                  $"density={density:F2}, spacing={overlapR:F2}");
    }
}
