using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// City layout: city hall, layer classification, per-style layer fills, special
// buildings, and nature decor placement.
public partial class MapDecorPlacer
{
    // -------------------------------------------------------------------------
    // PLACEMENT — CITY HALL
    // -------------------------------------------------------------------------

    Vector2Int FindCityHallTile(List<Vector2Int> allCityTiles)
    {
        if (allCityTiles.Count == 0) return new Vector2Int(-1, -1);

        bool hasRoads = RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated;

        // Yoldan cityHallClearingRadius kadar uzak tile'ları al
        // Azalan eşikle dene — hiç bulunamazsa en yakın olanla devam et
        List<Vector2Int> candidates = null;
        int[] thresholds = { cityHallClearingRadius, cityHallClearingRadius / 2, cityMinRoadDistance };
        foreach (int thresh in thresholds)
        {
            var filtered = new List<Vector2Int>();
            for (int i = 0; i < allCityTiles.Count; i++)
            {
                if (hasRoads && RoadGenerator.Instance.GetDistanceToRoadEdge(allCityTiles[i].x, allCityTiles[i].y) < thresh)
                    continue;
                filtered.Add(allCityTiles[i]);
            }
            if (filtered.Count > 0) { candidates = filtered; break; }
        }
        if (candidates == null || candidates.Count == 0) candidates = allCityTiles;

        // Candidate tile'ları hızlı lookup için set'e al
        var candidateSet = new HashSet<Vector2Int>(candidates);

        // 4-bağlantılı cluster'lara ayır — yol tarafından bölünen parçaları ayırt et
        var visited = new HashSet<Vector2Int>();
        var clusters = new List<List<Vector2Int>>();
        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };

