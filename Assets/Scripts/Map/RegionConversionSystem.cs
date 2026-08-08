using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// BÖLGE DÖNÜŞÜMÜ — boş araziye SERBEST EL ile sınır çizip şehre veya sanayiye çevirme.
///
/// Akış:
///   1. Oyuncu skill ağacında a38/a40 node'una tıklar → aktif yetenek bu modu açar.
///   2. Harita üzerine bir maske biner: aynı türden MEVCUT bölgeler vurgulanır,
///      dönüştürülemeyen yerler (su, sis, başka biyom, yol kenarı) kısıtlı renkle işaretlenir.
///   3. Sol tuşu basılı tutup sınır çizilir. Bırakınca şekil OTOMATİK KAPANIR
///      (son nokta ilk noktaya bağlanır) ve içi taranarak doldurulur.
///   4. Fiyat kapalı alandaki DÖNÜŞTÜRÜLEBİLİR tile sayısıyla doğru orantılıdır.
///   5. Onaydan sonra alan hemen değişmez: 4 saniye boyunca duman çıkar (inşaat),
///      sonra bölge dönüşür.
///
/// "Aynı türden mevcut bölgeler sınır olarak kullanılabilir": çizim mevcut bir şehrin
/// üstünden geçebilir. O tile'lar zaten hedef türde olduğu için dönüştürülemez sayılır ve
/// atlanır — dolayısıyla mevcut bölge doğal bir duvar gibi davranır.
///
/// Çizim sol sürüklemeyi kullandığından mod boyunca kamera dondurulur.
/// </summary>
public class RegionConversionSystem : MonoBehaviour
{
    public static RegionConversionSystem Instance { get; private set; }

    public enum Phase { Idle, Drawing, AwaitingConfirm, Converting }

    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa Camera.main kullanılır.")]
    public Camera mapCamera;
    [Tooltip("Boş bırakılırsa sahnede aranır. Mod boyunca dondurulur.")]
    public MapController mapController;
    [Tooltip("Boş bırakılırsa sahnede aranır.")]
    public MapPainter mapPainter;

    [Header("Maliyet (tile başına — doğrusal)")]
    public float cityCostPerTile       = 900f;
    public float industrialCostPerTile = 650f;

    [Header("Sınırlar")]
    [Tooltip("Bu sayıdan az dönüştürülebilir tile varsa işlem reddedilir.")]
    public int minConvertibleTiles = 12;
    [Tooltip("Tek seferde dönüştürülebilecek en fazla tile — kazara tüm adayı seçmeye karşı.")]
    public int maxConvertibleTiles = 40000;
    [Tooltip("Çizim sırasında saklanan en fazla nokta.")]
    public int maxStrokePoints = 4000;
    [Tooltip("Canlı önizlemenin yeniden hesaplanma aralığı (saniye). Her karede hesaplamak " +
             "büyük alanlarda takılmaya yol açar.")]
    [Range(0.03f, 0.5f)] public float previewInterval = 0.10f;

    [Header("İnşaat")]
    [Tooltip("Onaydan dönüşüme kadar geçen süre (saniye).")]
    public float constructionSeconds = 4f;
    [Tooltip("Alan boyunca çıkan duman bulutu sayısı (üst sınır).")]
    public int maxSmokePuffs = 70;
    public Color smokeColor = new Color(0.78f, 0.76f, 0.72f, 1f);
    public Vector2 smokeRiseSpeed = new Vector2(0.15f, 0.45f);
    public Vector2 smokeLifetime  = new Vector2(1.1f, 2.2f);
    public Vector2 smokeSize      = new Vector2(0.25f, 0.75f);

    [Header("Maske Renkleri")]
    [Tooltip("Aynı türden MEVCUT bölge — sınır olarak kullanılabilir.")]
    public Color existingColor   = new Color(0.30f, 0.85f, 1f, 0.42f);
    [Tooltip("Dönüştürülemez alan — ÇAPRAZ TARAMA şeridi. Düz renk yeşil zeminde " +
             "fark edilmiyordu; şerit deseni her zemin üstünde 'yasak' okunur.")]
    public Color restrictedColor = new Color(0.92f, 0.15f, 0.15f, 0.55f);
    [Tooltip("Şeritler arası dolgu — kısıtlı alanın tamamı hafifçe kızarır.")]
    public Color restrictedFill  = new Color(0.75f, 0.10f, 0.10f, 0.22f);
    [Tooltip("Çapraz şerit periyodu (piksel).")]
    [Range(4, 32)] public int restrictedStripePeriod = 10;
    [Tooltip("Periyodun kaç pikseli dolu şerit olsun.")]
    [Range(1, 16)] public int restrictedStripeWidth = 4;
    [Tooltip("Dönüştürülebilir boş arazi. Şeffaf bırakmak haritayı açık gösterir.")]
    public Color convertibleColor = new Color(0f, 0f, 0f, 0f);

