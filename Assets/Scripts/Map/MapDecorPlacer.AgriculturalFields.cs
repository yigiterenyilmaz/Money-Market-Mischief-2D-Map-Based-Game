using System.Collections.Generic;
using UnityEngine;

// Agricultural CROP FIELDS — textured "wheat" subregions painted INSIDE the agricultural biome
// (biome 4), underneath the sparse farm buildings (MapDecorPlacer.AgriculturalLayout). Real
// farmland here reads as a FEW large, smooth, organic crop plots scattered with green gaps
// between them — not a packed mosaic. Each plot is a smooth blob (many-sided polygon whose radius
// is modulated by a couple of sine harmonics, so the outline is curvy with no sharp corners), and
// is drawn as a fan mesh sampling one shared, COLOURED, tiling wheat texture: sharp golden furrow
// rows + greenish/amber blotches give every field the SAME crop look but with rich internal colour
// variation. Spacing is enforced between plot centres so the region stays sparse.
public partial class MapDecorPlacer
{
    // -------------------------------------------------------------------------
    // CROP FIELD — serialized tuning
    // -------------------------------------------------------------------------

    [Header("Agricultural Layout — Ekili Tarlalar (Wheat Fields)")]
    [Tooltip("Tarım bölgesine (biome 4) dokulu ekin tarlaları (buğday) çiz. Kapalıysa hiç tarla konmaz.")]
    public bool agriculturalPlaceCropFields = true;

    [Tooltip("Bölgenin EN FAZLA ne kadarı tarlalarla kaplansın (0..1). Aralarındaki boşluk " +
             "agriculturalFieldGapTiles ile de sınırlanır → SEYREK görünüm için düşük tutun.")]
    [Range(0.02f, 1f)] public float agriculturalFieldCoverage = 0.18f;

    [Tooltip("Tarla merkezleri arasında bırakılacak EK boşluk (tile). Büyük = tarlalar birbirinden " +
             "uzak, aralarında bol yeşil → daha az tarla.")]
    [Range(0f, 60f)] public float agriculturalFieldGapTiles = 14f;

    [Tooltip("Tarlaların yoldan (ve yol kenarı lambalarından) uzak duracağı tile mesafesi. Lambalar " +
             "yol merkezinden ~7 tile açıkta durduğu için bunu 8+ tutun → tarla yol/lamba üstüne taşmaz.")]
    [Range(0f, 30f)] public float agriculturalFieldRoadClearTiles = 9f;

    [Tooltip("Bir tarla plot'unun UZUN ekseni çapı aralığı (tile).")]
    public Vector2 agriculturalFieldLengthTiles = new Vector2(34f, 64f);

    [Tooltip("Bir tarla plot'unun KISA ekseni çapı aralığı (tile).")]
    public Vector2 agriculturalFieldWidthTiles = new Vector2(24f, 44f);

    [Tooltip("Ekin sırası (furrow) tekrar periyodu (tile). UZAKTAN görünür olması için BÜYÜK tutun.")]
    [Range(3f, 24f)] public float agriculturalCropRowPeriodTiles = 10f;

    [Tooltip("Plot kenarının ne kadar düzensiz/organik olacağı (0 = düzgün oval, 1 = çok dalgalı). " +
             "Kenar her zaman YUMUŞAK kalır (köşe yok).")]
    [Range(0f, 1f)] public float agriculturalFieldIrregularity = 0.6f;

    [Tooltip("Komşu tarlaların aynı yöne hizalanma eğilimi (0 = bağımsız rastgele açı, 1 = bölge açısı).")]
    [Range(0f, 1f)] public float agriculturalFieldAlignment = 0.5f;

    // Cleanup of the procedurally generated texture/materials between Repaint() calls.
    Texture2D cropFieldTexture;
    readonly List<Material> cropFieldMaterials = new List<Material>();

    // Tarla tile'larının kümesi (key = x + y*w) — tarım binalarının tarla ÜSTÜNE yerleşmesini
    // engellemek için (PlaceAgriculturalLayout bunu okur). Repaint başına temizlenir.
    readonly HashSet<int> cropFieldTiles = new HashSet<int>();

    // Yüksek köşe sayısı + yumuşak yarıçap modülasyonu → düzgün organik blob (sivri köşe yok).
    // Yüksek frekanslı harmonikleri (girintili kenar) pürüzsüz çözebilmek için bol köşe.
    const int CropFieldSides = 56;

    // -------------------------------------------------------------------------
    // CROP FIELD FILL
    // -------------------------------------------------------------------------

