using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TornadoSystem : MonoBehaviour
{
    public static TornadoSystem Instance { get; private set; }
    public static event Action OnTornadoStarted;
    public static event Action OnTornadoEnded;

    [Header("References")]
    public MapGenerator   mapGenerator;
    public MapPainter     mapPainter;
    public MapDecorPlacer mapDecorPlacer;
    public RoadGenerator  roadGenerator;

    [Header("Tornado Visuals")]
    public Sprite tornadoSprite;
    [Range(0.5f, 5f)]  public float tornadoScale = 1.5f;
    public int tornadoSortingOrder = 50;
    [Tooltip("Kendi ekseni etrafında dönme hızı (derece/saniye)")]
    [Range(90f, 1080f)] public float spinSpeed = 360f;

    [Header("Path Settings")]
    [Tooltip("Harita üzerindeki geçiş süresi (saniye)")]
    [Range(5f, 30f)] public float traverseTime = 12f;
    [Tooltip("Yay genişliği — harita boyutuna oran")]
    [Range(0.2f, 0.8f)] public float arcStrength = 0.4f;

    [Header("Damage Settings")]
    [Tooltip("Hortumun hasar yarıçapı (tile)")]
    [Range(2, 15)] public int damageRadius = 5;
    [Tooltip("Hasar kontrol sıklığı (saniye)")]
    [Range(0.1f, 1f)] public float damageCheckInterval = 0.3f;
    public Color crackColor = new Color(0.06f, 0.04f, 0.02f);

    [Header("Probability")]
    [Range(0f, 0.1f)] public float baseTornadoProbability = 0.02f;
    [Range(1, 5)]     public int   maxTornadoesPerSession = 2;

    [Header("Debug")]
    public Canvas debugCanvas;
    public Font   debugButtonFont;

    // ---

    private int  tornadoCount  = 0;
    private bool tornadoActive = false;
    private HashSet<Vector2Int> damagedRoadTiles = new HashSet<Vector2Int>();

    public IReadOnlyCollection<Vector2Int> DamagedRoadTiles => damagedRoadTiles;
    public bool IsTornadoActive => tornadoActive;

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (debugCanvas != null)
            CreateDebugButton();
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>Olasılık bazlı tetikleme. Oyun döngüsünden periyodik çağrılır.</summary>
    public bool TryTriggerTornado()
    {
        if (tornadoActive || tornadoCount >= maxTornadoesPerSession) return false;
        if (mapGenerator == null || mapDecorPlacer == null) return false;

        float chance = baseTornadoProbability;
        if (CountryData.Instance != null)
            chance *= CountryData.Instance.NaturalEventsIndex;

        if (UnityEngine.Random.value > chance) return false;

        TriggerTornado();
        return true;
    }

    /// <summary>Hortumu direkt başlatır.</summary>
    public void TriggerTornado()
    {
        if (tornadoActive) return;
        StartCoroutine(TornadoRoutine());
    }

    // =========================================================================
    // ANA COROUTINE
    // =========================================================================

    IEnumerator TornadoRoutine()
    {
        tornadoActive = true;
        tornadoCount++;
        OnTornadoStarted?.Invoke();

        Debug.Log($"TornadoSystem: Hortum başladı! (#{tornadoCount})");

        //yol hesapla
        Vector3 start, control, end;
        GenerateTornadoPath(out start, out control, out end);

        //hortum GameObject'i oluştur
        GameObject tornadoGO = new GameObject("Tornado");
        tornadoGO.transform.SetParent(mapDecorPlacer.transform);
        tornadoGO.transform.position = start;
        tornadoGO.transform.localScale = new Vector3(tornadoScale, tornadoScale, 1f);

        SpriteRenderer sr = tornadoGO.AddComponent<SpriteRenderer>();
        sr.sprite = tornadoSprite;
        sr.sortingOrder = tornadoSortingOrder;

        //harita boyut bilgileri
        float ppu = mapDecorPlacer.pixelsPerUnit;
        float halfW = mapGenerator.width  * 0.5f / ppu;
        float halfH = mapGenerator.height * 0.5f / ppu;

        float elapsed = 0f;
        float lastDamageTime = 0f;

        while (elapsed < traverseTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / traverseTime);

            //Bezier pozisyon
            Vector3 pos = EvaluateBezier(start, control, end, t);
            tornadoGO.transform.position = pos;

            //kendi ekseni etrafında sürekli dönme
            tornadoGO.transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);

            //periyodik hasar
            if (elapsed - lastDamageTime >= damageCheckInterval)
            {
                lastDamageTime = elapsed;
                ApplyDamageAtPosition(pos, ppu, halfW, halfH);
            }

            yield return null;
        }

        //temizlik
        if (tornadoGO != null) Destroy(tornadoGO);

        OnTornadoEnded?.Invoke();
        tornadoActive = false;

        Debug.Log("TornadoSystem: Hortum bitti.");
    }

    // =========================================================================
    // YOL HESAPLAMA
    // =========================================================================

    /// <summary>
    /// Quadratic Bezier eğrisi ile hortum rotası.
    /// Haritanın bir kenarından girer, yay çizerek diğer kenardan çıkar.
    /// </summary>
    void GenerateTornadoPath(out Vector3 start, out Vector3 control, out Vector3 end)
    {
        float ppu = mapDecorPlacer.pixelsPerUnit;
        float halfW = mapGenerator.width  * 0.5f / ppu;
        float halfH = mapGenerator.height * 0.5f / ppu;
        Vector3 anchor = mapDecorPlacer.transform.position;

        //giriş ve çıkış kenarlarını seç (farklı kenarlar)
        int entryEdge = UnityEngine.Random.Range(0, 4);
        //komşu kenar seç — karşı kenar düz çizgi yaratır, komşu kenar güzel yay yaratır
        int exitEdge = (entryEdge + (UnityEngine.Random.value > 0.5f ? 1 : 3)) % 4;

        start = GetEdgePoint(entryEdge, halfW, halfH, anchor);
        end   = GetEdgePoint(exitEdge,  halfW, halfH, anchor);

        //kontrol noktası — giriş-çıkış ortasına dik yönde offset
        Vector3 mid = (start + end) * 0.5f;
        Vector3 dir = (end - start).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float sign = UnityEngine.Random.value > 0.5f ? 1f : -1f;

        control = mid + perp * sign * arcStrength * Mathf.Max(halfW, halfH) * 2f;
    }

    Vector3 GetEdgePoint(int edge, float halfW, float halfH, Vector3 anchor)
    {
        float margin = 0.5f; //harita dışına biraz çık
        float x, y;

        switch (edge)
        {
            case 0: //üst
                x = anchor.x + UnityEngine.Random.Range(-halfW * 0.7f, halfW * 0.7f);
                y = anchor.y + halfH + margin;
                break;
            case 1: //sağ
                x = anchor.x + halfW + margin;
                y = anchor.y + UnityEngine.Random.Range(-halfH * 0.7f, halfH * 0.7f);
                break;
            case 2: //alt
                x = anchor.x + UnityEngine.Random.Range(-halfW * 0.7f, halfW * 0.7f);
                y = anchor.y - halfH - margin;
                break;
            default: //sol
                x = anchor.x - halfW - margin;
                y = anchor.y + UnityEngine.Random.Range(-halfH * 0.7f, halfH * 0.7f);
                break;
        }

        return new Vector3(x, y, anchor.z - 1f);
    }

    // =========================================================================
    // BEZIER
    // =========================================================================

    /// <summary>Quadratic Bezier: (1-t)²P0 + 2(1-t)tP1 + t²P2</summary>
    Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    // =========================================================================
    // HASAR
    // =========================================================================

    void ApplyDamageAtPosition(Vector3 worldPos, float ppu, float halfW, float halfH)
    {
        Vector3 anchor = mapDecorPlacer.transform.position;

        //world → tile dönüşümü
        int centerX = Mathf.RoundToInt((worldPos.x - anchor.x + halfW) * ppu);
        int centerY = Mathf.RoundToInt((worldPos.y - anchor.y + halfH) * ppu);

        int w = mapGenerator.width, h = mapGenerator.height;
        HashSet<Vector2Int> affectedTiles = new HashSet<Vector2Int>();

        int r = damageRadius;
        int rSq = r * r;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            if (dx * dx + dy * dy > rSq) continue;
            int tx = centerX + dx, ty = centerY + dy;
            if (tx < 0 || tx >= w || ty < 0 || ty >= h) continue;
            if (!mapGenerator.IsLand(tx, ty)) continue;
            affectedTiles.Add(new Vector2Int(tx, ty));
        }

        if (affectedTiles.Count == 0) return;

        //bina hasarı
        if (mapDecorPlacer != null)
            mapDecorPlacer.MarkBuildingsBroken(affectedTiles);

        //yol hasarı
        if (roadGenerator != null)
        {
            Texture2D tex = mapPainter?.GetMapTexture();
            int broken = 0;

            foreach (var tile in affectedTiles)
            {
                if (!roadGenerator.IsRoad(tile.x, tile.y)) continue;
                if (damagedRoadTiles.Contains(tile)) continue;
                damagedRoadTiles.Add(tile);
                broken++;

                if (tex != null)
                {
                    Color existing = tex.GetPixel(tile.x, tile.y);
                    tex.SetPixel(tile.x, tile.y, Color.Lerp(existing, crackColor, 0.9f));
                }
            }

            if (broken > 0)
            {
                tex?.Apply();
                UndergroundMapManager.Instance?.RefreshSurfaceSprite();
                RoadTrafficSystem.Instance?.OnRoadsBreaking(damagedRoadTiles);
            }
        }
    }

    // =========================================================================
    // DEBUG
    // =========================================================================

    void CreateDebugButton()
    {
        if (debugCanvas == null) return;

        GameObject btnGO = new GameObject("ForceTornadoBtn");
        btnGO.transform.SetParent(debugCanvas.transform, false);

        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(160f, 0f); //deprem butonunun yanına
        rt.sizeDelta = new Vector2(150f, 30f);

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.8f, 0.4f, 0.1f, 0.85f);

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() => TriggerTornado());

        GameObject txtGO = new GameObject("Text");
        txtGO.transform.SetParent(btnGO.transform, false);
        RectTransform trt = txtGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        Text txt = txtGO.AddComponent<Text>();
        txt.text      = "Force Tornado";
        txt.font      = debugButtonFont != null ? debugButtonFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 14;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
    }
}
