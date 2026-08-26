using System.Collections.Generic;
using UnityEngine;

// Agricultural CROP FIELDS — a rice-paddy style PARCEL MOSAIC painted over the agricultural biome
// (biome 4), underneath the sparse farm buildings (MapDecorPlacer.AgriculturalLayout). Reference
// look: farmland is an almost continuous patchwork of rectangular-ish parcels in many shades of
// green (lime → deep green → yellow-green → maturing gold, rare plowed brown), separated by THIN
// pale earth paths/dikes. Boundaries are mostly straight but slightly wonky.
//
// Implementation: the biome is partitioned by a ROTATED IRREGULAR GRID — strips of varying width
// along one axis, each strip independently cut into parcels of varying length along the other.
// A low-frequency Perlin warp bends the grid lines a little so nothing is laser-straight. Each
// parcel hashes to its own colour + brightness + crop-row phase; pixels near a parcel boundary
// become path-coloured. A random fraction of parcels stays EMPTY (transparent → underlying grass)
// so farm buildings keep room to spawn. The whole mosaic renders as ONE quad with ONE generated
// texture (a few pixels per tile) — far cheaper than the old per-blob meshes/materials.
public partial class MapDecorPlacer
{
    // -------------------------------------------------------------------------
    // CROP FIELD — serialized tuning
    // -------------------------------------------------------------------------

    [Header("Agricultural Layout — Tarla Mozaiği (Parcel Mosaic)")]
    [Tooltip("Tarım bölgesine (biome 4) parsel mozaiği (çeltik/ekin tarlaları) çiz. Kapalıysa hiç tarla konmaz.")]
    public bool agriculturalPlaceCropFields = true;

    [Tooltip("Parsel ŞERİT genişliği aralığı (tile). Şeritler bölge açısı boyunca uzanır; " +
             "her şeridin genişliği bu aralıktan rastgele seçilir → değişken parsel boyları.")]
    public Vector2 agriculturalParcelWidthTiles = new Vector2(9f, 22f);

    [Tooltip("Bir şerit İÇİNDEKİ parsel uzunluğu aralığı (tile). Genişlikten büyük tutun → " +
             "dikdörtgenimsi, uzunlamasına parseller (referans görünüm).")]
    public Vector2 agriculturalParcelLengthTiles = new Vector2(14f, 42f);

    [Tooltip("Bir parselin BOŞ (ekilmemiş, altındaki çim görünür) kalma olasılığı. Çiftlik " +
             "binaları yalnızca boş parsellere yerleşebildiği için 0 yapmayın.")]
    [Range(0f, 1f)] public float agriculturalParcelEmptyChance = 0.3f;

    [Tooltip("Parseller arasındaki açık toprak PATİKA genişliği (tile). Referans görüntüdeki " +
             "ince açık çizgiler. 0 = patika yok.")]
    [Range(0f, 3f)] public float agriculturalParcelPathTiles = 0.8f;

    [Tooltip("Tarlaların yolun GÖRSEL KENARINDAN (boyanmış dolgu + outline) uzak duracağı tile " +
             "mesafesi. Yol merkezinden DEĞİL — merkez baz alınırsa kalın highway'lerde mozaik " +
             "yolun dış bandının üstüne taşar. 0 = kenara dayanır (omuz bandını da örter), " +
             "2 = omuzun da dışında kalır. Küçük tutun — referansta tarlalar yola kadar dayanır.")]
    [Range(0f, 12f)] public float agriculturalParcelRoadClearTiles = 2f;

    [Tooltip("Grid çizgilerinin Perlin ile ne kadar eğrileceği (tile). 0 = cetvel gibi düz, " +
             "2-3 = hafif dalgalı doğal sınırlar.")]
    [Range(0f, 8f)] public float agriculturalParcelWarpTiles = 2f;

    [Tooltip("Parsel içi ekin sırası (furrow) tekrar periyodu (tile). Sıralar parselin uzun " +
             "ekseni boyunca uzanır; etki bilinçli olarak ÇOK hafiftir.")]
    [Range(2f, 20f)] public float agriculturalParcelRowPeriodTiles = 5f;