    /// <summary>
    /// Biome 4 bölgesine AZ sayıda, geniş, yumuşak-organik ekin tarlası yerleştirir. Tarla
    /// merkezleri arasında boşluk (gap) zorunlu kılınır → seyrek görünüm. Binalardan ÖNCE çağrılır.
    /// </summary>
    void PlaceAgriculturalFields(MapGenerator map, List<Vector2Int> tiles, float halfW, float halfH)
    {
        if (!agriculturalPlaceCropFields) return;
        if (tiles == null || tiles.Count == 0) return;

        CleanupCropFieldAssets();
        cropFieldTexture = CreateWheatTexture();

        int w = map.width;
        var regionSet = new HashSet<int>(tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
            regionSet.Add(tiles[i].x + tiles[i].y * w);
        var occupied = new HashSet<int>(tiles.Count);

        float regionAngle = Random.Range(0f, Mathf.PI);

        Vector2 lenR = agriculturalFieldLengthTiles;
        Vector2 widR = agriculturalFieldWidthTiles;
        float period    = Mathf.Max(3f, agriculturalCropRowPeriodTiles);
        float gap       = Mathf.Max(0f, agriculturalFieldGapTiles);
        int   roadClear = Mathf.RoundToInt(Mathf.Max(0f, agriculturalFieldRoadClearTiles));

        int targetCovered = Mathf.RoundToInt(tiles.Count * Mathf.Clamp01(agriculturalFieldCoverage));
        int covered = 0;
        int placed  = 0;
        int attempts = tiles.Count;

        var uLocal = new float[CropFieldSides];
        var vLocal = new float[CropFieldSides];
        var coverScratch = new List<int>(4096);
        // Yerleştirilen tarla merkezleri (tile): x, y, yarıçap → aralık (gap) kontrolü için.
        var centers = new List<Vector3>();

        for (int attempt = 0; attempt < attempts && covered < targetCovered; attempt++)
        {
            Vector2Int seed = tiles[Random.Range(0, tiles.Count)];
            if (occupied.Contains(seed.x + seed.y * w)) continue;

            float halfLen = Random.Range(lenR.x, lenR.y) * 0.5f;
            float halfWid = Random.Range(widR.x, widR.y) * 0.5f;
            float radius  = Mathf.Max(halfLen, halfWid);

            // Aralık (gap) kontrolü — yerleştirilmiş tarlalara çok yakınsa ele (seyreklik).
            bool tooClose = false;
            for (int i = 0; i < centers.Count; i++)
            {
                float dx = centers[i].x - seed.x, dy = centers[i].y - seed.y;
                float minD = centers[i].z + radius + gap;
                if (dx * dx + dy * dy < minD * minD) { tooClose = true; break; }
            }
            if (tooClose) continue;

            float jitter = Random.Range(-0.5f, 0.5f) * (1f - agriculturalFieldAlignment) * Mathf.PI;
            float theta  = regionAngle + jitter;
            if (Random.value < 0.4f) theta += Mathf.PI * 0.5f;

            BuildBlobOutline(halfLen, halfWid, uLocal, vLocal);

            Vector2 dir  = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
            Vector2 perp = new Vector2(-dir.y, dir.x);

            if (!TryCollectBlobTiles(seed, dir, perp, uLocal, vLocal, w, roadClear,
                                     regionSet, occupied, coverScratch))
                continue;

            for (int i = 0; i < coverScratch.Count; i++) { occupied.Add(coverScratch[i]); cropFieldTiles.Add(coverScratch[i]); }
            covered += coverScratch.Count;
            centers.Add(new Vector3(seed.x, seed.y, radius));

            CreateCropFieldMesh(seed, dir, perp, uLocal, vLocal, period, halfW, halfH);
            placed++;
        }

        Debug.Log($"MapDecorPlacer: agricultural crop fields — placed={placed}, coveredTiles={covered}/" +
                  $"{tiles.Count} (target={targetCovered}).");
    }

    /// <summary>
    /// Bir plot'un YUMUŞAK organik kenarını (dir/perp yerel uzayında) üretir. Yarıçap, birkaç sinüs
    /// harmoniğinin (rastgele fazlı) toplamıyla modüle edilir → sürekli/türevlenebilir, sivri köşe
    /// içermeyen dalgalı bir kapalı eğri. Uzun/kısa eksene göre anizotropik gerilir.
    /// </summary>
    void BuildBlobOutline(float halfLen, float halfWid, float[] uLocal, float[] vLocal)
    {
        float irr = Mathf.Clamp01(agriculturalFieldIrregularity);
        // Düşük frekanslar genel gövde formunu, yüksek frekanslar kenardaki küçük girinti/çıkıntıları
        // (jaggy ama YUVARLAK — sinüs olduğu için sivri değil) verir. Hepsi rastgele fazlı.
        float p1 = Random.Range(0f, Mathf.PI * 2f);
        float p2 = Random.Range(0f, Mathf.PI * 2f);
        float p3 = Random.Range(0f, Mathf.PI * 2f);
        float p5 = Random.Range(0f, Mathf.PI * 2f);
        float p7 = Random.Range(0f, Mathf.PI * 2f);
        float p11 = Random.Range(0f, Mathf.PI * 2f);

        for (int k = 0; k < CropFieldSides; k++)
        {
            float a = (Mathf.PI * 2f / CropFieldSides) * k;
            // Çok-frekanslı toplam: gövde (1-3) + ince lob/çıkıntılar (5,7,11). Yüksek harmonikler
            // küçük genlikli → kenar dalgalı/girintili ama her geçiş yumuşak yuvarlak kalır.
            float wave = 0.34f * Mathf.Sin(a + p1)
                       + 0.20f * Mathf.Sin(2f * a + p2)
                       + 0.15f * Mathf.Sin(3f * a + p3)
                       + 0.16f * Mathf.Sin(5f * a + p5)
                       + 0.14f * Mathf.Sin(7f * a + p7)
                       + 0.11f * Mathf.Sin(11f * a + p11)
                       + 0.08f * Mathf.Sin(17f * a + p3 + p7);
            float r = 1f + irr * 0.95f * wave; // her zaman pozitif
            uLocal[k] = Mathf.Cos(a) * halfLen * r;
            vLocal[k] = Mathf.Sin(a) * halfWid * r;
        }
    }

    /// <summary>
    /// Blob polygonunun kapsadığı tüm tile'ları toplar (bounding box + point-in-polygon). Herhangi
    /// bir tile bölge dışındaysa ya da işgalliyse false döner (plot reddedilir → taşma/çakışma yok).
    /// </summary>
    bool TryCollectBlobTiles(Vector2Int seed, Vector2 dir, Vector2 perp, float[] uLocal, float[] vLocal,
                             int w, int roadClear, HashSet<int> regionSet, HashSet<int> occupied,
                             List<int> outTiles)
    {
        outTiles.Clear();

        float maxU = 0f, maxV = 0f;
        for (int k = 0; k < uLocal.Length; k++)
        {
            maxU = Mathf.Max(maxU, Mathf.Abs(uLocal[k]));
            maxV = Mathf.Max(maxV, Mathf.Abs(vLocal[k]));
        }
        int b = Mathf.CeilToInt(Mathf.Sqrt(maxU * maxU + maxV * maxV));

        var rg = RoadGenerator.Instance;
        bool checkRoad = rg != null && rg.IsGenerated && roadClear > 0;

        for (int dx = -b; dx <= b; dx++)
        for (int dy = -b; dy <= b; dy++)
        {
            float lu = dx * dir.x + dy * dir.y;
            float lv = dx * perp.x + dy * perp.y;
            if (!PointInPolygon(lu, lv, uLocal, vLocal)) continue;

            int tx = seed.x + dx, ty = seed.y + dy;
            int key = tx + ty * w;
            if (!regionSet.Contains(key)) return false;     // bölge dışı
            if (occupied.Contains(key))   return false;     // başka tarla/işgal
            // Yola/lambaya değme — yol merkez mesafesi roadClear'dan küçükse plot'u reddet.
            if (checkRoad && rg.GetDistanceToRoad(tx, ty) <= roadClear) return false;
            outTiles.Add(key);
        }
        return outTiles.Count > 0;
    }

    static bool PointInPolygon(float px, float py, float[] xs, float[] ys)
    {
        bool inside = false;
        int n = xs.Length;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            if (((ys[i] > py) != (ys[j] > py)) &&
                (px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i]))
                inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// Bir blob tarlasını fan-triangulation mesh + ortak dokulu buğday materyaliyle oluşturur.
    /// Ekin sıraları plot'un uzun ekseni boyunca uzanır ve 'period' tile'da bir tekrarlar.
    /// </summary>
    void CreateCropFieldMesh(Vector2Int seed, Vector2 dir, Vector2 perp, float[] uLocal, float[] vLocal,
                             float period, float halfW, float halfH)
    {
        int n = uLocal.Length;
        var verts = new Vector3[n + 1];
        var uvs   = new Vector2[n + 1];
        var tris  = new int[n * 3];

        float invPpu = 1f / pixelsPerUnit;
        float cLx = seed.x * invPpu - halfW;
        float cLy = seed.y * invPpu - halfH;
        verts[n] = new Vector3(cLx, cLy, 0f);
        uvs[n]   = new Vector2(0f, 0f);

        float maxWy = cLy;

        for (int k = 0; k < n; k++)
        {
            float tx = seed.x + uLocal[k] * dir.x + vLocal[k] * perp.x;
            float ty = seed.y + uLocal[k] * dir.y + vLocal[k] * perp.y;
            float lx = tx * invPpu - halfW;
            float ly = ty * invPpu - halfH;
            verts[k] = new Vector3(lx, ly, 0f);
            if (ly > maxWy) maxWy = ly;

            uvs[k] = new Vector2(uLocal[k] / period, vLocal[k] / period);

            int t = k * 3;
            tris[t]     = n;
            tris[t + 1] = k;
            tris[t + 2] = (k + 1) % n;
        }

        var mesh = new Mesh { name = "CropFieldMesh" };
        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        var go = new GameObject("CropField");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.05f);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr  = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = cropFieldTexture;
        // Tüm tarlalar AYNI ekin görünür → tint neredeyse beyaz (renk/doku değişimi dokudan gelir).
        // Yalnızca çok hafif parlaklık sapması (tarlalar birbirinin kopyası gibi olmasın).
        float bright = Random.Range(0.94f, 1.04f);
        mat.color = new Color(bright, bright, bright, 1f);
        cropFieldMaterials.Add(mat);
        mr.sharedMaterial = mat;

        float worldMaxWy = maxWy + transform.position.y;
        mr.sortingOrder = 10 + (int)(worldMaxWy * -100f) - 2;

        decorObjects.Add(go);
    }

