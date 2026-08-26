using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TAHMİN PİYASASI EKRANI — a17. Açık soruları satır satır listeler; her satırda soru,
/// iki katsayı, kalan süre ve EVET/HAYIR butonları vardır.
///
/// Panel a17'nin aktif yeteneğiyle açılır ve kapanırken haritayı geri verir.
/// Satırlar açık soru sayısı değiştikçe yeniden kurulur; sayaç ve durum metni her
/// karede değil, sabit aralıkla tazelenir.
/// </summary>
public class PredictionMarketUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Panel açıkken haritayı dondurmak için. Boş bırakılırsa sahnede aranır.")]
    public MapController mapController;

    [Header("Görünüm")]
    public float panelWidth = 720f;
    public float rowHeight = 74f;

    private const float REFRESH_INTERVAL = 0.2f;

    private GameObject panel;
    private Transform rowParent;
    private Text messageText;
    private InputField stakeField;

    private readonly List<Row> rows = new List<Row>();
    private float refreshTimer;
    private float messageHideAt;

    private class Row
    {
        public GameObject root;
        public Text question;
        public Text status;
        public Button yes;
        public Button no;
        public OpenPrediction bound;
    }

    private void Start()
    {
        Canvas canvas = TradeUIBuilder.FindCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[PredictionMarketUI] Sahnede Canvas yok — tahmin piyasası kurulamadı.", this);
            enabled = false;
            return;
        }

        if (mapController == null)
            mapController = FindFirstObjectByType<MapController>();

        Build(canvas.transform);
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        PredictionMarketSystem.OnMarketsChanged += Rebuild;
        PredictionMarketSystem.OnResolved += HandleResolved;
    }

    private void OnDisable()
    {
        PredictionMarketSystem.OnMarketsChanged -= Rebuild;
        PredictionMarketSystem.OnResolved -= HandleResolved;
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < REFRESH_INTERVAL) return;

        refreshTimer = 0f;
        RefreshRows();

        if (messageText.enabled && Time.unscaledTime >= messageHideAt)
            messageText.enabled = false;
    }

    // ==================== PANEL ====================

    /// <summary>OpenPredictionMarketEffect tarafından çağrılır.</summary>
    public void Open()
    {
        if (panel == null) return;

        //ağaçtan tetikleniyor: önce ağacı kapat, yoksa panel altında kalır ve kamera kilitli kalırdı
        if (UImanager.Instance != null)
            UImanager.Instance.OnSkillTreeClose();

        panel.SetActive(true);

        if (mapController != null)
            mapController.enable = false;

        Rebuild();
    }

    public void Close()
    {
        if (panel == null) return;

        panel.SetActive(false);

        if (mapController != null)
            mapController.enable = true;
    }

    // ==================== SATIRLAR ====================

    private void Rebuild()
    {
        if (panel == null || rowParent == null) return;

        PredictionMarketSystem system = PredictionMarketSystem.Instance;
        if (system == null) return;

        IReadOnlyList<OpenPrediction> markets = system.OpenPredictions;

        //eksik satırları kur, fazlalıkları gizle — her değişimde hepsini yıkmak GC üretiyordu
        while (rows.Count < markets.Count)
            rows.Add(BuildRow(rows.Count));

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < markets.Count;
            rows[i].root.SetActive(used);
            rows[i].bound = used ? markets[i] : null;
        }

        RefreshRows();
    }

    private void RefreshRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (row.bound == null || !row.root.activeSelf) continue;

            OpenPrediction prediction = row.bound;
            row.question.text = prediction.question.text;

            if (prediction.HasBet)
            {
                string side = prediction.betOnYes ? "EVET" : "HAYIR";
                float payout = prediction.betOnYes ? prediction.yesPayout : prediction.noPayout;
                float potential = prediction.stake * payout;

                row.status.text = $"{side} · {prediction.stake:N0} yatırıldı · kazanırsan {potential:N0}" +
                                  $"   ({prediction.SecondsLeft:N0} sn)";
                row.status.color = TradeUIBuilder.Warn;
            }
            else
            {
                row.status.text = $"EVET ×{prediction.yesPayout:N2}   ·   HAYIR ×{prediction.noPayout:N2}" +
                                  $"   ({prediction.SecondsLeft:N0} sn)";
                row.status.color = TradeUIBuilder.InkSoft;
            }

            row.yes.interactable = !prediction.HasBet;
            row.no.interactable = !prediction.HasBet;
        }
    }

    private void PlaceBet(int rowIndex, bool onYes)
    {
        if (rowIndex < 0 || rowIndex >= rows.Count) return;

        OpenPrediction prediction = rows[rowIndex].bound;
        if (prediction == null) return;

        PredictionMarketSystem system = PredictionMarketSystem.Instance;
        if (system == null) return;

        if (!float.TryParse(stakeField.text, out float amount) || amount <= 0f)
        {
            ShowMessage("Geçerli bir miktar gir.", TradeUIBuilder.Warn);
            return;
        }

        if (!system.PlaceBet(prediction, amount, onYes))
        {
            ShowMessage("Bahis yapılamadı — para yetersiz veya bu soruya zaten oynadın.",
                TradeUIBuilder.Loss);
            return;
        }

        ShowMessage($"{(onYes ? "EVET" : "HAYIR")} · {amount:N0} yatırıldı.", TradeUIBuilder.Profit);
        RefreshRows();
    }

    private void HandleResolved(OpenPrediction prediction, bool outcomeYes, float profit)
    {
        if (panel == null || !panel.activeSelf) return;

        string outcome = outcomeYes ? "EVET" : "HAYIR";

        if (Mathf.Approximately(profit, 0f))
            ShowMessage($"Sonuç: {outcome}. (Oynamadın.)", TradeUIBuilder.InkSoft);
        else if (profit > 0f)
            ShowMessage($"Sonuç: {outcome}. Kazandın +{profit:N0}", TradeUIBuilder.Profit);
        else
            ShowMessage($"Sonuç: {outcome}. Kaybettin {profit:N0}", TradeUIBuilder.Loss);
    }

    private void ShowMessage(string message, Color color)
    {
        messageText.text = message;
        messageText.color = color;
        messageText.enabled = true;
        messageHideAt = Time.unscaledTime + 4f;
    }

    // ==================== KURULUM ====================

    private void Build(Transform canvas)
    {
        panel = TradeUIBuilder.Panel("PredictionMarketPanel", canvas, new Color(0.06f, 0.06f, 0.08f, 0.98f));
        TradeUIBuilder.Place(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(panelWidth, 460f));

        Text title = TradeUIBuilder.Label("Title", panel.transform, 20, TextAnchor.MiddleLeft, Color.white);
        TradeUIBuilder.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -16f),
            new Vector2(panelWidth - 40f, 26f));
        title.text = "TAHMİN PİYASASI";

        Text hint = TradeUIBuilder.Label("Hint", panel.transform, 13, TextAnchor.MiddleLeft,
            TradeUIBuilder.InkSoft);
        TradeUIBuilder.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -44f),
            new Vector2(panelWidth - 40f, 20f));
        hint.text = "Miktarı gir, bir sorunun tarafını seç. Sonuç açıklanınca kazanan katsayıyla ödenir.";

        stakeField = TradeUIBuilder.NumberField("StakeField", panel.transform, "100");
        TradeUIBuilder.Place(stakeField.GetComponent<RectTransform>(), new Vector2(0f, 1f),
            new Vector2(20f, -72f), new Vector2(140f, 30f));

        messageText = TradeUIBuilder.Label("Message", panel.transform, 13, TextAnchor.MiddleLeft,
            TradeUIBuilder.Warn);
        TradeUIBuilder.Place(messageText.rectTransform, new Vector2(0f, 1f), new Vector2(176f, -72f),
            new Vector2(panelWidth - 200f, 30f));
        messageText.enabled = false;

        GameObject rowHost = new GameObject("Rows", typeof(RectTransform));
        rowHost.transform.SetParent(panel.transform, false);
        TradeUIBuilder.Place(rowHost.GetComponent<RectTransform>(), new Vector2(0f, 1f),
            new Vector2(0f, -110f), new Vector2(panelWidth, 300f));
        rowParent = rowHost.transform;

        Button close = TradeUIBuilder.Button("Close", panel.transform, "KAPAT",
            new Color(0.22f, 0.22f, 0.26f), Close);
        TradeUIBuilder.Place(close.GetComponent<RectTransform>(), new Vector2(1f, 0f),
            new Vector2(-20f, 16f), new Vector2(100f, 32f));
    }

    private Row BuildRow(int index)
    {
        Row row = new Row();

        GameObject card = TradeUIBuilder.Panel($"Row{index}", rowParent, new Color(0.12f, 0.12f, 0.15f));
        TradeUIBuilder.Place(card.GetComponent<RectTransform>(), new Vector2(0f, 1f),
            new Vector2(20f, -index * (rowHeight + 8f)), new Vector2(panelWidth - 40f, rowHeight));
        row.root = card;

        row.question = TradeUIBuilder.Label("Question", card.transform, 15, TextAnchor.MiddleLeft, Color.white);
        TradeUIBuilder.Place(row.question.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -10f),
            new Vector2(panelWidth - 260f, 22f));

        row.status = TradeUIBuilder.Label("Status", card.transform, 12, TextAnchor.MiddleLeft,
            TradeUIBuilder.InkSoft);
        TradeUIBuilder.Place(row.status.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -36f),
            new Vector2(panelWidth - 260f, 20f));

        int captured = index; //closure'ın döngü değişkenini değil bu satırın index'ini tutması için
        row.yes = TradeUIBuilder.Button("Yes", card.transform, "EVET", new Color(0.12f, 0.5f, 0.24f),
            () => PlaceBet(captured, true));
        TradeUIBuilder.Place(row.yes.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
            new Vector2(-118f, 0f), new Vector2(92f, 34f));

        row.no = TradeUIBuilder.Button("No", card.transform, "HAYIR", new Color(0.6f, 0.16f, 0.15f),
            () => PlaceBet(captured, false));
        TradeUIBuilder.Place(row.no.GetComponent<RectTransform>(), new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f), new Vector2(92f, 34f));

        return row;
    }
}
