using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Industrial BUILDING placement over the industrial biome (biome 3). Buildings are LINED ALONG
// BOTH EDGES of the straight grid streets that RoadGenerator produces: walking each grid street
// (RoadGenerator.GetIndustrialGridPaths) and dropping buildings at building-footprint spacing,
// offset perpendicular to the street on each side. Buildings stay world-upright; only their
// position aligns to the roads. They reuse the city-building machinery (CreateCityBuildingObject)
// so they get day/night crossfade + dynamic shadows.
//
// NOTE: the industrial ROAD grid is generated separately in RoadGenerator (at initial road
// generation time). This file only places buildings; it never paints roads. Currently gated off
// by industrialPlaceBuildings while the road layout is being finalized.
public partial class MapDecorPlacer
{
    // -------------------------------------------------------------------------
    // INDUSTRIAL BLOCK GRID — serialized tuning (declared here; Unity serializes
    // public fields of a partial class regardless of which file they live in).
    // -------------------------------------------------------------------------

    [Header("Industrial Layout — Yol Kenari (Bina)")]
    [Tooltip("Sanayi binalarını yerleştir. Yol ızgarası RoadGenerator'da üretiliyor; bu sınıf yalnızca " +
             "bina koyar (yol BOYAMAZ). Binalar ızgara SOKAKLARININ iki kenarına dizilir. " +
             "Yollar oturana kadar KAPALI tutulabilir.")]
    public bool industrialPlaceBuildings = true;
    [Tooltip("Sokağın HER İKİ kenarına da diz (kapalıysa yalnızca bir kenar).")]
    public bool industrialLineBothSides = true;

    [Tooltip("Sanayi binalarının ölçek aralığı (min, max).")]
    public Vector2 industrialScaleRange = new Vector2(0.5f, 0.8f);

    // --- Sokağa dik yerleşim (yoldan uzaklık) ---
    [Tooltip("Bina ön cephesi ile yol KENARI arasında bırakılan boşluk (tile). Büyük = binalar yoldan geride.")]
    [Range(0, 8)] public int industrialRoadEdgeMargin = 1;
    [Tooltip("Sokağın her kenarına, yoldan içeri doğru kaç SIRA bina dizilsin.")]
    [Range(1, 4)] public int industrialRowsPerSide = 1;
    [Tooltip("Arka arkaya gelen bina sıraları arasındaki boşluk (tile). industrialRowsPerSide > 1 iken etkili.")]
    [Range(0, 12)] public int industrialRowSpacing = 1;

    // --- Sokak boyunca diziliş ---
    [Tooltip("Sokak boyunca bina adımı = ortalama bina genişliği × bu çarpan. " +
             "1 = bitişik, <1 = üst üste binerek sık, >1 = aralıklı.")]
    [Range(0.4f, 2.5f)] public float industrialAlongSpacing = 1f;
    [Tooltip("Her slotun dolma olasılığı. 1 = kesintisiz cephe, 0.5 = slotların yarısı boş (seyrek).")]
    [Range(0.1f, 1f)] public float industrialFillChance = 1f;
    [Tooltip("Bina başına SOKAK BOYUNCA küçük rastgele kaydırma (tile). Yola olan uzaklığı bozmaz; " +
             "yalnızca cephe boyunca hizayı kırar → daha doğal. 0 = tam hizalı.")]
    [Range(0f, 4f)] public float industrialAlongJitter = 0.2f;