    // -------------------------------------------------------------------------
    // PROCEDURAL WHEAT TEXTURE (coloured, shared by all fields)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tekrarlanabilir (wrap=Repeat) RENKLİ buğday dokusu: (1) plot uzun ekseni boyunca KESKİN
    /// altın furrow sıraları (koyu oluk ↔ açık tepe arasında net geçiş); (2) büyük ölçekli yeşilimsi/
    /// kehribar yamalar (blotch) → her tarlanın KENDİ İÇİNDE renk değişimi; (3) ince tane gürültüsü.
    /// Tüm tarlalar bu tek dokuyu paylaşır → aynı ekin, ama zengin iç doku.
    /// </summary>
    Texture2D CreateWheatTexture()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };

        // Buğday renk paleti.
        Color furrowDark  = new Color(0.46f, 0.39f, 0.13f); // oluk (gölge) — koyu altın
        Color furrowLight = new Color(0.95f, 0.86f, 0.43f); // tepe — açık saman
        Color greenTone   = new Color(0.62f, 0.66f, 0.27f); // olgunlaşmamış yeşil yama
        Color amberTone   = new Color(0.86f, 0.64f, 0.20f); // kehribar yama

        float seed = Random.Range(0f, 1000f);
        var pixels = new Color[N * N];

        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float fy = y / (float)N; // V (genişlik ekseni — sıralar buna göre değişir)
            float fx = x / (float)N; // U (uzunluk ekseni — sıralar bu yönde uzanır)

            // (1) KESKİN furrow: tek sıra; smoothstep ile dar parlak bant → net çizgiler.
            float row = 0.5f + 0.5f * Mathf.Cos(fy * Mathf.PI * 2f);
            float sharp = Mathf.SmoothStep(0.30f, 0.70f, row);
            // Sıra boyunca hafif eğrilme.
            float wobble = Mathf.PerlinNoise(seed + fx * 5f, seed * 0.5f) * 0.10f;
            sharp = Mathf.Clamp01(sharp + (wobble - 0.05f));
            Color c = Color.Lerp(furrowDark, furrowLight, sharp);

            // (2) Büyük ölçekli renk yamaları — tarla içi yeşil/kehribar değişim.
            float blotchG = Mathf.PerlinNoise(seed + fx * 2.3f, seed + fy * 2.3f);
            c = Color.Lerp(c, greenTone, Mathf.SmoothStep(0.55f, 0.85f, blotchG) * 0.45f);
            float blotchA = Mathf.PerlinNoise(seed + 50f + fx * 1.7f, seed + 50f + fy * 1.7f);
            c = Color.Lerp(c, amberTone, Mathf.SmoothStep(0.55f, 0.85f, blotchA) * 0.40f);

            // (3) İnce tane.
            float grain = 0.94f + 0.06f * Mathf.PerlinNoise(seed + x * 0.9f, seed + y * 0.9f);
            c.r = Mathf.Clamp01(c.r * grain);
            c.g = Mathf.Clamp01(c.g * grain);
            c.b = Mathf.Clamp01(c.b * grain);
            c.a = 1f;

            pixels[x + y * N] = c;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    void CleanupCropFieldAssets()
    {
        for (int i = 0; i < cropFieldMaterials.Count; i++)
            if (cropFieldMaterials[i] != null) Destroy(cropFieldMaterials[i]);
        cropFieldMaterials.Clear();

        if (cropFieldTexture != null) { Destroy(cropFieldTexture); cropFieldTexture = null; }

        cropFieldTiles.Clear();
    }
}
