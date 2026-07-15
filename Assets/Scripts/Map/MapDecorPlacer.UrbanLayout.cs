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

    [Tooltip("Bölgesel bina kontrastı (0..1). 0 = binalar tüm urban araziye eşit serpilir (eski davranış); " +
             "1 = binalar yerleşim öbeklerinde toplanır ve geniş bölgeler TAMAMEN boş (binasız) kalır. " +
             "Orman bölge gürültüsünden bağımsız → boş çayırlar, ormanlık ve yerleşim bölgeleri karışır.")]
    [Range(0f, 1f)] public float urbanBuildingRegionVariation = 0.6f;

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

    [Tooltip("Urban arazinin ne kadarı ÇIPLAK (tamamen ağaçsız) kalsın (0..1). Çıplak bölgeler orman " +
             "yoğunluğunu düşürmez — o bölgede orman HİÇ oluşmaz. 0 = çıplak bölge yok.")]
    [Range(0f, 1f)] public float urbanBareFraction = 0.35f;

    [Tooltip("Çıplak bölgelerin BÜYÜKLÜĞÜ (tile, dalga boyu). Orman öbek boyutundan çok daha büyük " +
             "tutun ki çıplak alanlar sıradan orman açıklığı gibi değil, dev yalın kesitler gibi " +
             "okunsun. Orman boyutunun 6–10 katı iyi başlangıçtır.")]
    [Range(50f, 1000f)] public float urbanBareRegionSizeTiles = 350f;

    [Tooltip("Orman yoğunluğunun TÜM sınırlara doğru sönme bandı (tile): harita kenarı, başka " +
             "biyom/su sınırı, yollar ve çıplak bölgeler. Bir sınıra bu mesafeden yakın ormanlar " +
             "kademeli seyrekleşir → orman hiçbir yerde duvar gibi bitmez, kenara doğru cılızlaşıp " +
             "dağılır. Yollar haritayı kestiği için ÇOK büyük değerler ormanı boğar; 10–25 idealdir. " +
             "0 = kapalı.")]
    [Range(0f, 200f)] public float urbanForestEdgeFadeTiles = 18f;

    [Tooltip("Ağaçların bina/yol kenarından koruyacağı boşluk (TILE). Ağacın GERÇEK sprite tabanına göre " +
             "ölçülür (binaların şişirilmiş yerleşim yarıçapına DEĞİL) → küçük değerler yeterlidir. 2–5.")]
    [Range(0f, 12f)] public float urbanTreeClearanceTiles = 3f;

    // -------------------------------------------------------------------------
    // NOISE HELPERS
    // -------------------------------------------------------------------------

    // GLSL tarzı eşik smoothstep'i: x, [edge0..edge1] bandında 0→1 yumuşak rampalanır.
    // DİKKAT: Unity'nin Mathf.SmoothStep(from, to, t)'si BUNUN TERSİDİR — from ile to ARASINDA
    // interpolasyon yapar (t parametre). Eşikleme için onu kullanmak yoğunluğu [thr..thr+edge]
    // bandına sıkıştırıp (asla 0/1 olmaz) tüm orman mantığını sessizce bozuyordu.
    static float ThresholdStep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / Mathf.Max(1e-5f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

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

        // Bölgesel yerleşim gürültüsü: makro Perlin bir "yerleşim uygunluğu" haritası verir; düşük
        // bölgelerde bina hiç konmaz → urban arazinin bir kısmı gerçekten BOŞ kalır, binalar köy/kasaba
        // öbekleri halinde toplanır. Orman bölge gürültüsünden bağımsız seed → boş çayır, ormanlık ve
        // yerleşim bölgeleri doğal biçimde karışır. cutoff bVar ile büyür: 0 = eski eşit serpme.
        float bVar        = Mathf.Clamp01(urbanBuildingRegionVariation);
        float bRegionScale = Mathf.Max(6f, urbanForestSizeTiles) * 3f;
        float bSeedX = Random.Range(0f, 500f);
        float bSeedY = Random.Range(0f, 500f);
        float bCutoff = 0.45f * bVar;

        int placed = 0;
        int[] spriteCounts = new int[valid.Count];

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2Int t = tiles[Random.Range(0, tiles.Count)];
            int tx = t.x, ty = t.y;

            if (!map.IsLand(tx, ty)) continue;
            if (map.GetBiome(tx, ty) != 1) continue;
            if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) continue;

            if (bVar > 0f)
            {
                float m = Mathf.PerlinNoise(bSeedX + tx / bRegionScale, bSeedY + ty / bRegionScale);
                // cutoff altı = bölge boş; dar smoothstep bandı yerleşim kenarını yumuşatır.
                float keep = ThresholdStep(bCutoff - 0.08f, bCutoff + 0.08f, m);
                if (keep <= 0f || (keep < 1f && Random.value > keep)) continue;
            }

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
        // (coverage 0 = az orman, 1 = neredeyse her yer). Eşik üstü DEĞER olarak döner (0..1): geniş
        // kenar bandı sayesinde orman çekirdeği dolgun, kenarlar/ara bölgeler kademeli seyrelir —
        // yoğunluk artık ikili (var/yok) değil, sürekli.
        float forestScale = Mathf.Max(6f, urbanForestSizeTiles);
        float coverage    = Mathf.Clamp01(urbanForestCoverage);
        float thr         = Mathf.Lerp(0.72f, 0.14f, coverage);
        const float edge  = 0.12f;
        float nSeedX = Random.Range(0f, 500f);
        float nSeedY = Random.Range(0f, 500f);

        // ÇIPLAK BÖLGELER: orman öbek boyutundan ÇOK daha büyük dalga boylu (urbanBareRegionSizeTiles)
        // ayrı bir makro gürültü haritayı az sayıda dev bölgeye ayırır. Cutoff altı = ÇIPLAK: o bölgede
        // orman hiç oluşmaz (yoğunluk düşürme DEĞİL — binary kapı). Cutoff üstü = normal orman bölgesi;
        // oradaki ormanlar tam dolgunlukta kalır.
        float bareFrac = Mathf.Clamp01(urbanBareFraction);

        // Dalga boyunu urban bölgenin GERÇEK boyutuna kıstır: kullanıcı ayarı bölgeden büyükse Perlin
        // bölge boyunca neredeyse sabit kalır → tüm bölge cutoff'un tek tarafına düşer ve hiç çıplak
        // alan çıkmaz (ya da her yer çıplak olur). Extent*0.5 ile bölgede en az ~2 dalga garanti.
        int bMinX = int.MaxValue, bMaxX = int.MinValue, bMinY = int.MaxValue, bMaxY = int.MinValue;
        for (int i = 0; i < tiles.Count; i++)
        {
            if (tiles[i].x < bMinX) bMinX = tiles[i].x;
            if (tiles[i].x > bMaxX) bMaxX = tiles[i].x;
            if (tiles[i].y < bMinY) bMinY = tiles[i].y;
            if (tiles[i].y > bMaxY) bMaxY = tiles[i].y;
        }
        float extent    = Mathf.Max(bMaxX - bMinX, bMaxY - bMinY);
        float bareScale = Mathf.Min(Mathf.Max(50f, urbanBareRegionSizeTiles),
                                    Mathf.Max(50f, extent * 0.5f));

        float BareNoiseAt(int fx, int fy)
            => Mathf.PerlinNoise(nSeedX + fx / bareScale + 113f, nSeedY + fy / bareScale + 113f);

        // Cutoff = gürültünün URBAN TILE'LAR üZERİNDEKİ gerçek quantile'ı → bareFrac kadar arazi
        // KESİN olarak çıplak kalır. Sabit eşik (dağılım tahmini) dalga boyu/bölge şekline göre
        // ıskalıyordu; quantile bunu garantiler.
        float bareCut = float.NegativeInfinity;
        if (bareFrac > 0f)
        {
            var bareVals = new List<float>(tiles.Count);
            for (int i = 0; i < tiles.Count; i++)
                bareVals.Add(BareNoiseAt(tiles[i].x, tiles[i].y));
            bareVals.Sort();
            int q = Mathf.Clamp(Mathf.RoundToInt((bareVals.Count - 1) * bareFrac), 0, bareVals.Count - 1);
            bareCut = bareVals[q];
        }

        // İç yoğunluk katmanı: hafif dalgalanma (0.75..1) — orman dolgun kalır ama tekdüze de olmaz.
        float densScale = forestScale * 1.7f;

        // -- SINIR SÖNME MESAFE ALANI (çok kaynaklı BFS) ----------------------------------------
        // Orman yoğunluğu HER sınıra doğru söner: harita kenarı, başka biyom/su, yollar ve çıplak
        // bölgeler. Tüm "orman dışı" tile'lar 0 mesafeyle BFS kaynağıdır (harita kenarındaki açık
        // tile'lar 1 ile tohumlanır); Chebyshev (8 komşu) dalga ile tile-uzaklık alanı kurulur.
        // ForestDensityAt yoğunluğu SmoothStep(0, fade, dist) ile çarpar → orman hiçbir sınırda
        // duvar gibi bitmez. Mesafeler fade tavanında kesilir → BFS maliyeti sınırlı kalır.
        float edgeFade = Mathf.Max(0f, urbanForestEdgeFadeTiles);
        int mapW = map.width, mapH = map.height;
        ushort[] borderDist = null;
        if (edgeFade > 0f)
        {
            bool hasRoadsN = RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated;
            ushort far = (ushort)(Mathf.CeilToInt(edgeFade) + 1);
            borderDist = new ushort[mapW * mapH];
            var bfs = new Queue<int>();

            for (int y = 0; y < mapH; y++)
            for (int x = 0; x < mapW; x++)
            {
                int i = y * mapW + x;
                // Yol için GÖRSEL kenar (boyanan piksel) baz alınır — IsRoad yalnızca 1-tile
                // centerline işaretler, mesafe yol ortasından ölçülüp bandın yarısını yutuyordu.
                bool blocked = !map.IsLand(x, y) || map.GetBiome(x, y) != 1
                               || (hasRoadsN && RoadGenerator.Instance.GetDistanceToRoadEdge(x, y) == 0)
                               || BareNoiseAt(x, y) < bareCut;
                if (blocked)
                {
                    borderDist[i] = 0; bfs.Enqueue(i);
                }
                else if (x == 0 || y == 0 || x == mapW - 1 || y == mapH - 1)
                {
                    borderDist[i] = 1; bfs.Enqueue(i); // harita kenarı da sınırdır
                }
                else borderDist[i] = far;
            }

            while (bfs.Count > 0)
            {
                int i = bfs.Dequeue();
                int cx = i % mapW, cy = i / mapW;
                int nd = borderDist[i] + 1;
                if (nd >= far) continue;
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int nx = cx + ox, ny = cy + oy;
                    if (nx < 0 || ny < 0 || nx >= mapW || ny >= mapH) continue;
                    int ni = ny * mapW + nx;
                    if (borderDist[ni] <= nd) continue;
                    borderDist[ni] = (ushort)nd;
                    bfs.Enqueue(ni);
                }
            }
        }

        float ForestDensityAt(int fx, int fy)
        {
            // Çıplak bölge kapısı önce — kesilen bölgede orman gürültüsü örneklemeye gerek yok.
            if (BareNoiseAt(fx, fy) < bareCut) return 0f;

            float lo = Mathf.PerlinNoise(nSeedX + fx / forestScale,          nSeedY + fy / forestScale);
            float hi = Mathf.PerlinNoise(nSeedX + fx / (forestScale * 0.45f) + 37f,
                                         nSeedY + fy / (forestScale * 0.45f) + 37f);
            float n = lo * 0.7f + hi * 0.3f;

            float dens = Mathf.PerlinNoise(nSeedX + fx / densScale + 271f,
                                           nSeedY + fy / densScale + 271f);
            dens = Mathf.Lerp(0.75f, 1f, Mathf.Clamp01(dens));

            float d = ThresholdStep(thr, thr + edge, n) * dens;

            // En yakın sınıra (biyom/su/yol/çıplak bölge/harita kenarı) uzaklıkla sön.
            if (borderDist != null)
                d *= ThresholdStep(0f, edgeFade, borderDist[fy * mapW + fx]);

            return d;
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
        // 1.3 = jitter üst sınırı, 3.5 = seyrek bölge aralık çarpanı (aşağıdaki mySpacing ile eşleşmeli).
        float maxTreeSpacing = Mathf.Max(0.02f, maxTreeR * packFactor * 1.3f * 3.5f);

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
            // Kıyı tamponu — binalarla aynı kural: ağaçlar kıyıya/sahile taşmasın.
            if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) continue;

            float forestP = ForestDensityAt(tx, ty);
            if (forestP <= 0.001f) continue;                                 // açıklık
            // Kabul olasılığı forestP² — lineer olasılık aralık (spacing) kısıtı tarafından
            // yutuluyordu: ağaç tile'dan çok büyük olduğu için adayların yarısı elense de her
            // aralık yuvası yine doluyordu. Kare düşüş fade bandını görünür kılar.
            if (forestP < 1f && Random.value > forestP * forestP) continue;  // yumuşak orman kenarı

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
            // Yoğunluk düştükçe aralık GÜÇLÜ biçimde büyür — fade'i asıl taşıyan bu çarpandır
            // (olasılık elemesi tek başına aralık kısıtınca yutulur, bkz. forestP² yorumu).
            float mySpacing = treeRadius * packFactor
                            * Mathf.Lerp(3.5f, 1f, forestP)
                            * Random.Range(0.7f, 1.3f);
            if (TreeOverlap(wx, wy, mySpacing)) continue;
            AddTree(wx, wy, mySpacing);

            float baseA   = 1f;
            int sortOrder = 10 + (int)(wy * -100f);

            // Ağaç gölgeleri düşük detaylı trace materyali kullanır (treeShadowTraceSteps) —
            // binlerce ağaçta asıl gölge fragment maliyeti buradan geliyordu.
            var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
                daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder, entry.isIsometric,
                lowDetailShadow: true);
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
                  $"bare={bareFrac:F2} (scale={bareScale:F0}, extent={extent:F0}), " +
                  $"treeDensity={urbanTreeDensity:F2} (pack={packFactor:F2}), buildings={bFoot.Count}");
    }
}
