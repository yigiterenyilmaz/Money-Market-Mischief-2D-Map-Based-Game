using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Tek bir tahmin sorusunun tanımı. Inspector'dan düzenlenir.</summary>
[Serializable]
public class PredictionQuestion
{
    [TextArea(1, 3)] public string text;

    [Tooltip("Sorunun EVET çıkma GERÇEK olasılığı. Oyuncuya gösterilmez.")]
    [Range(0f, 1f)] public float yesChance = 0.5f;

    [Tooltip("Sonucun açıklanmasına kadar geçen süre (saniye).")]
    public float resolveSeconds = 90f;
}

/// <summary>Piyasada açık duran bir soru: tanım + oyuncunun oynadığı pozisyon + sayaç.</summary>
public class OpenPrediction
{
    public PredictionQuestion question;
    public float closesAt;        //Time.time cinsinden sonuç zamanı
    public float stake;           //oyuncunun yatırdığı para (0 = oynamadı)
    public bool betOnYes;
    public float yesPayout;       //EVET kazanırsa 1 birim başına dönen katsayı
    public float noPayout;

    public bool HasBet => stake > 0f;
    public float SecondsLeft => Mathf.Max(0f, closesAt - Time.time);
}

/// <summary>
/// TAHMİN PİYASASI — a17 "polymarket".
///
/// Ekranda birkaç evet/hayır sorusu açık durur. Oyuncu birine para yatırır, süre dolunca
/// sonuç açıklanır ve kazanan taraf katsayıyla ödenir. Katsayı sorunun GERÇEK olasılığından
/// türetilir ama oyuncuya gösterilen orana ev payı (houseEdge) bindirilir — uzun vadede
/// piyasa oyuncuya karşı hafif avantajlıdır, kâr bilgiden gelir, tekrardan değil.
///
/// Sorular Inspector'dan düzenlenebilir; kod içindeki liste yalnızca bir başlangıç setidir.
/// Gerçek oyun durumuna (şüphe, seçim, olay) bağlanmıyorlar — bkz. trading-readme.md.
///
/// Mantık burada, sunum PredictionMarketUI'dadır.
/// </summary>
public class PredictionMarketSystem : MonoBehaviour
{
    public static PredictionMarketSystem Instance { get; private set; }

    [Header("Piyasa")]
    [Tooltip("Aynı anda açık duran soru sayısı.")]
    public int openSlots = 3;

    [Tooltip("Ev payı: ödeme katsayısı bu oranda kısılır (0.05 = %5).")]
    [Range(0f, 0.3f)] public float houseEdge = 0.05f;

    [Tooltip("Bir soru kapandıktan sonra yerine yenisi gelene kadar geçen süre (saniye).")]
    public float refillDelay = 5f;

    [Header("Sorular")]
    [Tooltip("Havuz. Açık slotlar buradan rastgele doldurulur.")]
    public List<PredictionQuestion> questionPool = new List<PredictionQuestion>
    {
        new PredictionQuestion { text = "Borsa bu dönem yükselişle kapatacak mı?",       yesChance = 0.55f, resolveSeconds = 90f  },
        new PredictionQuestion { text = "Yeni vergi paketi meclisten geçecek mi?",       yesChance = 0.45f, resolveSeconds = 120f },
        new PredictionQuestion { text = "Büyük bir yolsuzluk skandalı patlayacak mı?",   yesChance = 0.30f, resolveSeconds = 150f },
        new PredictionQuestion { text = "Merkez bankası faizi indirecek mi?",            yesChance = 0.40f, resolveSeconds = 120f },
        new PredictionQuestion { text = "Kripto piyasası sert bir düşüş yaşayacak mı?",  yesChance = 0.35f, resolveSeconds = 90f  },
        new PredictionQuestion { text = "İmar planı bu dönem onaylanacak mı?",           yesChance = 0.60f, resolveSeconds = 150f },
        new PredictionQuestion { text = "Şehirde büyük bir protesto çıkacak mı?",        yesChance = 0.25f, resolveSeconds = 180f },
    };

    private bool unlocked;
    private readonly List<OpenPrediction> open = new List<OpenPrediction>();
    private float refillAt;

    /// <summary>Tahmin piyasası skill ile açıldı.</summary>
    public static event Action OnUnlocked;

    /// <summary>Açık soru listesi değişti (yeni soru, kapanan soru, yatırılan para).</summary>
    public static event Action OnMarketsChanged;