        foreach (var seed in candidates)
        {
            if (visited.Contains(seed)) continue;
            var cluster = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(seed);
            visited.Add(seed);
            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                cluster.Add(pos);
                for (int i = 0; i < 4; i++)
                {
                    var nb = new Vector2Int(pos.x + dx4[i], pos.y + dy4[i]);
                    if (!visited.Contains(nb) && candidateSet.Contains(nb))
                    {
                        visited.Add(nb);
                        queue.Enqueue(nb);
                    }
                }
            }
            clusters.Add(cluster);
        }

        // En büyük cluster'ı seç
        List<Vector2Int> best = clusters[0];
        for (int i = 1; i < clusters.Count; i++)
            if (clusters[i].Count > best.Count) best = clusters[i];

        // En büyük cluster'ın centroid'i
        long sumX = 0, sumY = 0;
        for (int i = 0; i < best.Count; i++) { sumX += best[i].x; sumY += best[i].y; }
        float cx = (float)sumX / best.Count;
        float cy = (float)sumY / best.Count;

        // Centroid'e en yakın tile
        float bestDist = float.MaxValue;
        Vector2Int result = best[0];
        for (int i = 0; i < best.Count; i++)
        {
            float ddx = best[i].x - cx, ddy = best[i].y - cy;
            float d = ddx * ddx + ddy * ddy;
            if (d < bestDist) { bestDist = d; result = best[i]; }
        }
        return result;
    }

    void TryPlaceCityHall(MapGenerator map, BiomePaintSettings settings,
                          Vector2Int tile, float halfW, float halfH)
    {
        Sprite daySprite = settings.cityHallEntry.daySprite;
        if (daySprite == null)
        {
            Debug.LogWarning("MapDecorPlacer: cityHallEntry.daySprite atanmamış — belediye binası çizilemez.");
            return;
        }

        float wx = transform.position.x + (tile.x / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (tile.y / pixelsPerUnit) - halfH;

        float scale = Random.Range(cityHallScaleRange.x, cityHallScaleRange.y);
        float baseA = 1f;
        int sortOrder = 20 + (int)(wy * -100f);

        var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
            daySprite, settings.cityHallEntry.nightSprite, wx, wy, scale, baseA, sortOrder,
            settings.cityHallEntry.isIsometric);

        go.name = "CityHall";

        // Clearing radius kadar alanı işaretle
        float clearWorld = cityHallClearingRadius / pixelsPerUnit;
        AddDense(wx, wy, clearWorld);
        occupiedCenters.Add(new Vector2(wx, wy));

        decorObjects.Add(go);
        cityBuildings.Add(new BuildingData
        {
            go            = go,
            dayRenderer   = daySR,
            nightRenderer = nightSR,
            shadow        = shadow,
            tileX         = tile.x,
            tileY         = tile.y,
            isBroken      = false,
            isSpecial     = true,
            spriteIndex   = -1,
            brokenIndex   = -1,
            baseAlpha     = baseA,
        });
    }

    // -------------------------------------------------------------------------
    // LAYER CLASSIFICATION — Belediyeden uzaklığa göre tile'ları katmanlara ayır
    // -------------------------------------------------------------------------

    List<List<Vector2Int>> ClassifyLayers(List<Vector2Int> allTiles, Vector2Int hallTile,
                                          List<CityLayer> layers)
    {
        var result = new List<List<Vector2Int>>(layers.Count);
        for (int i = 0; i < layers.Count; i++)
            result.Add(new List<Vector2Int>());

        if (layers.Count == 0) return result;

        float clearRadius = cityHallClearingRadius;

        // Katmanlar ÖZEL DEĞİL — iç içe geçer ve blend olur. Her tile, kendi
        // [innerRadius, outerRadius) aralığına giren HER katmanın havuzuna eklenir.
        // Böylece innerRadius=0 olan bir katman (ör. Neighbourhoods) merkeze kadar
        // tile alır; başka bir katman (Towers/Buildings) aynı bölgeyi kapsasa bile.
        // Fiziksel çakışma FillLayer içindeki IsDenseOverlapping ile zaten engellenir.
        for (int t = 0; t < allTiles.Count; t++)
        {
            Vector2Int tile = allTiles[t];
            float dist = Vector2Int.Distance(tile, hallTile);

            if (hallTile.x >= 0 && dist <= clearRadius) continue;

            for (int i = 0; i < layers.Count; i++)
            {
                CityLayer layer = layers[i];

                // extendToEdge (catch-all): outerRadius <= 0 ise kenara kadar uzanır;
                // outerRadius > 0 ise o yarıçapla kesilir (şehir adaya yayılmaz).
                float outer = (layer.extendToEdge && layer.outerRadius <= 0f)
                    ? float.MaxValue
                    : layer.outerRadius;

                if (dist >= layer.innerRadius && dist < outer)
                    result[i].Add(tile);
            }
        }

        for (int i = 0; i < layers.Count; i++)
            Debug.Log($"MapDecorPlacer: katman[{i}] '{layers[i].name}' — {result[i].Count} tile");

        return result;
    }

    // -------------------------------------------------------------------------
    // GRID LAYER FILL — Jittered grid doldurma (Grid stili)
    // -------------------------------------------------------------------------

    void FillLayerGrid(MapGenerator map, CityLayer layer, List<Vector2Int> pool,
                       float halfW, float halfH)
    {
        if (layer.sprites == null || layer.sprites.Count == 0) return;
        if (pool.Count == 0) return;

        int step = Mathf.Max(1, layer.gridStep);
        // Güvenlik ağı: inspector "+" ile sıfırlanmış katmanlar görünmez/boş kalmasın
        Vector2 sr   = layer.scaleRange == Vector2.zero ? new Vector2(0.5f, 0.7f) : layer.scaleRange;
        float density = layer.fillDensity > 0f ? layer.fillDensity : 1f;
        var poolSet = new HashSet<Vector2Int>(pool);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < pool.Count; i++)
        {
            var t = pool[i];
            if (t.x < minX) minX = t.x;
            if (t.x > maxX) maxX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.y > maxY) maxY = t.y;
        }

        int placed = 0;
        int jitter  = Mathf.Max(1, step / 2);
        int[] spriteCounts = new int[layer.sprites.Count];

        // Grid hücrelerini topla ve karıştır — sütun düzeni süpürmesinden doğan hizalama
        // yanlılığını kır (ızgara aralığı 'step'ten geldiği için yapı korunur).
        var cells = new List<Vector2Int>();
        for (int gx = minX; gx <= maxX; gx += step)
        for (int gy = minY; gy <= maxY; gy += step)
            cells.Add(new Vector2Int(gx, gy));
        ShuffleInPlace(cells);

        for (int c = 0; c < cells.Count; c++)
        {
            int gx = cells[c].x, gy = cells[c].y;
            int jx = gx + Random.Range(-jitter, jitter + 1);
            int jy = gy + Random.Range(-jitter, jitter + 1);

            bool inPool = poolSet.Contains(new Vector2Int(jx, jy));
            if (!inPool)
            {
                int half = Mathf.Max(1, step / 2);
                for (int dx = -half; dx <= half && !inPool; dx += half)
                for (int dy = -half; dy <= half && !inPool; dy += half)
                {
                    if (poolSet.Contains(new Vector2Int(jx + dx, jy + dy)))
                        inPool = true;
                }
            }
            if (!inPool) continue;

            float effDensity = density * RadialDensityMultiplier(jx, jy, layer.thinning);
            if (Random.value > effDensity) continue;

            if (!map.IsLand(jx, jy)) continue;
            if (map.GetBiome(jx, jy) != 2) continue;
            if (cityShoreBuffer > 0 && !HasShoreBuffer(map, jx, jy)) continue;

            int spriteIdx = PickBalancedSpriteIndex(spriteCounts);
            var entry = layer.sprites[spriteIdx];
            Sprite daySprite = entry.daySprite;
            if (daySprite == null) continue;

            float scale = Random.Range(sr.x, sr.y);

            if (!TryFindRoadFreePosition(jx, jy, daySprite, scale, 8, cityMinRoadDistance, out int newJx, out int newJy))
                continue;
            if (newJx != jx || newJy != jy)
            {
                if (!map.IsLand(newJx, newJy)) continue;
                if (map.GetBiome(newJx, newJy) != 2) continue;
                if (cityShoreBuffer > 0 && !HasShoreBuffer(map, newJx, newJy)) continue;
            }
            jx = newJx; jy = newJy;

            float effRadius = ComputeBuildingRadius(daySprite, scale, layer.overlapRadius)
                              * RadialSpacingMultiplier(jx, jy, layer.thinning);
            float wx = transform.position.x + (jx / pixelsPerUnit) - halfW;
            float wy = transform.position.y + (jy / pixelsPerUnit) - halfH;

            if (IsDenseOverlapping(wx, wy, effRadius)) continue;
            AddDense(wx, wy, effRadius);

            float baseA   = 1f;
            int sortOrder = 10 + (int)(wy * -100f);

            var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
                daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder, entry.isIsometric);

            decorObjects.Add(go);
            cityBuildings.Add(new BuildingData
            {
                go            = go,
                dayRenderer   = daySR,
                nightRenderer = nightSR,
                shadow        = shadow,
                tileX         = jx,
                tileY         = jy,
                isBroken      = false,
                isSpecial     = false,
                spriteIndex   = spriteIdx,
                brokenIndex   = -1,
                baseAlpha     = baseA,
            });
            spriteCounts[spriteIdx]++;
            placed++;
        }

        Debug.Log($"MapDecorPlacer: grid layer '{layer.name}' step={step}, placed={placed}");
    }

    // -------------------------------------------------------------------------
    // MANHATTAN LAYER FILL — Belediye'den referansla ortogonal cadde/sokak ızgarası
    // -------------------------------------------------------------------------

    void FillLayerManhattan(MapGenerator map, CityLayer layer, Vector2Int hallTile,
                             List<Vector2Int> pool, float halfW, float halfH)
    {
        if (hallTile.x < 0) return;
        if (layer.sprites == null || layer.sprites.Count == 0) return;
        if (pool == null || pool.Count == 0) return;

        int avenueGap = Mathf.Max(2, layer.avenueSpacing);
        int streetGap = Mathf.Max(2, layer.streetSpacing);
        int maxStripWidth = Mathf.Max(0, Mathf.Min(avenueGap, streetGap) / 2 - 1);
        int stripWidth = Mathf.Clamp(layer.streetWidth, 0, maxStripWidth);
        // Güvenlik ağı: inspector "+" ile sıfırlanmış katmanlar görünmez/boş kalmasın
        Vector2 sr   = layer.scaleRange == Vector2.zero ? new Vector2(0.5f, 0.7f) : layer.scaleRange;
        float density = layer.fillDensity > 0f ? layer.fillDensity : 1f;

        int placed = 0;
        int streetSkipped = 0;
        int[] spriteCounts = new int[layer.sprites.Count];

        // Sütun düzeni yanlılığını kır — yerleşimi rastgele sırada dene (sokak ızgarası korunur).
        var shuffled = new List<Vector2Int>(pool);
        ShuffleInPlace(shuffled);

        for (int i = 0; i < shuffled.Count; i++)
        {
            Vector2Int t = shuffled[i];
            int tx = t.x, ty = t.y;

            // Avenue (dikey) veya street (yatay) şeridinde mi? → sokak olarak boş bırak
            int avMod = MathMod(tx - hallTile.x, avenueGap);
            int stMod = MathMod(ty - hallTile.y, streetGap);
            if (avMod < stripWidth || stMod < stripWidth)
            {
                streetSkipped++;
                continue;
            }

            float effDensity = density * RadialDensityMultiplier(tx, ty, layer.thinning);
            if (Random.value > effDensity) continue;

            if (!map.IsLand(tx, ty)) continue;
            if (map.GetBiome(tx, ty) != 2) continue;
            if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) continue;

            int spriteIdx = PickBalancedSpriteIndex(spriteCounts);
            var entry = layer.sprites[spriteIdx];
            Sprite daySprite = entry.daySprite;
            if (daySprite == null) continue;

            float scale = Random.Range(sr.x, sr.y);

            if (!TryFindRoadFreePosition(tx, ty, daySprite, scale, 8, cityMinRoadDistance, out int newTx, out int newTy))
                continue;
            if (newTx != tx || newTy != ty)
            {
                if (!map.IsLand(newTx, newTy)) continue;
                if (map.GetBiome(newTx, newTy) != 2) continue;
                if (cityShoreBuffer > 0 && !HasShoreBuffer(map, newTx, newTy)) continue;
            }
            tx = newTx; ty = newTy;

            float effRadius = ComputeBuildingRadius(daySprite, scale, layer.overlapRadius)
                              * RadialSpacingMultiplier(tx, ty, layer.thinning);
            float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
            float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

            if (IsDenseOverlapping(wx, wy, effRadius)) continue;
            AddDense(wx, wy, effRadius);

            float baseA   = 1f;
            int sortOrder = 10 + (int)(wy * -100f);

            var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
                daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder, entry.isIsometric);

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
                spriteIndex   = spriteIdx,
                brokenIndex   = -1,
                baseAlpha     = baseA,
            });
            spriteCounts[spriteIdx]++;
            placed++;
        }

        Debug.Log($"MapDecorPlacer: manhattan layer '{layer.name}' — placed={placed}, streetSkipped={streetSkipped}, " +
                  $"avenue={avenueGap}, street={streetGap}, stripWidth={stripWidth}");
    }

    // -------------------------------------------------------------------------
    // SCATTER LAYER FILL — Seyrek dağılım (Scatter stili)
    // -------------------------------------------------------------------------

    void FillLayerScatter(MapGenerator map, CityLayer layer, List<Vector2Int> pool,
                           float halfW, float halfH)
    {
        if (layer.sprites == null || layer.sprites.Count == 0) return;
        if (pool == null || pool.Count == 0) return;

        // Yoğunluk: fillDensity ana kontrol (0.1 seyrek … 1 yoğun), scatterRate fazladan
        // deneme çarpanı (boşlukları doldurur). Eski tamsayı bölmesi küçük havuzlarda 0
        // deneme üretiyordu — artık deneme sayısı havuz boyutuyla doğru orantılı.
        Vector2 sr = layer.scaleRange == Vector2.zero ? new Vector2(0.5f, 0.7f) : layer.scaleRange;
        float density = layer.fillDensity > 0f ? layer.fillDensity : 1f;
        int rate = Mathf.Clamp(layer.scatterRate, 1, 8);
        int attempts = Mathf.Max(1, Mathf.RoundToInt(pool.Count * density * rate));

        int placed = 0;
        int[] spriteCounts = new int[layer.sprites.Count];
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2Int t = pool[Random.Range(0, pool.Count)];
            int tx = t.x, ty = t.y;

            // Kenara doğru seyrelme — dış tile'larda yerleşme olasılığını düşür.
            if (Random.value > RadialDensityMultiplier(tx, ty, layer.thinning)) continue;

            if (!map.IsLand(tx, ty)) continue;
            if (map.GetBiome(tx, ty) != 2) continue;
            if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) continue;

            int spriteIdx = PickBalancedSpriteIndex(spriteCounts);
            var entry = layer.sprites[spriteIdx];
            Sprite daySprite = entry.daySprite;
            if (daySprite == null) continue;

            float scale = Random.Range(sr.x, sr.y);

            if (!TryFindRoadFreePosition(tx, ty, daySprite, scale, 8, cityMinRoadDistance, out int newTx, out int newTy))
                continue;
            if (newTx != tx || newTy != ty)
            {
                if (!map.IsLand(newTx, newTy)) continue;
                if (cityShoreBuffer > 0 && !HasShoreBuffer(map, newTx, newTy)) continue;
            }
            tx = newTx; ty = newTy;

            float effRadius = ComputeBuildingRadius(daySprite, scale, layer.overlapRadius)
                              * RadialSpacingMultiplier(tx, ty, layer.thinning);
            float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
            float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;
            if (IsDenseOverlapping(wx, wy, effRadius)) continue;
            AddDense(wx, wy, effRadius);

            float baseA   = 1f; // tüm binalar tam opak
            int sortOrder = 10 + (int)(wy * -100f);

            var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
                daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder, entry.isIsometric);

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
                spriteIndex   = spriteIdx,
                brokenIndex   = -1,
                baseAlpha     = baseA,
            });
            spriteCounts[spriteIdx]++;
            placed++;
        }

        Debug.Log($"MapDecorPlacer: scatter layer '{layer.name}' attempts={attempts}, placed={placed}");
    }

    // -------------------------------------------------------------------------
    // FILL LAYER DISPATCHER
    // -------------------------------------------------------------------------

    void FillLayer(MapGenerator map, BiomePaintSettings settings, CityLayer layer,
                   Vector2Int hallTile, List<Vector2Int> layerTiles, float halfW, float halfH)
    {
        if (layer.sprites == null || layer.sprites.Count == 0) return;
        if (layerTiles == null || layerTiles.Count == 0) return;

        switch (layer.style)
        {
            case CityLayerStyle.ManhattanGrid:
                FillLayerManhattan(map, layer, hallTile, layerTiles, halfW, halfH);
                break;
            case CityLayerStyle.Grid:
                FillLayerGrid(map, layer, layerTiles, halfW, halfH);
                break;
            case CityLayerStyle.Scatter:
                FillLayerScatter(map, layer, layerTiles, halfW, halfH);
                break;
        }
    }

    // -------------------------------------------------------------------------
    // SPECIAL CITY BUILDINGS — Zone bazlı özel binalar
    // -------------------------------------------------------------------------

    void PlaceSpecialCityBuildings(MapGenerator map, BiomePaintSettings settings,
                                   List<List<Vector2Int>> layerPools, int cityTileCount,
                                   float halfW, float halfH)
    {
        if (settings.specialCityBuildings == null) return;
        if (layerPools == null || layerPools.Count == 0) return;
        bool hasRoads = RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated;

        for (int s = 0; s < settings.specialCityBuildings.Count; s++)
        {
            var special = settings.specialCityBuildings[s];
            if (special.daySprite == null || special.count <= 0) continue;

            // Şehir boyutuna göre hedef sayı: büyük şehir → tam count, küçük şehir → rastgele.
            int targetCount = ResolveSpecialCount(special.count, cityTileCount);
            if (targetCount <= 0)
            {
                Debug.Log($"MapDecorPlacer: özel bina '{special.daySprite.name}' bu şehirde " +
                          $"üretilmedi (şehir küçük, cityTiles={cityTileCount}).");
                continue;
            }

            int layerIdx = Mathf.Clamp(special.targetLayer, 0, layerPools.Count - 1);
            List<Vector2Int> pool = layerPools[layerIdx];
            if (pool.Count == 0) continue;

            // Outskirts kuralı: özel bina, hedef katmandan daha İÇ katmanların (kule/orta bina)
            // kapsadığı çekirdeği dışlasın. Mahalle katmanı innerRadius=0 olduğundan havuzu
            // tüm şehri kaplar; iç katmanların en büyük outerRadius'unu çekirdek sınırı sayıp
            // yalnızca onun DIŞINDAki tile'lara yerleştiriyoruz → landmark'lar dış halkaya düşer.
            float coreRadius = 0f;
            for (int li = 0; li < layerIdx && li < settings.cityLayers.Count; li++)
                if (settings.cityLayers[li].outerRadius > coreRadius)
                    coreRadius = settings.cityLayers[li].outerRadius;

            if (coreRadius > 0f && cityHallTileCached.x >= 0)
            {
                var outskirts = new List<Vector2Int>(pool.Count);
                for (int p = 0; p < pool.Count; p++)
                    if (Vector2Int.Distance(pool[p], cityHallTileCached) >= coreRadius)
                        outskirts.Add(pool[p]);

                if (outskirts.Count > 0)
                    pool = outskirts;
                else
                    Debug.LogWarning($"MapDecorPlacer: özel bina '{special.daySprite.name}' — " +
                        $"çekirdek dışı (>= {coreRadius} tile) boş kaldı, tüm katman havuzuna düşülüyor.");
            }

            // Ölçek ve overlap hedef katmandan alınır
            CityLayer targetLayer = settings.cityLayers.Count > 0
                ? settings.cityLayers[Mathf.Clamp(special.targetLayer, 0, settings.cityLayers.Count - 1)]
                : null;
            Vector2 scaleRange = targetLayer != null ? targetLayer.scaleRange : new Vector2(0.40f, 0.55f);
            float overlapR     = targetLayer != null ? targetLayer.overlapRadius : 0.3f;

            int placed = 0;

            // Havuzu bir kez karıştırıp her tile'ı EN FAZLA bir kez dene. Rastgele-yerine-koymalı
            // örnekleme (Random.Range ile) geçerli tile azınlıktayken (yol/kıyı çoğu halkayı yerken)
            // aynı geçersiz tile'lara takılıp yerleşen=0 bırakabiliyordu; sıralı tarama, geçerli tek
            // bir tile bile varsa landmark'ı garanti yerleştirir.
            var candidates = new List<Vector2Int>(pool);
            ShuffleList(candidates);
            for (int c = 0; c < candidates.Count && placed < targetCount; c++)
            {
                Vector2Int tile = candidates[c];
                if (TryPlaceSpecialBuilding(map, special, tile.x, tile.y, scaleRange, overlapR, halfW, halfH))
                {
                    placed++;
                    if (special.connectToRoad && hasRoads)
                        RoadGenerator.Instance.ConnectBuildingToRoad(map, tile);
                }
            }

            if (placed < targetCount)
                Debug.LogWarning($"MapDecorPlacer: özel bina '{special.daySprite.name}' — " +
                                 $"hedef={targetCount} (max={special.count}), yerleşen={placed}");
        }
    }

    /// <summary>
    /// Özel bina sayısını şehir boyutuna göre çözer.
    /// Büyük şehir (cityTileCount >= specialBigCityTileCount) → tam maxCount üretilir.
    /// Daha küçük şehirlerde tavan şehir boyutuyla orantılı küçülür ve 0..tavan arası
    /// rastgele bir sayı seçilir — böylece maxCount=1 olan binalar küçük şehirlerde
    /// her zaman görünmez.
    /// </summary>
    int ResolveSpecialCount(int maxCount, int cityTileCount)
    {
        if (specialBigCityTileCount <= 0 || cityTileCount >= specialBigCityTileCount)
            return maxCount;

        float t   = Mathf.Clamp01((float)cityTileCount / specialBigCityTileCount);
        int   cap = Mathf.CeilToInt(maxCount * t);
        return Random.Range(0, cap + 1); // üst sınır hariç → 0..cap
    }

    bool TryPlaceSpecialBuilding(MapGenerator map, SpecialCityBuilding special,
                                 int tx, int ty, Vector2 scaleRange, float overlapR,
                                 float halfW, float halfH)
    {
        if (!map.IsLand(tx, ty)) return false;
        if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) return false;

        float scale = Random.Range(scaleRange.x, scaleRange.y);

        // Yol kontrolu — special icin kaydirma yok (yol konnektoru orijinal tile'a cekiliyor),
        // sadece reject. Caller havuzdan baska bir tile dener.
        if (SpriteOverlapsRoad(tx, ty, special.daySprite, scale, cityMinRoadDistance)) return false;

        float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

        // Özel binalar da normal binalarla AYNI dense sistemine girer (tower/building/neighbourhood
        // hepsi denseOccupied kullanır). Böylece hem mevcut binaların üstüne yerleşmez hem de
        // behind-clearance / sprite-yarıçapı mantığından faydalanır. Özel binalar katmanlardan
        // ÖNCE yerleştiği için kendilerini denseOccupied'a yazmak şart — yoksa sonradan dolan
        // tower katmanı bu binayı görmez ve üstüne biner.
        float effRadius = ComputeBuildingRadius(special.daySprite, scale, overlapR);
        if (IsDenseOverlapping(wx, wy, effRadius)) return false;

        float clearWorld = special.clearingRadius > 0 ? special.clearingRadius / pixelsPerUnit : 0f;
        AddDense(wx, wy, Mathf.Max(effRadius, clearWorld));
        occupiedCenters.Add(new Vector2(wx, wy));

        float baseA    = 1f; // tüm binalar tam opak
        int sortOrder  = 10 + (int)(wy * -100f);

        var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
            special.daySprite, special.nightSprite, wx, wy, scale, baseA, sortOrder, special.isIsometric);

        // Animasyonlu bina ise day renderer'a SpriteSheetAnimator tak
        if (special.isAnimated && special.animationFrames != null && special.animationFrames.Length > 1 && special.frameRate > 0f)
        {
            var animator = daySR.gameObject.AddComponent<SpriteSheetAnimator>();
            animator.frames = special.animationFrames;
            animator.frameRate = special.frameRate;
        }

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
            isSpecial     = true,
            spriteIndex   = -1,
            brokenIndex   = -1,
            baseAlpha     = baseA,
        });

        return true;
    }

    // -------------------------------------------------------------------------
    // PLACEMENT — NATURE DECOR
    // -------------------------------------------------------------------------

    void TryPlaceNatureDecor(MapGenerator map, BiomePaintSettings settings,
                             int biome, int tx, int ty, float halfW, float halfH)
    {
        Sprite sprite = PickDecorSprite(biome, settings);
        if (sprite == null) return;

        // Ekili tarla (crop field) ÜSTÜNE doğa dekoru koyma — tarlalar temiz kalmalı.
        if (cropFieldTiles.Count > 0 && cropFieldTiles.Contains(tx + ty * map.width)) return;

        float scale = Random.Range(spriteScaleRange.x, spriteScaleRange.y);

        // Yol üstüne/kenarına dekor koyma — sprite footprint'i yola değiyorsa ele.
        if (SpriteOverlapsRoad(tx, ty, sprite, scale, cityMinRoadDistance)) return;

        float wx    = transform.position.x + (tx / pixelsPerUnit) - halfW;
        float wy    = transform.position.y + (ty / pixelsPerUnit) - halfH;

        // Bina footprint'lerinin (denseOccupied) üstüne dekor koyma — doğa dekoru en son
        // çalıştığı için tüm bina/fabrika/tarım footprint'leri zaten kayıtlıdır.
        float radius = ComputeBuildingRadius(sprite, scale, 0f);
        if (IsDenseOverlapping(wx, wy, radius)) return;

        PlaceSimpleSprite("Decor", sprite, wx, wy, scale, Random.value > 0.5f, 2);
    }

    Sprite PickDecorSprite(int biome, BiomePaintSettings s)
    {
        List<Sprite> pool;
        switch (biome)
        {
            case 1: pool = s.agriculturalDecor; break;
            case 3: pool = s.industrialDecor;   break;
            case 4: pool = s.urbanDecor;        break;
            default: return null;
        }
        if (pool == null || pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }
}
