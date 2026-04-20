using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Haritanın üstüne yarı saydam dalga efekti koyar.
/// Sadece su alanında görünür, kara üzerinde şeffaf.
/// G kanalında kıyı mesafe bilgisi — kıyıya vuran dalga animasyonu için.
/// </summary>
public class OceanWaveOverlay : MonoBehaviour
{
    [Header("References")]
    public MapPainter mapPainter;
    public MapGenerator mapGenerator;

    [Header("Shader")]
    public Shader oceanShader;

    [Header("Open Ocean Wave Settings")]
    [Range(0f, 1f)] public float intensity = 0.25f;
    public Color waveColorLight = new Color(0.3f, 0.5f, 0.7f, 0.2f);
    public Color waveColorDark = new Color(0.05f, 0.12f, 0.25f, 0.15f);
    public Color foamColor = new Color(0.8f, 0.9f, 1f, 0.3f);

    [Range(3f, 30f)] public float waveScale1 = 8f;
    [Range(5f, 40f)] public float waveScale2 = 15f;
    public Vector2 waveSpeed1 = new Vector2(0.06f, 0.04f);
    public Vector2 waveSpeed2 = new Vector2(-0.04f, 0.06f);

    [Range(10f, 50f)] public float foamScale = 25f;
    public Vector2 foamSpeed = new Vector2(0.01f, -0.015f);
    [Range(0.5f, 0.95f)] public float foamThreshold = 0.72f;

    [Header("Shore Wave Settings")]
    [Range(5, 40)] public int shoreWaveMaxDist = 20;
    [Range(0f, 1f)] public float shoreWaveIntensity = 0.55f;
    public Color shoreWaveColor = new Color(0.85f, 0.92f, 1f, 0.5f);
    public Color shoreWaveColorNight = new Color(0.25f, 0.35f, 0.5f, 0.35f);
    [Range(0.5f, 5f)] public float shoreWaveSpeed = 1.5f;
    [Range(1f, 8f)] public float shoreWaveFrequency = 3f;
    [Range(0f, 1f)] public float shoreFoamIntensity = 0.7f;

    [Header("Fog Overlay")]
    [Tooltip("Sorting order for fog layer — ships(8) altinda kalmasi icin 9+ olmali")]
    public int fogSortingOrder = 10;

    private GameObject overlayGO;
    private SpriteRenderer overlaySR;
    private Material overlayMat;
    private GameObject fogOverlayGO;
    private SpriteRenderer fogOverlaySR;
    private bool initialized;
    private DayNightCycle dayNight;
    private float gameTime;

    /// <summary>
    /// MapPainter tarafından harita boyanır boyanmaz çağrılır — gecikme yok.
    /// </summary>
    public void SetupNow()
    {
        if (initialized) return;
        Setup();
    }

    void Setup()
    {
        if (oceanShader == null)
            oceanShader = Shader.Find("Custom/OceanWave");
        if (oceanShader == null)
        {
            Debug.LogWarning("OceanWaveOverlay: Shader bulunamadı!");
            return;
        }

        int w = mapGenerator.width, h = mapGenerator.height;

        // su tarafında kıyı mesafe alanı (BFS)
        int[,] waterShoreDist = BuildWaterShoreDistField(w, h);

        // mask texture — R: kara=1/su=0, G: kıyı mesafesi (normalize 0-1)
        // Yol pikselleri (su uzerinden gecen kopruler dahil) "kara" sayilir,
        // boylece dalga animasyonu yolun ustune cizmez.
        var roadGen = RoadGenerator.Instance;
        bool roadsReady = roadGen != null && roadGen.IsGenerated;

        Texture2D mask = new Texture2D(w, h, TextureFormat.RGBA32, false);
        mask.filterMode = FilterMode.Point;
        Color[] maskPx = new Color[w * h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                bool isLand = mapGenerator.IsLand(x, y);
                bool isRoadPixel = roadsReady && roadGen.GetDistanceToRoadEdge(x, y) == 0;

                float land = (isLand || isRoadPixel) ? 1f : 0f;
                float shoreDist = 1f;
                if (!isLand && !isRoadPixel && waterShoreDist[x, y] < int.MaxValue)
                    shoreDist = Mathf.Clamp01((float)waterShoreDist[x, y] / shoreWaveMaxDist);
                maskPx[x + y * w] = new Color(land, shoreDist, 0f, 1f);
            }
        mask.SetPixels(maskPx);
        mask.Apply();

        // beyaz sprite — shader tüm işi yapacak
        Texture2D spriteTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        spriteTex.filterMode = FilterMode.Point;
        Color[] spritePx = new Color[w * h];
        for (int i = 0; i < spritePx.Length; i++)
            spritePx[i] = Color.white;
        spriteTex.SetPixels(spritePx);
        spriteTex.Apply();

        // materyal
        overlayMat = new Material(oceanShader);
        overlayMat.SetTexture("_MaskTex", mask);
        UpdateMaterialProperties();

        // overlay GO
        if (overlayGO != null) Destroy(overlayGO);
        overlayGO = new GameObject("OceanWaveOverlay");
        overlayGO.transform.SetParent(transform);
        overlayGO.transform.localPosition = new Vector3(0f, 0f, -0.5f);

        overlaySR = overlayGO.AddComponent<SpriteRenderer>();
        overlaySR.sprite = Sprite.Create(spriteTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        overlaySR.material = overlayMat;
        overlaySR.sortingOrder = 1;

        // sis overlay — gemilerin üstünde, sis bölgesinde yarı saydam katman
        CreateFogOverlay(w, h);

        initialized = true;
        Debug.Log("OceanWaveOverlay: Aktif (kıyı dalgası + sis overlay dahil).");
    }

    /// <summary>
    /// Sis bölgelerini gemilerin üstünde çizen ayrı sprite katmanı.
    /// fogMap verisini kullanarak sadece sisli pikselleri boyar.
    /// </summary>
    void CreateFogOverlay(int w, int h)
    {
        Color fogColor = mapGenerator.fogColor;
        Texture2D fogTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        fogTex.filterMode = FilterMode.Point;
        Color[] fogPx = new Color[w * h];

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                float fog = mapGenerator.GetFog(x, y);
                fogPx[x + y * w] = fog > 0f
                    ? new Color(fogColor.r, fogColor.g, fogColor.b, fog)
                    : new Color(0f, 0f, 0f, 0f);
            }

        fogTex.SetPixels(fogPx);
        fogTex.Apply();

        if (fogOverlayGO != null) Destroy(fogOverlayGO);
        fogOverlayGO = new GameObject("FogOverlay");
        fogOverlayGO.transform.SetParent(transform);
        fogOverlayGO.transform.localPosition = new Vector3(0f, 0f, -1f);

        fogOverlaySR = fogOverlayGO.AddComponent<SpriteRenderer>();
        fogOverlaySR.sprite = Sprite.Create(fogTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        fogOverlaySR.sortingOrder = fogSortingOrder;
    }

    /// <summary>
    /// Harita kenarından flood fill ile okyanusa bağlı su piksellerini tespit eder.
    /// Göller (karayla çevrili su) false kalır.
    /// </summary>
    bool[,] BuildOceanMask(int w, int h)
    {
        bool[,] isOcean = new bool[w, h];
        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };
        var queue = new Queue<Vector2Int>();

        // harita kenarındaki su piksellerinden başla
        for (int x = 0; x < w; x++)
        {
            if (!mapGenerator.IsLand(x, 0))     { isOcean[x, 0] = true; queue.Enqueue(new Vector2Int(x, 0)); }
            if (!mapGenerator.IsLand(x, h - 1)) { isOcean[x, h - 1] = true; queue.Enqueue(new Vector2Int(x, h - 1)); }
        }
        for (int y = 1; y < h - 1; y++)
        {
            if (!mapGenerator.IsLand(0, y))     { isOcean[0, y] = true; queue.Enqueue(new Vector2Int(0, y)); }
            if (!mapGenerator.IsLand(w - 1, y)) { isOcean[w - 1, y] = true; queue.Enqueue(new Vector2Int(w - 1, y)); }
        }

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (isOcean[nx, ny] || mapGenerator.IsLand(nx, ny)) continue;
                isOcean[nx, ny] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return isOcean;
    }

