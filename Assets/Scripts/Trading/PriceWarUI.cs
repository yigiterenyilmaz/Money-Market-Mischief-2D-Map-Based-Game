using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// FİYAT SAVAŞI ŞERİDİ — a15. Trade panelinin sol üstünde, savaş sürerken görünür.
///
/// Rakibin kalan gücü SAYI olarak gösterilmez; yalnızca bulanık bir cümleye çevrilir.
/// Oyuncunun elindeki tek sağlam sayı kendi yaktığı para ve bir sonraki baskının
/// maliyetidir — karar bu ikisinin üstünde verilir.
/// </summary>
public class PriceWarUI : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Şeridin kurulacağı trade paneli. Boş bırakılırsa CandlestickChart'tan alınır.")]
    public GameObject hostPanel;

    [Header("Görünüm")]
    public Vector2 margin = new Vector2(16f, -16f);
    public float panelWidth = 300f;

    private const float REFRESH_INTERVAL = 0.15f;

    private GameObject root;
    private Text stateText;
    private Text rivalText;
    private Text costText;
    private Button pushUpButton;
    private Button pushDownButton;
    private Button withdrawButton;
    private Text resultText;

    private float refreshTimer;
    private float resultHideAt;

    private void Start()
    {
        if (hostPanel == null)
        {
            //TradingSystem varsa referansını kullan, yoksa (veya alanı boşsa) kendin bul
            CandlestickChart chart = TradingSystem.Instance != null ? TradingSystem.Instance.chart : null;
            if (chart == null)
                chart = FindFirstObjectByType<CandlestickChart>();

            if (chart != null)
                hostPanel = chart.investmentPanel;
        }

        if (hostPanel == null)
        {
            Debug.LogWarning("[PriceWarUI] Trade paneli bulunamadı — fiyat savaşı şeridi kurulamadı.", this);
            enabled = false;
            return;
        }

        Build(hostPanel.transform);
        Refresh();
    }

    private void OnEnable()
    {
        PriceWarSystem.OnWarChanged += Refresh;
        PriceWarSystem.OnWarEnded += HandleWarEnded;
    }

    private void OnDisable()
    {
        PriceWarSystem.OnWarChanged -= Refresh;
        PriceWarSystem.OnWarEnded -= HandleWarEnded;
    }

    private void Update()
    {
        if (root == null) return;
        if (hostPanel != null && !hostPanel.activeSelf) return;

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < REFRESH_INTERVAL) return;

        refreshTimer = 0f;
        Refresh();

        if (resultText.enabled && Time.unscaledTime >= resultHideAt)
        {
            resultText.enabled = false;
            Refresh();
        }
    }

    // ==================== VERİ ====================

    private void Refresh()
    {
        if (root == null) return;

        PriceWarSystem war = PriceWarSystem.Instance;
        bool active = war != null && war.WarActive;

        root.SetActive(active || (resultText != null && resultText.enabled));

        if (!active)
        {
            pushUpButton.gameObject.SetActive(false);
            pushDownButton.gameObject.SetActive(false);
            withdrawButton.gameObject.SetActive(false);
            stateText.text = "";
            rivalText.text = "";
            costText.text = "";
            return;
        }

        //yön seçilmeden iki buton da açık; seçildikten sonra yalnızca o yön basılabilir
        pushUpButton.gameObject.SetActive(!war.DirectionChosen || war.PushingUp);
        pushDownButton.gameObject.SetActive(!war.DirectionChosen || !war.PushingUp);
        withdrawButton.gameObject.SetActive(true);

        string direction = !war.DirectionChosen
            ? "yön seçilmedi"
            : (war.PushingUp ? "ALIŞ baskısı" : "SATIŞ baskısı");

        stateText.text = $"FİYAT SAVAŞI · {direction}";
        costText.text = $"yakılan {war.Spent:N0}   ·   sonraki baskı {war.NextPushCost:N0}   ·   " +
                        $"kazanırsan {war.PotentialPayout:N0}";

        rivalText.text = DescribeRival(war.RivalIntegrity, war.DirectionChosen);
        rivalText.color = war.RivalIntegrity < 0.3f ? TradeUIBuilder.Profit
            : war.RivalIntegrity < 0.7f ? TradeUIBuilder.Warn
            : TradeUIBuilder.Loss;
    }

    /// <summary>Rakibin gücünü sayı vermeden anlatır — belirsizlik oyunun kendisi.</summary>
    private string DescribeRival(float integrity, bool engaged)
    {
        if (!engaged) return "Karşında biri var. Gücünü bilmiyorsun.";
        if (integrity > 0.85f) return "Rakip hiç sarsılmadı.";
        if (integrity > 0.6f) return "Rakip sağlam duruyor.";
        if (integrity > 0.35f) return "Rakip zorlanmaya başladı.";
        if (integrity > 0.15f) return "Rakip zayıflıyor.";

        return "Rakip kırılmak üzere.";
    }

    private void HandleWarEnded(bool won, float net)
    {
        if (resultText == null) return;

        resultText.text = won
            ? $"Rakip çekildi. Net {(net >= 0f ? "+" : "")}{net:N0}"
            : $"Çekildin. Yakılan {-net:N0} gitti.";
        resultText.color = won ? TradeUIBuilder.Profit : TradeUIBuilder.Loss;
        resultText.enabled = true;
        resultHideAt = Time.unscaledTime + 5f;

        Refresh();
    }

    private void OnPushUp() => PriceWarSystem.Instance?.Push(true);
    private void OnPushDown() => PriceWarSystem.Instance?.Push(false);
    private void OnWithdraw() => PriceWarSystem.Instance?.Withdraw();

    // ==================== KURULUM ====================

    private void Build(Transform host)
    {
        root = TradeUIBuilder.Panel("PriceWarPanel", host, TradeUIBuilder.Surface);
        TradeUIBuilder.Place(root.GetComponent<RectTransform>(), new Vector2(0f, 1f), margin,
            new Vector2(panelWidth, 132f));

        stateText = TradeUIBuilder.Label("State", root.transform, 15, TextAnchor.MiddleLeft, Color.white);
        TradeUIBuilder.Place(stateText.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -10f),
            new Vector2(panelWidth - 24f, 20f));

        rivalText = TradeUIBuilder.Label("Rival", root.transform, 14, TextAnchor.MiddleLeft,
            TradeUIBuilder.InkSoft);
        TradeUIBuilder.Place(rivalText.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -34f),
            new Vector2(panelWidth - 24f, 20f));

        costText = TradeUIBuilder.Label("Cost", root.transform, 12, TextAnchor.MiddleLeft,
            TradeUIBuilder.InkSoft);
        TradeUIBuilder.Place(costText.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -56f),
            new Vector2(panelWidth - 24f, 18f));

        resultText = TradeUIBuilder.Label("Result", root.transform, 14, TextAnchor.MiddleLeft,
            TradeUIBuilder.Profit);
        TradeUIBuilder.Place(resultText.rectTransform, new Vector2(0f, 1f), new Vector2(12f, -34f),
            new Vector2(panelWidth - 24f, 20f));
        resultText.enabled = false;

        float buttonWidth = (panelWidth - 36f) / 2f;

        pushUpButton = TradeUIBuilder.Button("PushUp", root.transform, "ALIŞ BASKISI",
            new Color(0.12f, 0.5f, 0.24f), OnPushUp);
        TradeUIBuilder.Place(pushUpButton.GetComponent<RectTransform>(), new Vector2(0f, 0f),
            new Vector2(12f, 44f), new Vector2(buttonWidth, 30f));

        pushDownButton = TradeUIBuilder.Button("PushDown", root.transform, "SATIŞ BASKISI",
            new Color(0.6f, 0.16f, 0.15f), OnPushDown);
        TradeUIBuilder.Place(pushDownButton.GetComponent<RectTransform>(), new Vector2(1f, 0f),
            new Vector2(-12f, 44f), new Vector2(buttonWidth, 30f));

        withdrawButton = TradeUIBuilder.Button("Withdraw", root.transform, "ÇEKİL",
            new Color(0.22f, 0.22f, 0.26f), OnWithdraw);
        TradeUIBuilder.Place(withdrawButton.GetComponent<RectTransform>(), new Vector2(0f, 0f),
            new Vector2(12f, 10f), new Vector2(panelWidth - 24f, 28f));

        root.SetActive(false);
    }
}