    [Header("Seçim Görseli")]
    public Color selectionFillValid   = new Color(0.45f, 0.95f, 0.65f, 0.38f);
    public Color selectionFillInvalid = new Color(1f, 0.45f, 0.35f, 0.38f);
    [Tooltip("Çizimin İÇİNDE kalan ama dönüştürülemeyen kareler (mevcut bölgeler, su, tarım, " +
             "yol). Seçimden ÇIKARILDIKLARI belli olsun diye çapraz taramayla kesilir.")]
    public Color selectionExcluded     = new Color(0.05f, 0.05f, 0.07f, 0.80f);
    [Tooltip("Elenen alanın şeritler arası dolgusu.")]
    public Color selectionExcludedFill = new Color(0.05f, 0.05f, 0.07f, 0.42f);
    public Color strokeColor = new Color(0.95f, 0.98f, 1f, 0.95f);
    public float strokeWidth = 0.06f;

    [Header("Katman Sıralaması")]
    public int overlaySortingOrder   = 29000;
    public int selectionSortingOrder = 29500;
    public int strokeSortingOrder    = 30000;
    public int smokeSortingOrder     = 30500;
    public float overlayZ = -6f;

    private bool cityUnlocked, industrialUnlocked;

    private MapDecorPlacer.ConvertTarget activeTarget;
    private Phase phase = Phase.Idle;

    private readonly List<Vector2Int> stroke        = new List<Vector2Int>();
    private readonly List<Vector2Int> selectedTiles = new List<Vector2Int>();
    //çizimin içinde kalan ama dönüştürülemeyen kareler — sadece görsel geri bildirim için
    private readonly List<Vector2Int> excludedTiles = new List<Vector2Int>();
    private RectInt selectionBounds;
    private float   pendingCost;
    private float   lastPreviewTime;

    private GameObject   overlayGo, selectionGo, strokeGo;
    private LineRenderer strokeLine;
    private Texture2D      overlayTex, selectionTex;
    private static Sprite  smokeSprite;

    //events
    public static event Action<MapDecorPlacer.ConvertTarget> OnTargetUnlocked;
    public static event Action<MapDecorPlacer.ConvertTarget> OnModeEntered;
    public static event Action OnModeExited;
    /// <summary>dönüştürülebilir tile, maliyet, onaylanabilir mi</summary>
    public static event Action<int, float, bool> OnSelectionChanged;
    public static event Action OnSelectionCleared;
    /// <summary>0..1 inşaat ilerlemesi</summary>
    public static event Action<float> OnConstructionProgress;
    /// <summary>hedef, dönüşen tile, ödenen</summary>
    public static event Action<MapDecorPlacer.ConvertTarget, int, float> OnConverted;