    [Tooltip("Ekili tarlalar GECE ne kadar kararsın (0 = kararma yok, 0.6 = gece %60 daha koyu). " +
             "Gündüz↔gece geçişinde LightingRatio ile yumuşak harmanlanır.")]
    [Range(0f, 1f)] public float agriculturalFieldNightDarken = 0.6f;

    // Cleanup of the procedurally generated texture/material/mesh between Repaint() calls.
    Texture2D cropFieldTexture;
    readonly List<Material> cropFieldMaterials = new List<Material>();
    // Her tarla materyalinin GÜNDÜZ (temel) rengi — gece kararmasını buradan türetiriz.
    // cropFieldMaterials ile indeks eşleşir. Repaint başına temizlenir.
    readonly List<Color> cropFieldBaseColors = new List<Color>();
    readonly List<Mesh> cropFieldMeshes = new List<Mesh>();

    // EKİLİ tile'ların kümesi (key = x + y*w) — tarım binalarının ekin ÜSTÜNE yerleşmesini
    // engellemek için (PlaceAgriculturalLayout bunu okur). Boş parseller kümeye girmez →
    // binalar oralara yerleşebilir. Repaint başına temizlenir.
    readonly HashSet<int> cropFieldTiles = new HashSet<int>();

    // Doku çözünürlüğü: tile başına piksel. 6 → çapraz kenarlardaki merdiven basamakları
    // yarı boyuta iner (aynı mesafede iki katı basamak). Bellek 3'e göre 4 katına çıkar,
    // bu yüzden aşağıdaki tavan boyutuyla sınırlanır.
    const int CropFieldPxPerTile = 6;

    // Doku hiçbir boyutta bunu aşmasın; aşacaksa çözünürlük kademeli düşürülür.
    // Çok geniş tarım bölgelerinde bellek patlamasını ve GPU limitini önler.
    const int CropFieldMaxTextureSize = 8192;

    // Parsel renk paleti (referans: çeltik tarlaları) + ağırlıkları. Ağırlık büyük = daha sık.
    static readonly Color[] ParcelPalette =
    {
        new Color(0.44f, 0.64f, 0.20f), // taze yeşil (genç çeltik)
        new Color(0.35f, 0.56f, 0.17f), // orta yeşil
        new Color(0.26f, 0.46f, 0.14f), // koyu doygun yeşil
        new Color(0.52f, 0.64f, 0.24f), // sarımsı yeşil
        new Color(0.60f, 0.55f, 0.26f), // olgunlaşan altın-yeşil
        new Color(0.46f, 0.37f, 0.22f), // sürülmüş kahverengi (nadir)
    };
    static readonly float[] ParcelWeights = { 3f, 3f, 2f, 2f, 1f, 0.5f };

    // Parseller arası patika (açık kuru toprak) rengi.
    static readonly Color ParcelPathColor = new Color(0.68f, 0.63f, 0.45f);