    /// <summary>
    /// Sadece okyanus su piksellerinden kıyıya olan mesafeyi hesaplar (BFS).
    /// Göl kıyıları int.MaxValue kalır — dalga efekti uygulanmaz.
    /// </summary>
    int[,] BuildWaterShoreDistField(int w, int h)
    {
        bool[,] isOcean = BuildOceanMask(w, h);

        int[,] dist = new int[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                dist[x, y] = int.MaxValue;

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };
        var queue = new Queue<Vector2Int>();

        // sadece okyanus-kara sınırındaki su pikselleri — mesafe 0
        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
        {
            if (!isOcean[x, y]) continue;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx4[i], ny = y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (mapGenerator.IsLand(nx, ny))
                {
                    dist[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                    break;
                }
            }
        }

        // BFS — sadece shoreWaveMaxDist kadar yay
        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            int d = dist[pos.x, pos.y];
            if (d >= shoreWaveMaxDist) continue;
            for (int i = 0; i < 4; i++)
            {
                int nx = pos.x + dx4[i], ny = pos.y + dy4[i];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (!isOcean[nx, ny]) continue;
                if (dist[nx, ny] <= d + 1) continue;
                dist[nx, ny] = d + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        return dist;
    }

    void Update()
    {
        if (!initialized || overlayMat == null) return;

        if (dayNight == null)
            dayNight = DayNightCycle.Instance;

        // Time.deltaTime pause'da 0 döner — animasyon durur
        gameTime += Time.deltaTime;

        UpdateMaterialProperties();
    }

    void UpdateMaterialProperties()
    {
        float ratio = (dayNight != null) ? dayNight.LightingRatio : 0f;

        overlayMat.SetColor("_WaveColor1", waveColorLight);
        overlayMat.SetColor("_WaveColor2", waveColorDark);
        overlayMat.SetColor("_FoamColor", foamColor);
        overlayMat.SetFloat("_WaveScale1", waveScale1);
        overlayMat.SetFloat("_WaveScale2", waveScale2);
        overlayMat.SetVector("_WaveSpeed1", waveSpeed1);
        overlayMat.SetVector("_WaveSpeed2", waveSpeed2);
        overlayMat.SetFloat("_FoamScale", foamScale);
        overlayMat.SetVector("_FoamSpeed", foamSpeed);
        overlayMat.SetFloat("_FoamThreshold", foamThreshold);
        overlayMat.SetFloat("_Intensity", intensity);
        overlayMat.SetFloat("_GameTime", gameTime);

        // kıyı dalgası — gece/gündüz renk geçişi
        Color blendedShoreColor = Color.Lerp(shoreWaveColor, shoreWaveColorNight, ratio);
        overlayMat.SetFloat("_ShoreWaveIntensity", shoreWaveIntensity);
        overlayMat.SetColor("_ShoreWaveColor", blendedShoreColor);
        overlayMat.SetFloat("_ShoreWaveSpeed", shoreWaveSpeed);
        overlayMat.SetFloat("_ShoreWaveFrequency", shoreWaveFrequency);
        overlayMat.SetFloat("_ShoreFoamIntensity", shoreFoamIntensity);
    }
}