    /// <summary>Bir soru sonuçlandı: soru, sonuç EVET mi, oyuncunun kâr/zararı (oynamadıysa 0).</summary>
    public static event Action<OpenPrediction, bool, float> OnResolved;

    public bool IsUnlocked => unlocked;
    public IReadOnlyList<OpenPrediction> OpenPredictions => open;

    private void Awake()
    {
        //DİKKAT: Managers objesi paylaşımlı — Destroy(gameObject) oradaki tüm manager'ları silerdi.
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!unlocked) return;

        //süresi dolanları sonuçlandır
        for (int i = open.Count - 1; i >= 0; i--)
        {
            if (open[i].SecondsLeft > 0f) continue;

            Resolve(open[i]);
            open.RemoveAt(i);
        }

        //boş slotları doldur
        if (open.Count < openSlots && Time.time >= refillAt)
        {
            OpenNewPrediction();
            refillAt = Time.time + refillDelay;
        }
    }

    // ==================== AÇILIŞ ====================

    /// <summary>UnlockPredictionMarketEffect tarafından çağrılır.</summary>
    public void Unlock()
    {
        if (unlocked) return;

        unlocked = true;

        //ilk slotları hemen doldur, oyuncu paneli açtığında boş ekran görmesin
        while (open.Count < openSlots && OpenNewPrediction()) { }

        OnUnlocked?.Invoke();
    }

    // ==================== BAHİS ====================

    /// <summary>
    /// Açık bir soruya para yatırır. Bir soruya yalnızca bir kez oynanır — aynı soruya
    /// üst üste yatırmak katsayıyı ortalama alma oyununa çeviriyordu, tahmin oyununa değil.
    /// </summary>
    public bool PlaceBet(OpenPrediction prediction, float amount, bool onYes)
    {
        if (!unlocked) return false;
        if (prediction == null || !open.Contains(prediction)) return false;
        if (prediction.HasBet) return false;
        if (amount <= 0f) return false;
        if (GameStatManager.Instance == null) return false;
        if (!GameStatManager.Instance.HasEnoughWealth(amount)) return false;
        if (!GameStatManager.Instance.TrySpendWealth(amount)) return false;

        prediction.stake = amount;
        prediction.betOnYes = onYes;

        OnMarketsChanged?.Invoke();
        return true;
    }

    private void Resolve(OpenPrediction prediction)
    {
        bool outcomeYes = UnityEngine.Random.value < prediction.question.yesChance;
        float profit = 0f;

        if (prediction.HasBet)
        {
            bool won = prediction.betOnYes == outcomeYes;

            if (won)
            {
                float payout = prediction.betOnYes ? prediction.yesPayout : prediction.noPayout;
                float gross = prediction.stake * payout;

                GameStatManager.Instance?.AddWealth(gross);
                profit = gross - prediction.stake;
            }
            else
            {
                profit = -prediction.stake; //para yatırırken zaten düşüldü
            }
        }

        OnResolved?.Invoke(prediction, outcomeYes, profit);
        OnMarketsChanged?.Invoke();
    }

    // ==================== SORU ÜRETİMİ ====================

    private bool OpenNewPrediction()
    {
        if (questionPool == null || questionPool.Count == 0) return false;

        //ekranda duran sorunun aynısını tekrar açma
        List<PredictionQuestion> candidates = new List<PredictionQuestion>();
        for (int i = 0; i < questionPool.Count; i++)
        {
            if (questionPool[i] == null || string.IsNullOrEmpty(questionPool[i].text)) continue;
            if (IsOnScreen(questionPool[i])) continue;

            candidates.Add(questionPool[i]);
        }

        if (candidates.Count == 0) return false;

        PredictionQuestion picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        float yesChance = Mathf.Clamp(picked.yesChance, 0.05f, 0.95f);

        OpenPrediction prediction = new OpenPrediction
        {
            question = picked,
            closesAt = Time.time + Mathf.Max(5f, picked.resolveSeconds),
            //adil katsayı 1/olasılık; ev payı kadar kısılır
            yesPayout = 1f / yesChance * (1f - houseEdge),
            noPayout = 1f / (1f - yesChance) * (1f - houseEdge)
        };

        open.Add(prediction);
        OnMarketsChanged?.Invoke();
        return true;
    }

    private bool IsOnScreen(PredictionQuestion question)
    {
        for (int i = 0; i < open.Count; i++)
            if (open[i].question == question) return true;

        return false;
    }
}