    // -------------------------------------------------------------------------
    // PARCEL MOSAIC FILL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Biome 4 bölgesini döndürülmüş düzensiz bir grid ile parsellere böler ve tek bir quad +
    /// üretilmiş doku olarak çizer. Her parselin kendi rengi vardır; aralarında ince patikalar
    /// bulunur; bir kısmı boş bırakılır (binalar için). Binalardan ÖNCE çağrılır.
    /// </summary>
    void PlaceAgriculturalFields(MapGenerator map, List<Vector2Int> tiles, float halfW, float halfH)
    {
        if (!agriculturalPlaceCropFields) return;
        if (tiles == null || tiles.Count == 0) return;

        CleanupCropFieldAssets();

        int w = map.width;

        // Bölge bounding box'ı.
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t.x < minX) minX = t.x;
            if (t.x > maxX) maxX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.y > maxY) maxY = t.y;
        }
        int bw = maxX - minX + 1;
        int bh = maxY - minY + 1;

        // Tile başına "boyanabilir mi" tablosu: bölge üyeliği + yol açıklığı.
        //
        // Açıklık YOLUN GÖRSEL KENARINDAN ölçülür (GetDistanceToRoadEdge: 0 = boyanmış yol
        // pikseli, 1 = kenara komşu tile...). Merkez hattından ölçmek (GetDistanceToRoad)
        // yanlıştır: highway'in boyalı bandı thickness + outline*2 tile genişliğindedir, yani
        // yarı genişliği birkaç tile eder — merkeze 3 tile açıklık bırakmak mozaiği hâlâ yolun
        // outline'ının ÜSTÜNE bırakır (mozaik quad'ı harita dokusunun üstünde çizilir).
        var rg = RoadGenerator.Instance;
        int roadClear = Mathf.RoundToInt(agriculturalParcelRoadClearTiles);
        bool checkRoad = rg != null && rg.IsGenerated;

        var paintable = new bool[bw * bh];
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            // roadClear = 0 olsa bile yol pikselinin kendisi (mesafe 0) asla boyanmaz.
            if (checkRoad && rg.GetDistanceToRoadEdge(t.x, t.y) <= roadClear) continue;
            paintable[(t.x - minX) + (t.y - minY) * bw] = true;
        }

        // ---- Döndürülmüş düzensiz grid ----
        float ang = Random.Range(0f, Mathf.PI);
        float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);

        // Bbox köşelerini (u,v) uzayına döndürüp grid aralığını bul.
        float uMin = float.MaxValue, uMax = float.MinValue;
        float vMin = float.MaxValue, vMax = float.MinValue;
        for (int cy = 0; cy <= 1; cy++)
        for (int cx = 0; cx <= 1; cx++)
        {
            float fx = cx == 0 ? minX : maxX + 1;
            float fy = cy == 0 ? minY : maxY + 1;
            float u = ca * fx + sa * fy;
            float v = -sa * fx + ca * fy;
            if (u < uMin) uMin = u; if (u > uMax) uMax = u;
            if (v < vMin) vMin = v; if (v > vMax) vMax = v;
        }
        float pad = agriculturalParcelWarpTiles + 2f;
        uMin -= pad; uMax += pad; vMin -= pad; vMax += pad;

        Vector2 widR = agriculturalParcelWidthTiles;
        Vector2 lenR = agriculturalParcelLengthTiles;
        float minWid = Mathf.Max(3f, Mathf.Min(widR.x, widR.y));
        float maxWid = Mathf.Max(minWid, Mathf.Max(widR.x, widR.y));
        float minLen = Mathf.Max(3f, Mathf.Min(lenR.x, lenR.y));
        float maxLen = Mathf.Max(minLen, Mathf.Max(lenR.x, lenR.y));

        // Şerit kenarları (u ekseni) ve her şeridin parsel kenarları (v ekseni).
        var uEdges = new List<float>();
        for (float u = uMin; u < uMax + maxWid; u += Random.Range(minWid, maxWid))
            uEdges.Add(u);

        var vEdges = new List<float[]>(uEdges.Count - 1);
        for (int s = 0; s < uEdges.Count - 1; s++)
        {
            var edges = new List<float>();
            // Şeritler arası faz kayması — parsel köşeleri hizalanmasın.
            for (float v = vMin - Random.Range(0f, maxLen); v < vMax + maxLen; v += Random.Range(minLen, maxLen))
                edges.Add(v);
            vEdges.Add(edges.ToArray());
        }

        int hashSeed = Random.Range(1, 1 << 22);
        float noiseSeed = Random.Range(0f, 512f);

        // ---- Doku boyama ----
        int S = CropFieldPxPerTile;
        while (S > 1 && (bw * S > CropFieldMaxTextureSize || bh * S > CropFieldMaxTextureSize))
            S--;

        int texW = bw * S, texH = bh * S;

        // Mozaiğin tile→piksel dönüşümünü sakla: bölge dönüşümü (a29) dönüşen tarlaların
        // piksellerini tek tek silmek zorunda (bkz. MapDecorPlacer.CropDepot.ClearCropTiles).
        cropFieldOriginX       = minX;
        cropFieldOriginY       = minY;
        cropFieldPixelsPerTile = S;

        cropFieldTexture = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            // Point — harita dokusu ve yol overlay'i ile ayni pixel-art dili. Mozaik zaten
            // tile basina CropFieldPxPerTile piksel cozunurlukte; bilinear bunun ustune
            // bulaniklik ekliyordu (parsel sinirlari ve patikalar yakin zoom'da lapa oluyordu).
            filterMode = FilterMode.Point,
        };
        var pixels = new Color32[texW * texH];
        var clear = new Color32(0, 0, 0, 0);

        float pathHalf = Mathf.Max(0f, agriculturalParcelPathTiles) * 0.5f;
        float rowPeriod = Mathf.Max(2f, agriculturalParcelRowPeriodTiles);
        float warpAmp = agriculturalParcelWarpTiles;
        float emptyChance = Mathf.Clamp01(agriculturalParcelEmptyChance);
        float invS = 1f / S;

        int paintedParcels = 0;

        for (int py = 0; py < texH; py++)
        {
            float fy = minY + (py + 0.5f) * invS;

            for (int px = 0; px < texW; px++)
            {
                int idx = px + py * texW;

                float fx = minX + (px + 0.5f) * invS;

                // Bölge kenarı: maske tile çözünürlüklü olduğu için doğrudan okunursa
                // çapraz sınırlar tam tile'lık merdiven yapıyor. Komşu dört tile arasında
                // bilineer örnekleyip 0.5'te eşikleyince kenar tile'ın içinden geçebiliyor —
                // çapraz sınırlar tam basamak yerine pah kırılmış görünüyor.
                if (SampleCoverage(paintable, bw, bh, fx - minX - 0.5f, fy - minY - 0.5f) < 0.5f)
                {
                    pixels[idx] = clear;
                    continue;
                }

                // Grid çizgilerini hafifçe eğ (düşük frekanslı warp).
                float wu = (Mathf.PerlinNoise(noiseSeed + fx * 0.03f, noiseSeed + fy * 0.03f) - 0.5f) * 2f * warpAmp;
                float wv = (Mathf.PerlinNoise(noiseSeed + 77f + fx * 0.03f, noiseSeed + 77f + fy * 0.03f) - 0.5f) * 2f * warpAmp;
                float u = ca * fx + sa * fy + wu;
                float v = -sa * fx + ca * fy + wv;

                int s = FindBand(uEdges, u);
                float[] pe = vEdges[s];
                int p = FindBand(pe, v);

                // Boş parsel → altındaki çim görünsün.
                if (Hash01(s, p, hashSeed) < emptyChance) { pixels[idx] = clear; continue; }

                // Parsel sınırına yakınsa patika.
                float distEdge = Mathf.Min(
                    Mathf.Min(u - uEdges[s], uEdges[s + 1] - u),
                    Mathf.Min(v - pe[p], pe[p + 1] - v));
                if (distEdge < pathHalf)
                {
                    float pg = 0.94f + 0.10f * Mathf.PerlinNoise(noiseSeed + px * 0.31f, noiseSeed + py * 0.31f);
                    pixels[idx] = new Color(ParcelPathColor.r * pg, ParcelPathColor.g * pg, ParcelPathColor.b * pg, 1f);
                    continue;
                }

                // Parsel rengi: ağırlıklı palet + parsel başına parlaklık sapması.
                Color c = PickParcelColor(s, p, hashSeed);
                float bright = 0.88f + 0.12f * Hash01(s, p, hashSeed + 917);

                // Parsel İÇİ doğal doku — üç ölçekli Perlin:
                // (a) benek/leke (mottle): ekin örtüsündeki gür/seyrek yamalar, tonu koyulaştırır;
                // (b) ıslak/kuru yama (patch): geniş, yavaş dalgalanan aydınlık farkı;
                // (c) ince tane (grain): piksel ölçeğinde doku hissi.
                float mottle = Mathf.PerlinNoise(noiseSeed + 37f + fx * 0.16f, noiseSeed + 37f + fy * 0.16f);
                c = Color.Lerp(c, new Color(c.r * 0.62f, c.g * 0.70f, c.b * 0.62f),
                               Mathf.SmoothStep(0.35f, 0.85f, mottle) * 0.45f);
                float patch = Mathf.PerlinNoise(noiseSeed + 133f + fx * 0.055f, noiseSeed + 133f + fy * 0.055f);
                float grain = 0.90f + 0.13f * Mathf.PerlinNoise(noiseSeed + fx * 1.7f, noiseSeed + fy * 1.7f);

                // Çok hafif ekin sırası (parsel fazlı) + bölgesel ton kayması (parlatmadan).
                float rowPhase = Hash01(s, p, hashSeed + 311) * Mathf.PI * 2f;
                float row = 1f + 0.05f * Mathf.Sin(u * (Mathf.PI * 2f / rowPeriod) + rowPhase);
                float regional = Mathf.PerlinNoise(noiseSeed + 191f + fx * 0.008f, noiseSeed + 191f + fy * 0.008f);
                c = Color.Lerp(c, new Color(0.60f, 0.56f, 0.28f), Mathf.Clamp01((regional - 0.5f) * 0.30f));

                float m = bright * row * grain * (0.93f + 0.14f * patch);
                pixels[idx] = new Color(Mathf.Clamp01(c.r * m), Mathf.Clamp01(c.g * m), Mathf.Clamp01(c.b * m), 1f);
            }
        }

        cropFieldTexture.SetPixels32(pixels);
        cropFieldTexture.Apply(false);

        // ---- Ekili tile kümesi (bina engelleme) — tile merkezinden parsel sorgusu ----
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (!paintable[(t.x - minX) + (t.y - minY) * bw]) continue;
            float fx = t.x + 0.5f, fy = t.y + 0.5f;
            float wu = (Mathf.PerlinNoise(noiseSeed + fx * 0.03f, noiseSeed + fy * 0.03f) - 0.5f) * 2f * warpAmp;
            float wv = (Mathf.PerlinNoise(noiseSeed + 77f + fx * 0.03f, noiseSeed + 77f + fy * 0.03f) - 0.5f) * 2f * warpAmp;
            float u = ca * fx + sa * fy + wu;
            float v = -sa * fx + ca * fy + wv;
            int s = FindBand(uEdges, u);
            int p = FindBand(vEdges[s], v);
            if (Hash01(s, p, hashSeed) < emptyChance) continue;
            cropFieldTiles.Add(t.x + t.y * w);
            paintedParcels++;
        }

        CreateCropFieldQuad(minX, minY, maxX, maxY, halfW, halfH);

        Debug.Log($"MapDecorPlacer: agricultural parcel mosaic — bbox={bw}x{bh}, strips={uEdges.Count - 1}, " +
                  $"croppedTiles={paintedParcels}/{tiles.Count}, emptyChance={emptyChance:F2}.");
    }

    /// <summary>
    /// Tile çözünürlüklü boyanabilirlik maskesini komşu dört tile arasında bilineer örnekler.
    /// gx/gy, tile MERKEZLERİ tam sayıya denk gelecek şekilde verilir.
    /// Dönen değer 0..1 kapsama oranıdır; 0.5 eşiği kenarı tile'ın ortasından geçirir.
    /// </summary>
    static float SampleCoverage(bool[] mask, int bw, int bh, float gx, float gy)
    {
        int x0 = Mathf.FloorToInt(gx);
        int y0 = Mathf.FloorToInt(gy);
        float tx = gx - x0;
        float ty = gy - y0;

        float m00 = MaskAt(mask, bw, bh, x0, y0);
        float m10 = MaskAt(mask, bw, bh, x0 + 1, y0);
        float m01 = MaskAt(mask, bw, bh, x0, y0 + 1);
        float m11 = MaskAt(mask, bw, bh, x0 + 1, y0 + 1);

        return Mathf.Lerp(Mathf.Lerp(m00, m10, tx), Mathf.Lerp(m01, m11, tx), ty);
    }

    /// <summary>Maske değeri; bölge dışı 0 sayılır (kenarda kapsama doğal olarak düşer).</summary>
    static float MaskAt(bool[] mask, int bw, int bh, int x, int y)
    {
        if (x < 0 || y < 0 || x >= bw || y >= bh) return 0f;
        return mask[x + y * bw] ? 1f : 0f;
    }

    /// <summary>Sıralı kenar dizisinde x'in düştüğü bant indeksini (binary search) döndürür.</summary>
    static int FindBand(List<float> edges, float x)
    {
        int lo = 0, hi = edges.Count - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (edges[mid] <= x) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    static int FindBand(float[] edges, float x)
    {
        int lo = 0, hi = edges.Length - 2;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (edges[mid] <= x) lo = mid; else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>Deterministik parsel hash'i → [0,1). Aynı (şerit, parsel, seed) hep aynı değeri verir.</summary>
    static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1103515245u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }

    /// <summary>Parsel için ağırlıklı paletten renk seçer (deterministik).</summary>
    static Color PickParcelColor(int s, int p, int seed)
    {
        float total = 0f;
        for (int i = 0; i < ParcelWeights.Length; i++) total += ParcelWeights[i];
        float r = Hash01(s, p, seed + 4231) * total;
        for (int i = 0; i < ParcelWeights.Length; i++)
        {
            r -= ParcelWeights[i];
            if (r <= 0f) return ParcelPalette[i];
        }
        return ParcelPalette[0];
    }

    /// <summary>Mozaik dokusunu bölge bbox'ını kaplayan TEK bir quad olarak sahneye koyar.</summary>
    void CreateCropFieldQuad(int minX, int minY, int maxX, int maxY, float halfW, float halfH)
    {
        float invPpu = 1f / pixelsPerUnit;
        float x0 = minX * invPpu - halfW;
        float y0 = minY * invPpu - halfH;
        float x1 = (maxX + 1) * invPpu - halfW;
        float y1 = (maxY + 1) * invPpu - halfH;

        var mesh = new Mesh { name = "CropFieldMosaic" };
        mesh.vertices = new[]
        {
            new Vector3(x0, y0, 0f), new Vector3(x1, y0, 0f),
            new Vector3(x1, y1, 0f), new Vector3(x0, y1, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        cropFieldMeshes.Add(mesh);

        var go = new GameObject("CropFieldMosaic");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr  = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = cropFieldTexture;
        var baseColor = Color.white;
        mat.color = baseColor;
        cropFieldMaterials.Add(mat);
        cropFieldBaseColors.Add(baseColor);
        mr.sharedMaterial = mat;

        // Üst kenara göre sırala: bbox'ın en üstünden aşağıdaki HER binanın sortingOrder'ı daha
        // büyük olur (10 + wy*-100) → binalar mozaiğin üstüne çizilir.
        float worldTopY = y1 + transform.position.y;
        mr.sortingOrder = 10 + (int)(worldTopY * -100f) - 2;

        decorObjects.Add(go);
    }

    void CleanupCropFieldAssets()
    {
        for (int i = 0; i < cropFieldMaterials.Count; i++)
            if (cropFieldMaterials[i] != null) Destroy(cropFieldMaterials[i]);
        cropFieldMaterials.Clear();
        cropFieldBaseColors.Clear();

        for (int i = 0; i < cropFieldMeshes.Count; i++)
            if (cropFieldMeshes[i] != null) Destroy(cropFieldMeshes[i]);
        cropFieldMeshes.Clear();

        if (cropFieldTexture != null) { Destroy(cropFieldTexture); cropFieldTexture = null; }

        cropFieldTiles.Clear();
        cropFieldPixelsPerTile = 0; //doku gitti — eski tile→piksel eşlemesi artık geçersiz
    }

    /// <summary>
    /// Ekili tarlaları gündüz↔gece döngüsüne bağlar: LightingRatio (0=gündüz, 1=gece) arttıkça
    /// tarla materyallerinin RGB'sini agriculturalFieldNightDarken oranında karartır. ApplyCrossfade
    /// içinden her ışık değişiminde çağrılır.
    /// </summary>
    void ApplyCropFieldNightDarken(float ratio)
    {
        if (cropFieldMaterials.Count == 0) return;
        // ratio=0 → çarpan 1 (gündüz), ratio=1 → çarpan (1 - darken) (gece).
        float mul = Mathf.Lerp(1f, 1f - Mathf.Clamp01(agriculturalFieldNightDarken), Mathf.Clamp01(ratio));
        for (int i = 0; i < cropFieldMaterials.Count; i++)
        {
            var m = cropFieldMaterials[i];
            if (m == null) continue;
            Color b = cropFieldBaseColors[i];
            m.color = new Color(b.r * mul, b.g * mul, b.b * mul, b.a);
        }
    }
}