    // -------------------------------------------------------------------------
    // INDUSTRIAL BLOCK FILL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sanayi binalarını, RoadGenerator'ın ürettiği DÜZ IZGARA SOKAKLARININ iki kenarına dizer.
    /// Her ızgara sokağı (industrialGridPaths) boyunca, bina ayak izi kadar aralıklarla, sokağın
    /// her iki tarafına dik yönde ofsetli binalar yerleştirilir. Binalar dünya-dik kalır (şehir
    /// binalarıyla aynı görünüm); yalnızca konumları yola göre hizalanır. Yola değen adaylar
    /// dışarı doğru itilir (TryFindRoadFreePosition). Tüm ayarlar yukarıda + MapDecorPlacer'da
    /// serileştirilir.
    /// </summary>
    void PlaceIndustrialLayout(MapGenerator map, BiomePaintSettings settings,
                               List<Vector2Int> tiles, float halfW, float halfH)
    {
        // Yollar RoadGenerator'da çiziliyor. Yollar doğrulanana kadar bina yerleşimini atla.
        // Açmak için industrialPlaceBuildings = true.
        if (!industrialPlaceBuildings)
        {
            Debug.Log("MapDecorPlacer: industrial layout atlandı — industrialPlaceBuildings KAPALI " +
                      "(MapDecorPlacer bileşenindeki 'Industrial Place Buildings' kutusunu işaretle).");
            return;
        }

        if (settings.industrialBuildings == null || settings.industrialBuildings.Count == 0)
        {
            Debug.Log("MapDecorPlacer: industrial layout atlandı — industrialBuildings boş.");
            return;
        }

        var rg = RoadGenerator.Instance;
        if (rg == null || !rg.IsGenerated)
        {
            Debug.Log("MapDecorPlacer: industrial layout atlandı — yollar henüz üretilmedi.");
            return;
        }

        var gridPaths = rg.GetIndustrialGridPaths();
        if (gridPaths == null || gridPaths.Count == 0)
        {
            Debug.Log("MapDecorPlacer: industrial layout atlandı — sanayi ızgara yolu yok " +
                      "(RoadGenerator.enableIndustrialGrids kapalı veya bölge çok küçük olabilir).");
            return;
        }

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

        Vector2 sr      = industrialScaleRange == Vector2.zero ? new Vector2(0.5f, 0.8f) : industrialScaleRange;
        float density   = Mathf.Clamp01(industrialFillChance);
        float alongSp   = Mathf.Max(0.4f, industrialAlongSpacing);
        float jitterAmp = Mathf.Max(0f, industrialAlongJitter); // sokak boyunca (tile)
        int rows        = Mathf.Max(1, industrialRowsPerSide);
        int rowGap      = Mathf.Max(0, industrialRowSpacing);

        // --- Birim uyumu: spacing'i ortalama BİNA AYAK İZİ cinsinden ölç ----------------------
        float meanScale = (sr.x + sr.y) * 0.5f;
        float fpW = 0f;
        for (int i = 0; i < valid.Count; i++)
            fpW += settings.industrialBuildings[valid[i]].daySprite.rect.width * meanScale;
        fpW /= valid.Count;  // ortalama bina genişliği (tile)

        // Sokak boyunca bina adımı (tile) — binalar yan yana dizilsin.
        int stepAlong = Mathf.Max(1, Mathf.RoundToInt(fpW * alongSp));

        // Yol kenarına kadar dik ofset tabanı: yol kalınlığı + outline + omuz + ek margin.
        float roadClear = rg.industrialGridThickness * 0.5f + rg.industrialGridOutlineWidth
                          + rg.shoulderWidth + Mathf.Max(0, industrialRoadEdgeMargin);

        int[] sides = industrialLineBothSides ? new[] { +1, -1 } : new[] { +1 };

        int placed = 0, streets = 0;
        int[] spriteCounts = new int[valid.Count];

        foreach (var path in gridPaths)
        {
            if (path == null || path.Count < 2) continue;
            bool anyOnStreet = false;

            // Serit yonu: tek komsu ciftinden alinca rasterize edilmis EGIK seritte kuantalama
            // gurultusu olusuyor (perp 45°'ye kadar sekip binalari yola sokuyordu). Grid lane'leri
            // DUZ oldugu icin yonu UC NOKTALARDAN al = gercek serit yonu, tilt'ten bagimsiz kararli.
            Vector2 tangent = ((Vector2)(path[path.Count - 1] - path[0]));
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector2.right;
            tangent.Normalize();
            Vector2 perp = new Vector2(-tangent.y, tangent.x);

            // Adimlamayi TILE-index yerine ARC-UZUNLUK ile yap: egik serit birim uzunlukta ~1.41x
            // fazla tile icerir; index-adimlama binalari egik seritlerde sikistiriyordu. Kumulatif
            // mesafe stepAlong'u her astiginda bir slot ac (eksen-hizali seritte p-adimlama ile ayni).
            float accS = 0f, nextS = 0f;
            for (int p = 0; p < path.Count; p++)
            {
                if (p > 0) accS += Vector2.Distance(path[p], path[p - 1]);
                if (accS < nextS) continue;
                nextS += stepAlong;

                Vector2Int c = path[p];

                // Sokak boyunca (teğet yönünde) rastgele kaydırma — yola uzaklığı bozmaz.
                float jAlong = jitterAmp > 0f ? (Random.value - 0.5f) * 2f * jitterAmp : 0f;

                foreach (int side in sides)
                for (int row = 0; row < rows; row++)
                {
                    if (Random.value > density) continue;

                    int pick      = PickBalancedSpriteIndex(spriteCounts);
                    var entry     = settings.industrialBuildings[valid[pick]];
                    Sprite daySprite = entry.daySprite;

                    float scale = Random.Range(sr.x, sr.y);
                    int hTiles  = Mathf.Max(1, Mathf.RoundToInt(daySprite.rect.height * scale));
                    float halfDepth = daySprite.rect.width * scale * 0.5f; // binanın yola bakan yarı-genişliği

                    // Yol merkezinden dik yönde dışarı ofset; her sıra bir bina boyu daha geride.
                    float offset = roadClear + halfDepth + row * (fpW + rowGap);
                    float baseX = c.x + perp.x * offset * side + tangent.x * jAlong;
                    float baseY = c.y + perp.y * offset * side + tangent.y * jAlong;

                    // Sprite merkezini taban üstünden yukarı kaydır (taban yere otursun).
                    int tx = Mathf.RoundToInt(baseX);
                    int ty = Mathf.RoundToInt(baseY + hTiles * 0.5f);

                    // Sanayi bölgesinde, karada ve kıyı tamponunda olmalı.
                    if (!map.IsLand(tx, ty) || map.GetBiome(tx, ty) != 3) continue;
                    if (cityShoreBuffer > 0 && !HasShoreBuffer(map, tx, ty)) continue;

                    // Yola değiyorsa dışarı doğru ittirerek yol-serbest bir konum bul; bulunamazsa atla.
                    if (!TryFindRoadFreePosition(tx, ty, daySprite, scale, 4, industrialRoadEdgeMargin,
                                                 out int fx, out int fy))
                        continue;
                    tx = fx; ty = fy;
                    if (map.GetBiome(tx, ty) != 3 || !map.IsLand(tx, ty)) continue;

                    float effRadius = (stepAlong * 0.45f) / pixelsPerUnit;
                    float wx = transform.position.x + (tx / pixelsPerUnit) - halfW;
                    float wy = transform.position.y + (ty / pixelsPerUnit) - halfH;

                    if (IsDenseOverlapping(wx, wy, effRadius)) continue;
                    AddDense(wx, wy, effRadius);

                    float baseA   = 1f;
                    int sortOrder = 10 + (int)(wy * -100f);

                    var (go, daySR, nightSR, shadow) = CreateCityBuildingObject(
                        daySprite, entry.nightSprite, wx, wy, scale, baseA, sortOrder, entry.isIsometric);
                    go.name = "IndustrialBuilding";

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
                    anyOnStreet = true;
                }
            }

            if (anyOnStreet) streets++;
        }

        Debug.Log($"MapDecorPlacer: industrial layout (yol kenarı) — streets={streets}/{gridPaths.Count}, " +
                  $"placed={placed}, stepAlong={stepAlong}, rows={rows}, roadClear={roadClear:F1}, fp={fpW:F0}, " +
                  $"bothSides={industrialLineBothSides}, margin={industrialRoadEdgeMargin}, " +
                  $"fillChance={density:F2}, alongJitter={jitterAmp:F1}");
    }
}
