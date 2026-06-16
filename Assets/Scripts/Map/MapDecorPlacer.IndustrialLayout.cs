using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Industrial layout: factories / industrial buildings packed into vertical lanes
// (columns) separated by aisles, for a dense industrial-district look. Lanes share a
// slight tilt and gently wander (Perlin) so they read as parallel-but-natural rather
// than ruler-straight. Runs over the industrial biome (biome 3) tile pool. Buildings
// reuse the city-building machinery (CreateCityBuildingObject) so they get day/night
// crossfade + dynamic shadows. All tuning is serialized on MapDecorPlacer.
public partial class MapDecorPlacer
{
    // -------------------------------------------------------------------------
    // INDUSTRIAL LANE FILL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sanayi bölgesini (biome 3) DİKEY lane'ler halinde doldurur. Her lane içinde binalar
    /// aşağıdan yukarı üst üste (footprint yüksekliği + küçük boşluk kadar ilerleyerek)
    /// paketlenir; lane'ler arasında aisle (yol/boşluk) bırakılır. Tüm lane'ler ortak bir
    /// açıyla hafifçe yatar ve Perlin gürültüsüyle yumuşakça kıvrılır → cetvel gibi düz değil,
    /// paralel ama doğal bir sanayi-sokağı görünümü. Tüm ayarlar MapDecorPlacer'da serileştirilir.
    /// </summary>
    void PlaceIndustrialLayout(MapGenerator map, BiomePaintSettings settings,
                               List<Vector2Int> tiles, float halfW, float halfH)
    {
        if (settings.industrialBuildings == null || settings.industrialBuildings.Count == 0)
        {
            Debug.Log("MapDecorPlacer: industrial layout atlandı — industrialBuildings boş.");
            return;
        }
        if (tiles == null || tiles.Count == 0) return;

        // null sprite'lı entry'leri ele — geçerli indeksleri topla (dengeli seçim için)
        var valid = new List<int>();
        for (int i = 0; i < settings.industrialBuildings.Count; i++)
            if (settings.industrialBuildings[i].daySprite != null)
                valid.Add(i);
        if (valid.Count == 0)
        {
            Debug.LogWarning("MapDecorPlacer: industrial layout — hiçbir industrialBuildings entry'sinde daySprite yok.");
            return;
        }

        bool hasRoads = RoadGenerator.Instance != null && RoadGenerator.Instance.IsGenerated;

        // Hızlı tile lookup + bölge bounding box
        var tileSet = new HashSet<Vector2Int>(tiles);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t.x < minX) minX = t.x;
            if (t.x > maxX) maxX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.y > maxY) maxY = t.y;
        }

        Vector2 sr     = industrialScaleRange == Vector2.zero ? new Vector2(0.5f, 0.8f) : industrialScaleRange;
        int columns    = Mathf.Max(1, industrialLaneDepth);             // bir stripe kaç bina KOLONU geniş
        int gap        = Mathf.Max(0, industrialBuildingGap);
        float density  = industrialFillDensity > 0f ? industrialFillDensity : 1f;
        // Sıkılık çarpanı: lane İÇİ kolon adımını (yatay) ve dikey ilerlemeyi birlikte küçültür →
        // binalar stripe içinde her iki eksende de birbirine yanaşır, hatta hafifçe örtüşür →
        // yoğun, sıkışık sanayi görünümü. DİKKAT: lane'ler arası mesafeyi (aisle) ETKİLEMEZ.
        float packing  = Mathf.Clamp(industrialPacking, 0.3f, 1f);

        // --- Birim uyumu: spacing'i TILE değil, BİNA AYAK İZİ cinsinden ölç --------------------
        // Bir bina ~rect.height*scale tile yüksekliğinde (PPU=100, sprite PPU=100). Tile cinsinden
        // laneWidth=4 gibi değerler bir binanın onda biri kadar kalıyordu; bu yüzden temsili bir
        // ayak izi (fpW/fpH) hesaplayıp tüm geometriyi onun katları olarak kuruyoruz.
        float meanScale = (sr.x + sr.y) * 0.5f;
        float fpW = 0f, fpH = 0f;
        for (int i = 0; i < valid.Count; i++)
        {
            var s = settings.industrialBuildings[valid[i]].daySprite;
            fpW += s.rect.width  * meanScale;
            fpH += s.rect.height * meanScale;
        }
        fpW /= valid.Count;  // ortalama bina genişliği (tile)
        fpH /= valid.Count;  // ortalama bina yüksekliği (tile)

        float colStep  = Mathf.Max(1f, fpW * packing);                  // stripe içi kolonlar arası yatay adım
        float laneSpan = columns * colStep;                             // stripe genişliği (tile)
        // Aisle: bina genişliği KATI olarak boşluk (örn. 1.5 = ~1.5 bina genişliği boş koridor).
        float aisleSpan = Mathf.Max(0f, industrialAisleSpacing) * fpW;
        float stride    = Mathf.Max(1f, laneSpan + aisleSpan);          // lane merkezleri arası mesafe — packing'den BAĞIMSIZ

        // Hafif eğim: tüm lane'ler ortak bir açıyla yatar → paralel ama düz değil "sanayi" hissi.
        // tiltSlope, Y boyunca her tile için kolon X'inin kaç tile kayacağı (tan(açı)).
        float tiltSlope = Mathf.Tan(Mathf.Clamp(industrialLaneTiltDegrees, -45f, 45f) * Mathf.Deg2Rad);

        // Organik kıvrım: tüm stripe bir Perlin omurgasıyla birlikte dalgalanır (kolonlar bir arada
        // kalır). Genlik/frekans ve bina jitter'ı tamamen inspector'dan ayarlanır.
        float wobbleAmp  = Mathf.Max(0f, industrialLaneWobble);         // tile
        float wobbleFreq = Mathf.Max(0.0001f, industrialLaneWobbleFrequency);
        float jitterAmp  = Mathf.Max(0f, industrialBuildingJitter);    // tile

        int placed = 0, lanes = 0;
        int[] spriteCounts = new int[valid.Count];

        // Lane'leri kenara hizalı göstermemek için band başlangıcını rastgele kaydır.
        float startX = minX + Random.value * stride;

        int laneIndex = 0;
        for (float laneBaseX = startX; laneBaseX <= maxX; laneBaseX += stride, laneIndex++)
        {
            // Her lane'e ayrı bir gürültü fazı ver → komşu lane'ler aynı şekilde kıvrılmasın.
            float noisePhase = laneIndex * 0.37f + Random.value * 0.13f;
            bool anyInLane = false;

            // Stripe genişliği boyunca KOLONLAR: her kolon ayrı bir dikey bina dizisi.
            for (int c = 0; c < columns; c++)
            {
                float colX = laneBaseX + c * colStep;

                // Kolon boyunca aşağıdan yukarı süpür, binaları üst üste (dikey) paketle.
                int y = minY;
                while (y <= maxY)
                {
                    int pick      = PickBalancedSpriteIndex(spriteCounts);
                    var entry     = settings.industrialBuildings[valid[pick]];
                    Sprite daySprite = entry.daySprite;

                    float scale = Random.Range(sr.x, sr.y);

                    // Bina footprint yüksekliği (tile). 1 tile = 1/pixelsPerUnit dünya birimi,
                    // yani tile yüksekliği = sprite piksel yüksekliği × scale.
                    int hTiles  = Mathf.Max(1, Mathf.RoundToInt(daySprite.rect.height * scale));
                    int advance = Mathf.Max(1, Mathf.RoundToInt(hTiles * packing) + gap);

                    // Stripe omurgasının bu Y'deki kayması: eğim + ortak organik kıvrım. Jitter binaya özel.
                    float wobble = wobbleAmp > 0f
                        ? (Mathf.PerlinNoise(noisePhase, y * wobbleFreq) - 0.5f) * 2f * wobbleAmp
                        : 0f;
                    float jitter = jitterAmp > 0f ? (Random.value - 0.5f) * 2f * jitterAmp : 0f;

                    int tx = Mathf.RoundToInt(colX + tiltSlope * (y - minY) + wobble + jitter);
                    int ty = y + hTiles / 2;

                    // Bu hücre sanayi bölgesinde değilse: tek tile ilerle (kolon boşlukta devam etsin).
                    if (!tileSet.Contains(new Vector2Int(tx, ty)) ||
                        !map.IsLand(tx, ty) || map.GetBiome(tx, ty) != 3)
                    {
                        y += 1;
                        continue;
                    }

                    if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) { y += advance; continue; }

                    // Yoğunluk: bazı slotları boş bırakarak tam dolu görünümü kır.
                    if (Random.value > density) { y += advance; continue; }

                    // Yol kontrolü — special binalar gibi kaydırma yok, sadece reject.
                    if (hasRoads && SpriteOverlapsRoad(tx, ty, daySprite, scale, cityMinRoadDistance))
                    {
                        y += advance;
                        continue;
                    }

                    // Clearance'ı bina YÜKSEKLİK çemberine değil, packing GRID adımına bağla → kolonlar
                    // omuz omuza paketlenir, dikey komşular da packing kadar yaklaşır/örtüşür. min(colStep,
                    // advance)*0.45 → grid komşuları her zaman kabul, daha sıkışıklar (gross overlap) reddedilir.
                    // industrialOverlapRadius taban (dünya birimi) olarak kalır.
                    float stepTiles = Mathf.Min(colStep, advance);
                    float effRadius = Mathf.Max(industrialOverlapRadius, (stepTiles * 0.45f) / pixelsPerUnit);
                    float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
                    float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

                    if (IsDenseOverlapping(wx, wy, effRadius)) { y += advance; continue; }
                    denseOccupied.Add(new Vector3(wx, wy, effRadius));

                    float baseA   = 1f;
                    int sortOrder = 10 + (int)(wy * -100f);

                    var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
                        daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder, entry.isIsometric);
                    go.name = "IndustrialBuilding";

                    // Animasyonlu sanayi binası ise frame döngüsünü tak (gündüz + varsa gece).
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
                    anyInLane = true;

                    y += advance;
                }
            }

            if (anyInLane) lanes++;
        }

        Debug.Log($"MapDecorPlacer: industrial layout — lanes={lanes}, placed={placed}, " +
                  $"columns/lane={columns}, colStep={colStep:F1}, laneSpan={laneSpan:F1}, aisleSpan={aisleSpan:F1}, " +
                  $"stride={stride:F1}, fp={fpW:F0}x{fpH:F0}, gap={gap}, packing={packing:F2}, tilt={industrialLaneTiltDegrees}°, " +
                  $"wobble={wobbleAmp:F1}/{wobbleFreq:F3}, jitter={jitterAmp:F1}");
    }
}