    public bool  IsModeActive => phase != Phase.Idle;
    public Phase CurrentPhase => phase;
    public MapDecorPlacer.ConvertTarget ActiveTarget => activeTarget;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            //sadece bileşeni yok et — paylaşılan Managers nesnesini götürmesin
            Debug.LogWarning("[RegionConversionSystem] Sahnede birden fazla kopya var — fazlası kaldırıldı.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private Camera ActiveCamera => mapCamera != null ? mapCamera : Camera.main;

    // -------------------------------------------------------------------------
    // AÇILIŞ
    // -------------------------------------------------------------------------

    public void Unlock(MapDecorPlacer.ConvertTarget target)
    {
        bool changed = false;

        if (target == MapDecorPlacer.ConvertTarget.Cities && !cityUnlocked)      { cityUnlocked = true; changed = true; }
        if (target == MapDecorPlacer.ConvertTarget.Industrial && !industrialUnlocked) { industrialUnlocked = true; changed = true; }

        if (changed) OnTargetUnlocked?.Invoke(target);
    }

    public bool IsUnlocked(MapDecorPlacer.ConvertTarget target)
        => target == MapDecorPlacer.ConvertTarget.Cities ? cityUnlocked : industrialUnlocked;

    // -------------------------------------------------------------------------
    // MOD
    // -------------------------------------------------------------------------

    /// <summary>Skill'in aktif yeteneği buraya girer.</summary>
    public void EnterMode(MapDecorPlacer.ConvertTarget target)
    {
        if (!IsUnlocked(target)) return;
        if (phase == Phase.Converting) return; //inşaat sürerken yeni mod açılmaz

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.CanConvert)
        {
            Debug.LogWarning("[RegionConversionSystem] Harita hazır değil — mod açılamadı.");
            return;
        }

        activeTarget = target;
        phase        = Phase.Drawing;

        //skill ağacı tam ekran bir panel; açık kalırsa oyuncu haritayı göremez.
        //Mod skill'e tıklanarak açıldığı için kapatma işini burada üstleniyoruz.
        //Ayrıca duraklat dışındaki arayüz gizlenir — çizim sırasında ağacı tekrar açmak
        //modu yarıda bırakırdı.
        if (UImanager.Instance != null) UImanager.Instance.SetMapToolMode(true);

        ClearSelectionVisuals();
        BuildOverlay();
        SetGamePaused(true); //sınır çizilirken dünya dursun

        OnModeEntered?.Invoke(target);
    }

    /// <summary>
    /// Seçim boyunca oyunu durdurur. Update() timeScale=0'da da çalıştığı için çizim
    /// etkilenmez; ama inşaat sayacı Time.deltaTime kullandığından ONAYDAN ÖNCE
    /// devam ettirilir — yoksa 4 saniye hiç dolmaz.
    /// </summary>
    private void SetGamePaused(bool paused)
    {
        if (GameManager.Instance == null) return;

        if (paused) GameManager.Instance.PauseGame();
        else        GameManager.Instance.ResumeGame();
    }

    public void ExitMode()
    {
        if (phase == Phase.Idle) return;
        if (phase == Phase.Converting) return; //inşaat iptal edilemez

        phase = Phase.Idle;
        ClearSelectionVisuals();
        DestroyOverlay();
        SetGamePaused(false);

        if (UImanager.Instance != null) UImanager.Instance.SetMapToolMode(false);

        OnModeExited?.Invoke();
    }

    // -------------------------------------------------------------------------
    // GİRDİ — SERBEST EL ÇİZİM
    // -------------------------------------------------------------------------

    private void Update()
    {
        if (phase == Phase.Idle || phase == Phase.Converting) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        //Esc iptal eder. Sağ tık artık ÇİZİM olduğu için iptal klavyeye taşındı;
        //panelde ayrıca Vazgeç butonu var.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (phase == Phase.AwaitingConfirm) { ClearSelection(); phase = Phase.Drawing; }
            else ExitMode();
            return;
        }

