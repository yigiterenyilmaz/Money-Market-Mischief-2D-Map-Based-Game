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

    [Tooltip("Urban arazinin ne kadarı ORMAN olsun (0..1). 0 = ağaç yok, 1 = neredeyse tüm açık alan " +
             "ormanlık. Ağaç kapsamını/sayısını asıl bu belirler.")]
    [Range(0f, 1f)] public float urbanForestCoverage = 0.55f;

    [Tooltip("Orman öbeklerinin BÜYÜKLÜĞÜ (tile). Büyük = geniş, birbirine bağlı ormanlar + geniş " +
             "açıklıklar; küçük = dağınık küçük korular. 30–80 arası doğal durur.")]
    [Range(8f, 200f)] public float urbanForestSizeTiles = 48f;

    [Tooltip("Orman İÇİ ağaç sıklığı (0..1). 1 = kanopiler iç içe (sık orman), 0 = ağaçlar aralıklı. " +
             "Aralık ağaç SPRITE boyutuna göre otomatik ölçeklenir — sabit dünya-birimi ayarı YOK.")]
    [Range(0f, 1f)] public float urbanTreeDensity = 0.85f;

    [Tooltip("Ağaçların bina/yol kenarından koruyacağı boşluk (TILE). Ağacın GERÇEK sprite tabanına göre " +
             "ölçülür (binaların şişirilmiş yerleşim yarıçapına DEĞİL) → küçük değerler yeterlidir. 2–5.")]
    [Range(0f, 12f)] public float urbanTreeClearanceTiles = 3f;

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

        Vector2 sr = urbanNatureScaleRange == Vector2.zero ? new Vector2(0.3f, 0.5f) : urbanNatureScaleRange;

        // -- ORMAN ALANI (fBm gürültü) ---------------------------------------------------------
        // coverage → eşik: Perlin ~0.5 civarında yoğunlaştığı için eşiği [0.72 .. 0.14] arasına eşleriz
        // (coverage 0 = az orman, 1 = neredeyse her yer). Çekirdek TAM dolar; yalnızca ince kenar seyrelir
        // → ormanlar dolgun ve büyük görünür (eski lineer rampa çekirdeği de seyreltiyordu = cılız orman).
        float forestScale = Mathf.Max(6f, urbanForestSizeTiles);
        float coverage    = Mathf.Clamp01(urbanForestCoverage);
        float thr         = Mathf.Lerp(0.72f, 0.14f, coverage);
        const float edge  = 0.06f;
        float nSeedX = Random.Range(0f, 500f);
        float nSeedY = Random.Range(0f, 500f);

        float ForestDensityAt(int fx, int fy)
        {
            float lo = Mathf.PerlinNoise(nSeedX + fx / forestScale,          nSeedY + fy / forestScale);
            float hi = Mathf.PerlinNoise(nSeedX + fx / (forestScale * 0.45f) + 37f,
                                         nSeedY + fy / (forestScale * 0.45f) + 37f);
            float n = lo * 0.7f + hi * 0.3f;
            return Mathf.SmoothStep(thr, thr + edge, n); // çekirdek=1, açıklık=0
        }

        // -- AĞAÇ ARALIĞI (sprite-boyutuna göre) ------------------------------------------------
        // Aralık ağacın GERÇEK yarıçapından türetilir → sabit dünya-birimi sihirli sayı yok. density 1 =
        // kanopiler iç içe (aralık ≈ 0.7×yarıçap), density 0 = seyrek (≈2.2×). Her ağaç kendi yarıçapını
        // taşıdığı için farklı ölçekli ağaçlar doğru aralıklanır.
        float packFactor = Mathf.Lerp(2.2f, 0.7f, Mathf.Clamp01(urbanTreeDensity));
        float clearWorld = Mathf.Max(0f, urbanTreeClearanceTiles) / pixelsPerUnit;
        int   roadClearTiles = Mathf.RoundToInt(Mathf.Max(0f, urbanTreeClearanceTiles));

        // En büyük olası ağaç aralığı (grid hücresi için) — geçerli sprite'ların maks yarıçapı × maks pack.
        float maxTreeR = 0f;
        for (int i = 0; i < valid.Count; i++)
        {
            float r = ComputeBuildingRadius(settings.urbanNature[valid[i]].daySprite, sr.y, 0f);
            if (r > maxTreeR) maxTreeR = r;
        }
        float maxTreeSpacing = Mathf.Max(0.02f, maxTreeR * packFactor * 1.3f);

        // -- GERÇEK BİNA FOOTPRINT'LERİ ---------------------------------------------------------
        // KRİTİK: denseOccupied'a karşı test ETMEYİZ — orada bina yarıçapları SEYREK yerleşim için
        // şişirilmiştir (ör. urbanSpacing ~ onlarca tile) ve her binanın etrafında koca bir delik açıp
        // ormanı yok eder. Bunun yerine cityBuildings'teki her binanın GERÇEK sprite tabanından ince bir
        // spatial hash kurarız; ağaçlar yalnızca binanın asıl gövdesinden + küçük bir tampon kadar durur.
        // (Ağaçlar en son yerleştiği için cityBuildings tüm gerçek binaları içerir, henüz ağaç yok.)
        var bFoot = new List<Vector3>(cityBuildings.Count);
        float bMaxR = 0f;
        for (int i = 0; i < cityBuildings.Count; i++)
        {
            var bd = cityBuildings[i];
            if (bd.isTree || bd.go == null || bd.dayRenderer == null || bd.dayRenderer.sprite == null) continue;
            float bR = ComputeBuildingRadius(bd.dayRenderer.sprite, bd.go.transform.localScale.x, 0f);
            Vector3 bp = bd.go.transform.position;
            bFoot.Add(new Vector3(bp.x, bp.y, bR));
            if (bR > bMaxR) bMaxR = bR;
        }
        float bCell = Mathf.Max(0.05f, bMaxR);
        var bHash = new Dictionary<Vector2Int, List<Vector3>>();
        for (int i = 0; i < bFoot.Count; i++)
        {
            Vector3 f = bFoot[i];
            var c = new Vector2Int(Mathf.FloorToInt(f.x / bCell), Mathf.FloorToInt(f.y / bCell));
            if (!bHash.TryGetValue(c, out var l)) { l = new List<Vector3>(); bHash[c] = l; }
            l.Add(f);
        }
        bool NearBuilding(float x, float y, float myR)
        {
            if (bHash.Count == 0) return false;
            float reach = myR + bMaxR;
            int minCx = Mathf.FloorToInt((x - reach) / bCell), maxCx = Mathf.FloorToInt((x + reach) / bCell);
            int minCy = Mathf.FloorToInt((y - reach) / bCell), maxCy = Mathf.FloorToInt((y + reach) / bCell);
            for (int cx = minCx; cx <= maxCx; cx++)
            for (int cy = minCy; cy <= maxCy; cy++)
            {
                if (!bHash.TryGetValue(new Vector2Int(cx, cy), out var l)) continue;
                for (int k = 0; k < l.Count; k++)
                {
                    float dx = x - l[k].x, dy = y - l[k].y, md = myR + l[k].z;
                    if (dx * dx + dy * dy < md * md) return true;
                }
            }
            return false;
        }

        // -- AĞAÇ-AĞAÇ ARALIK HASH'İ (değişken yarıçap) -----------------------------------------
        float cell = Mathf.Max(0.05f, maxTreeSpacing);
        var treeGrid = new Dictionary<Vector2Int, List<Vector3>>();
        Vector2Int TreeCell(float x, float y)
            => new Vector2Int(Mathf.FloorToInt(x / cell), Mathf.FloorToInt(y / cell));
        bool TreeOverlap(float x, float y, float myR)
        {
            Vector2Int c = TreeCell(x, y);
            for (int cx = c.x - 1; cx <= c.x + 1; cx++)
            for (int cy = c.y - 1; cy <= c.y + 1; cy++)
            {
                if (!treeGrid.TryGetValue(new Vector2Int(cx, cy), out var list)) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    Vector3 o = list[k];
                    float dx = x - o.x, dy = y - o.y;
                    float minD = Mathf.Max(myR, o.z);
                    if (dx * dx + dy * dy < minD * minD) return true;
                }
            }
            return false;
        }
        void AddTree(float x, float y, float r)
        {
            Vector2Int c = TreeCell(x, y);
            if (!treeGrid.TryGetValue(c, out var list)) { list = new List<Vector3>(); treeGrid[c] = list; }
            list.Add(new Vector3(x, y, r));
        }

        int placed = 0;
        int[] spriteCounts = new int[valid.Count];

        // TÜM urban tile'larını karıştırılmış sırayla bir kez gez → orman çekirdekleri dolu/solid çıkar.
        var order = new List<Vector2Int>(tiles);
        ShuffleInPlace(order);

        for (int idx = 0; idx < order.Count; idx++)
        {
            int tx = order[idx].x, ty = order[idx].y;

            if (!map.IsLand(tx, ty)) continue;
            if (map.GetBiome(tx, ty) != 1) continue;

            float forestP = ForestDensityAt(tx, ty);
            if (forestP <= 0.001f) continue;                       // açıklık
            if (forestP < 1f && Random.value > forestP) continue;  // yumuşak orman kenarı

            int pick      = PickBalancedSpriteIndex(spriteCounts);
            var entry     = settings.urbanNature[valid[pick]];
            Sprite daySprite = entry.daySprite;
            float  scale  = Random.Range(sr.x, sr.y);

            // Yoldan uzak dur (tam sprite footprint + clearance tampon).
            if (SpriteOverlapsRoad(tx, ty, daySprite, scale, roadClearTiles)) continue;

            float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
            float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

            float treeRadius = ComputeBuildingRadius(daySprite, scale, 0f);

            // Binaların GERÇEK gövdesinden uzak dur (şişirilmiş yerleşim yarıçapından değil).
            if (NearBuilding(wx, wy, treeRadius + clearWorld)) continue;

            // Diğer ağaçlara karşı sprite-boyutuna göre aralık (±%30 jitter → ızgara değil).
            float mySpacing = treeRadius * packFactor * Random.Range(0.7f, 1.3f);
            if (TreeOverlap(wx, wy, mySpacing)) continue;
            AddTree(wx, wy, mySpacing);

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
                isTree        = true,
                spriteIndex   = valid[pick],
                brokenIndex   = -1,
                baseAlpha     = baseA,
                baseScale     = scale, // zoom LOD ölçek telafisi bu orijinal ölçeğe göre uygulanır
            });
            spriteCounts[pick]++;
            placed++;
        }

        Debug.Log($"MapDecorPlacer: urban nature (ağaç) — tiles={order.Count}, placed={placed}, " +
                  $"coverage={coverage:F2}→thr={thr:F2}, forestSize={forestScale:F0}, " +
                  $"treeDensity={urbanTreeDensity:F2} (pack={packFactor:F2}), buildings={bFoot.Count}");
    }
}