        if (phase != Phase.Drawing) return;

        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null) return;

        //SAĞ tuş çizer — SOL tuş kameraya bırakıldı ki çizim sırasında haritada gezilebilsin
        if (mouse.rightButton.wasPressedThisFrame && !IsPointerOverUI())
        {
            stroke.Clear();
            AppendStrokePoint(decor, mouse.position.ReadValue());
            EnsureStrokeLine();
            strokeGo.SetActive(true);
        }
        else if (mouse.rightButton.isPressed && stroke.Count > 0)
        {
            AppendStrokePoint(decor, mouse.position.ReadValue());

            //CANLI önizleme: çizim sürerken şekli geçici olarak kapatıp içini hesaplar,
            //böylece mevcut bölgelerin/suyun elendiği ANINDA görülür. Her karede yapmak
            //pahalı olduğu için kısıtlanır (oyun duraklı olduğundan unscaledTime).
            if (Time.unscaledTime - lastPreviewTime >= previewInterval)
            {
                lastPreviewTime = Time.unscaledTime;
                ComputeSelection(decor);
                DrawSelection(IsSelectionAcceptable());
                NotifySelection(selectedTiles.Count, pendingCost, IsSelectionAcceptable());
            }
        }
        else if (mouse.rightButton.wasReleasedThisFrame && stroke.Count > 0)
        {
            CloseAndFill();
        }
    }

    private static bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void AppendStrokePoint(MapDecorPlacer decor, Vector2 screenPos)
    {
        if (stroke.Count >= maxStrokePoints) return;

        Vector2Int tile = decor.ScreenToTile(ActiveCamera, screenPos);
        if (tile.x < 0) return;
        if (stroke.Count > 0 && stroke[stroke.Count - 1] == tile) return;

        stroke.Add(tile);
        RedrawStroke(decor, closed: false);
    }

    private void RedrawStroke(MapDecorPlacer decor, bool closed)
    {
        EnsureStrokeLine();

        int count = stroke.Count + (closed && stroke.Count > 1 ? 1 : 0);
        strokeLine.positionCount = count;

        for (int i = 0; i < stroke.Count; i++)
        {
            Vector3 p = decor.TileToWorldCenter(stroke[i]);
            p.z = overlayZ - 0.1f;
            strokeLine.SetPosition(i, p);
        }

        //kapanış: son nokta ilk noktaya bağlanır
        if (closed && stroke.Count > 1)
        {
            Vector3 p = decor.TileToWorldCenter(stroke[0]);
            p.z = overlayZ - 0.1f;
            strokeLine.SetPosition(count - 1, p);
        }
    }

    // -------------------------------------------------------------------------
    // KAPATMA + DOLDURMA
    // -------------------------------------------------------------------------

    private void CloseAndFill()
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null) return;

        RedrawStroke(decor, closed: true);
        ComputeSelection(decor);

        if (selectedTiles.Count == 0 && stroke.Count < 3)
        {
            NotifySelection(0, 0f, false);
            return;
        }

        bool ok = IsSelectionAcceptable();
        DrawSelection(ok);
        phase = Phase.AwaitingConfirm;

        NotifySelection(selectedTiles.Count, pendingCost, ok);
    }

    /// <summary>Seçim yeterli büyüklükte ve karşılanabilir mi.</summary>
    private bool IsSelectionAcceptable()
    {
        if (selectedTiles.Count < minConvertibleTiles) return false;

        return GameStatManager.Instance != null &&
               GameStatManager.Instance.HasEnoughWealth(pendingCost);
    }

    /// <summary>
    /// Çizimi (gerekirse geçici olarak) kapatıp içini tarar; dönüştürülebilir kareleri
    /// selectedTiles'a, elenenleri excludedTiles'a yazar ve fiyatı hesaplar.
    /// Hem canlı önizlemede hem bırakışta aynı kod çalışır ki ikisi asla ayrışmasın.
    /// </summary>
    private void ComputeSelection(MapDecorPlacer decor)
    {
        selectedTiles.Clear();
        excludedTiles.Clear();
        pendingCost = 0f;

        if (stroke.Count < 3)
        {
            selectionBounds = new RectInt(0, 0, 0, 0);
            return;
        }

        //poligonun sınır kutusu
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < stroke.Count; i++)
        {
            if (stroke[i].x < minX) minX = stroke[i].x;
            if (stroke[i].x > maxX) maxX = stroke[i].x;
            if (stroke[i].y < minY) minY = stroke[i].y;
            if (stroke[i].y > maxY) maxY = stroke[i].y;
        }

        selectionBounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);

        //tarama çizgisiyle doldur (çift-tek kuralı), sadece dönüştürülebilirleri topla
        var xs = new List<float>(16);

        for (int y = minY; y <= maxY; y++)
        {
            xs.Clear();

            for (int i = 0; i < stroke.Count; i++)
            {
                Vector2Int a = stroke[i];
                Vector2Int b = stroke[(i + 1) % stroke.Count]; //kapalı: son → ilk

                //yatay kenar taramaya katılmaz
                if (a.y == b.y) continue;

                //yarı-açık aralık: bir köşe iki kez sayılmasın
                bool crosses = (y >= Mathf.Min(a.y, b.y)) && (y < Mathf.Max(a.y, b.y));
                if (!crosses) continue;

                float t = (float)(y - a.y) / (b.y - a.y);
                xs.Add(a.x + t * (b.x - a.x));
            }

            if (xs.Count < 2) continue;
            xs.Sort();

            for (int i = 0; i + 1 < xs.Count; i += 2)
            {
                int xStart = Mathf.CeilToInt(xs[i]);
                int xEnd   = Mathf.FloorToInt(xs[i + 1]);

                for (int x = xStart; x <= xEnd; x++)
                {
                    Vector2Int tile = new Vector2Int(x, y);

                    //İçeride ama dönüştürülemez (mevcut şehir/sanayi, tarım, su, yol kenarı):
                    //seçimden ÇIKARILIR. Ayrı listede tutulur ki oyuncu neyin elendiğini görsün.
                    if (!decor.IsConvertible(tile, activeTarget)) { excludedTiles.Add(tile); continue; }

                    selectedTiles.Add(tile);
                    if (selectedTiles.Count >= maxConvertibleTiles) break;
                }

                if (selectedTiles.Count >= maxConvertibleTiles) break;
            }

            if (selectedTiles.Count >= maxConvertibleTiles) break;
        }

        //çizginin kendi üstündeki tile'lar da alana dahil (ince şeritler kaybolmasın).
        //HashSet ile bakılır: Contains(List) çizim uzadıkça karesel yavaşlıyordu.
        var picked = new HashSet<Vector2Int>(selectedTiles);
        for (int i = 0; i < stroke.Count; i++)
        {
            if (!decor.IsConvertible(stroke[i], activeTarget)) { excludedTiles.Add(stroke[i]); continue; }
            if (picked.Add(stroke[i])) selectedTiles.Add(stroke[i]);
        }

        pendingCost = selectedTiles.Count * GetCostPerTile(activeTarget);
    }

    private void NotifySelection(int count, float cost, bool ok)
        => OnSelectionChanged?.Invoke(count, cost, ok);

    public void ClearSelection()
    {
        selectedTiles.Clear();
        excludedTiles.Clear();
        stroke.Clear();
        ClearSelectionVisuals();
        OnSelectionCleared?.Invoke();
    }

    private void ClearSelectionVisuals()
    {
        if (strokeGo != null) strokeGo.SetActive(false);
        if (selectionGo != null) selectionGo.SetActive(false);

        //bir sonraki çizim eski yamayı temizlemek zorunda kalmasın
        lastDrawnBounds = new RectInt(0, 0, 0, 0);
    }

    public float GetCostPerTile(MapDecorPlacer.ConvertTarget target)
        => target == MapDecorPlacer.ConvertTarget.Cities ? cityCostPerTile : industrialCostPerTile;

    // -------------------------------------------------------------------------
    // ONAY + İNŞAAT
    // -------------------------------------------------------------------------

    /// <summary>UI'daki onay buraya bağlanır.</summary>
    public bool ConfirmSelection()
    {
        if (phase != Phase.AwaitingConfirm) return false;
        if (selectedTiles.Count < minConvertibleTiles) return false;

        GameStatManager stats = GameStatManager.Instance;
        if (stats == null || !stats.HasEnoughWealth(pendingCost)) return false;

        //para ŞİMDİ alınır: inşaat başladı, iptal edilemez
        if (!stats.TrySpendWealth(pendingCost)) return false;

        phase = Phase.Converting;
        if (strokeGo != null) strokeGo.SetActive(false);

        //inşaat sayacı ve duman gerçek zamanda aksın
        SetGamePaused(false);

        StartCoroutine(ConstructionRoutine(new List<Vector2Int>(selectedTiles), activeTarget, pendingCost));
        return true;
    }

    private IEnumerator ConstructionRoutine(List<Vector2Int> tiles, MapDecorPlacer.ConvertTarget target, float cost)
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;

        SpawnSmoke(decor, tiles);

        float duration = Mathf.Max(0.1f, constructionSeconds);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            OnConstructionProgress?.Invoke(Mathf.Clamp01(t / duration));
            yield return null;
        }
        OnConstructionProgress?.Invoke(1f);

        if (decor != null && decor.ConvertRegion(tiles, target, out RectInt affected))
        {
            if (mapPainter == null) mapPainter = FindAnyObjectByType<MapPainter>();
            if (mapPainter != null) mapPainter.RepaintBiomeRegion(affected);
        }

        //dönüşüm bitti — mod kapanır, maske kalkar
        phase = Phase.Idle;
        ClearSelection();
        DestroyOverlay();

        if (UImanager.Instance != null) UImanager.Instance.SetMapToolMode(false);

        OnConverted?.Invoke(target, tiles.Count, cost);
        OnModeExited?.Invoke();
    }

    // -------------------------------------------------------------------------
    // DUMAN
    // -------------------------------------------------------------------------

    private void SpawnSmoke(MapDecorPlacer decor, List<Vector2Int> tiles)
    {
        if (decor == null || tiles.Count == 0 || maxSmokePuffs <= 0) return;

        if (smokeSprite == null) smokeSprite = CreatePixelDisc(16);

        int puffs = Mathf.Min(maxSmokePuffs, tiles.Count);
        int step  = Mathf.Max(1, tiles.Count / puffs);

        for (int i = 0; i < tiles.Count; i += step)
        {
            Vector3 pos = decor.TileToWorldCenter(tiles[i]);
            pos.z = overlayZ - 0.2f;

            GameObject puff = new GameObject("ConstructionSmoke");
            puff.transform.SetParent(transform, false);
            puff.transform.position = pos;

            SpriteRenderer sr = puff.AddComponent<SpriteRenderer>();
            sr.sprite       = smokeSprite;
            sr.color        = smokeColor;
            sr.sortingOrder = smokeSortingOrder;

            SmokePuff behaviour = puff.AddComponent<SmokePuff>();
            behaviour.Launch(
                UnityEngine.Random.Range(smokeSize.x, smokeSize.y),
                UnityEngine.Random.Range(smokeRiseSpeed.x, smokeRiseSpeed.y),
                UnityEngine.Random.Range(smokeLifetime.x, smokeLifetime.y),
                constructionSeconds,
                smokeColor);
        }
    }

    // -------------------------------------------------------------------------
    // MASKE + SEÇİM KATMANLARI
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mod maskesini kurar: aynı türden mevcut bölgeler vurgulanır, dönüştürülemeyen
    /// alanlar kısıtlı renkle boyanır. Mod başına BİR kez üretilir.
    /// </summary>
    private void BuildOverlay()
    {
        MapDecorPlacer decor = MapDecorPlacer.Instance;
        if (decor == null || !decor.TryGetMapSize(out int w, out int h)) return;

        DestroyOverlay();

        overlayTex = NewOverlayTexture(w, h);
        Color[] pixels = new Color[w * h];

        int targetBiome = (int)activeTarget;
        int period = Mathf.Max(2, restrictedStripePeriod);
        int stripe = Mathf.Clamp(restrictedStripeWidth, 1, period - 1);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            Vector2Int tile = new Vector2Int(x, y);

            Color c;
            if (decor.GetBiomeAt(tile) == targetBiome)
            {
                c = existingColor; //aynı tür — sınır olarak kullanılabilir
            }
            else if (decor.IsConvertible(tile, activeTarget))
            {
                c = convertibleColor; //serbest boş arazi
            }
            else
            {
                //YASAK: tarım/sanayi/şehir/su/sis/yol kenarı hepsi buraya düşer.
                //Çapraz şerit deseni, altındaki zemin ne renk olursa olsun okunur.
                bool onStripe = ((x + y) % period) < stripe;
                c = onStripe ? restrictedColor : restrictedFill;
            }

            pixels[x + y * w] = c;
        }

        overlayTex.SetPixels(pixels);
        overlayTex.Apply();

        overlayGo = NewOverlayObject("RegionConversionOverlay", overlayTex, overlaySortingOrder, decor, w, h);

        //seçim katmanı (başlangıçta boş)
        selectionTex = NewOverlayTexture(w, h);
        Color[] blank = new Color[w * h];
        selectionTex.SetPixels(blank);
        selectionTex.Apply();

        selectionGo = NewOverlayObject("RegionConversionSelection", selectionTex, selectionSortingOrder, decor, w, h);
        selectionGo.SetActive(false);
    }

    private RectInt lastDrawnBounds = new RectInt(0, 0, 0, 0);

    private void DrawSelection(bool valid)
    {
        if (selectionTex == null || selectionGo == null) return;

        int w = selectionTex.width, h = selectionTex.height;
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill  = valid ? selectionFillValid : selectionFillInvalid;

        //Yalnızca ilgili dikdörtgenler dokunulur. Tüm dokuyu her seferinde yeniden yazmak
        //büyük haritada milyonlarca Color ayırıyordu (fark edilir bir takılma).
        ClearSelectionRect(lastDrawnBounds, clear);

        RectInt area = ExpandToTexture(selectionBounds, w, h);
        if (area.width <= 0 || area.height <= 0) { selectionGo.SetActive(false); return; }

        Color[] pixels = selectionTex.GetPixels(area.xMin, area.yMin, area.width, area.height);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        int period = Mathf.Max(2, restrictedStripePeriod);
        int stripe = Mathf.Clamp(restrictedStripeWidth, 1, period - 1);

        //önce elenenler — çapraz taramayla "kesildi" görünsün
        for (int i = 0; i < excludedTiles.Count; i++)
        {
            Vector2Int t = excludedTiles[i];
            if (!area.Contains(t)) continue;

            bool onStripe = ((t.x + t.y) % period) < stripe;
            pixels[(t.x - area.xMin) + (t.y - area.yMin) * area.width] =
                onStripe ? selectionExcluded : selectionExcludedFill;
        }

        //sonra seçilenler — üst üste binerse seçim kazanır
        for (int i = 0; i < selectedTiles.Count; i++)
        {
            Vector2Int t = selectedTiles[i];
            if (!area.Contains(t)) continue;

            pixels[(t.x - area.xMin) + (t.y - area.yMin) * area.width] = fill;
        }

        selectionTex.SetPixels(area.xMin, area.yMin, area.width, area.height, pixels);
        selectionTex.Apply();

        lastDrawnBounds = area;
        selectionGo.SetActive(true);
    }

    /// <summary>Sınır kutusunu doku sınırlarına kırpar.</summary>
    private static RectInt ExpandToTexture(RectInt r, int w, int h)
    {
        int xMin = Mathf.Clamp(r.xMin, 0, w);
        int yMin = Mathf.Clamp(r.yMin, 0, h);
        int xMax = Mathf.Clamp(r.xMax, 0, w);
        int yMax = Mathf.Clamp(r.yMax, 0, h);

        return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
    }

    private void ClearSelectionRect(RectInt r, Color clear)
    {
        if (selectionTex == null || r.width <= 0 || r.height <= 0) return;

        RectInt area = ExpandToTexture(r, selectionTex.width, selectionTex.height);
        if (area.width <= 0 || area.height <= 0) return;

        Color[] blank = new Color[area.width * area.height];
        for (int i = 0; i < blank.Length; i++) blank[i] = clear;

        selectionTex.SetPixels(area.xMin, area.yMin, area.width, area.height, blank);
    }

    private Texture2D NewOverlayTexture(int w, int h)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point, //harita piksel-art — maske de keskin kalsın
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave,
        };
    }

    private GameObject NewOverlayObject(string name, Texture2D tex, int sortingOrder,
                                        MapDecorPlacer decor, int w, int h)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                                  decor.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sr.sortingOrder = sortingOrder;

        //harita ile aynı merkez: MapDecorPlacer'ın origin'i haritanın ortasıdır
        Vector3 center = decor.transform.position;
        center.z = overlayZ;
        go.transform.position = center;

        return go;
    }

    private void DestroyOverlay()
    {
        if (overlayGo != null)   { Destroy(overlayGo);   overlayGo = null; }
        if (selectionGo != null) { Destroy(selectionGo); selectionGo = null; }

        if (overlayTex != null)   { Destroy(overlayTex);   overlayTex = null; }
        if (selectionTex != null) { Destroy(selectionTex); selectionTex = null; }
    }

    private void EnsureStrokeLine()
    {
        if (strokeGo != null) return;

        strokeGo = new GameObject("RegionConversionStroke");
        strokeGo.transform.SetParent(transform, false);

        strokeLine = strokeGo.AddComponent<LineRenderer>();
        strokeLine.useWorldSpace   = true;
        strokeLine.widthMultiplier = strokeWidth;
        strokeLine.numCapVertices  = 2;
        strokeLine.material        = new Material(Shader.Find("Sprites/Default"));
        strokeLine.startColor      = strokeColor;
        strokeLine.endColor        = strokeColor;
        strokeLine.sortingOrder    = strokeSortingOrder;
        strokeLine.positionCount   = 0;
    }

    /// <summary>Sert kenarlı, Point filtreli dolu daire (duman bulutu için).</summary>
    private static Sprite CreatePixelDisc(int size)
    {
        size = Mathf.Max(8, size);

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave,
        };

        float radius   = size * 0.5f - 0.5f;
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
            tex.SetPixel(x, y, d <= radius ? Color.white : Color.clear);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size),
                             new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private void OnDestroy()
    {
        DestroyOverlay();
    }
}
