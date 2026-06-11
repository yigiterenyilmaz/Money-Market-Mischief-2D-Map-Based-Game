using System;
using System.Collections.Generic;
using UnityEngine;

public class PipeHuntManager : MonoBehaviour
{
    public static PipeHuntManager Instance { get; private set; }

    [Header("Referanslar")]
    public MiniGameData minigameData;
    public PipeHuntDatabase database;

    [Header("Debug")]
    [SerializeField] private bool bypassUnlockCheck = false;
    
    //state
    private PipeHuntState currentState = PipeHuntState.Idle;
    private List<PipeInstance> pipes = new List<PipeInstance>();
    private HuntTool currentTool;
    private int toolRemainingDurability;
    private float gameDuration;
    private float gameTimer;
    private float accumulatedIncome;

    //overtime state
    private float overtimeElapsed;//süre bittikten sonra oyuncunun kaç saniye daha fazla kaldığını tutar.
    private float totalSuspicionAdded; //overtime boyunca eklenen toplam şüphe miktarı

    //events — UI dinleyecek
    public static event Action<List<PipeInstance>, float, HuntTool> OnGameStarted; //borular, süre, seçilen alet
    public static event Action<float> OnTimerUpdate; //kalan süre
    public static event Action<PipeInstance, int> OnPipeHit; //vurulan boru, kalan boru dayanıklılığı
    public static event Action<PipeInstance> OnPipeBurst; //patlayan boru
    public static event Action<int> OnEmptyHit; //boş zemine vuruldu, kalan alet dayanıklılığı
    public static event Action<int> OnToolDamaged; //alet hasar aldı, kalan dayanıklılık
    public static event Action OnToolBroken; //alet kırıldı
    public static event Action<float> OnIncomeUpdate; //toplam biriken gelir
    public static event Action OnOvertimeStarted; //süre doldu, overtime başladı
    public static event Action<float, float> OnOvertimeUpdate; //overtime geçen süre, toplam eklenen şüphe
    public static event Action<PipeHuntResult> OnGameFinished; //minigame bitti

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        GameStatManager.OnGameOver += HandleGameOver;
    }

    private void OnDisable()
    {
        GameStatManager.OnGameOver -= HandleGameOver;
    }

    /// <summary>
    /// Şüphe %100'e ulaştığında GameStatManager tarafından tetiklenir.
    /// Minigame aktifse game over olarak bitirir.
    /// </summary>
    private void HandleGameOver()
    {
        if (!IsPlaying()) return;

        FinishGame(PipeHuntEndReason.GameOver);
    }

    private void Update()
    {
        if (currentState == PipeHuntState.Active)
        {
            //süre sayacı (oyun duraklatılmış, unscaled kullan)
            gameTimer -= Time.unscaledDeltaTime;
            OnTimerUpdate?.Invoke(gameTimer);

            //patlayan borulardan gelir biriktir
            AccumulateIncome();

            //süre doldu → overtime'a geç
            if (gameTimer <= 0f)
            {
                gameTimer = 0f;
                EnterOvertime();
            }
        }
        else if (currentState == PipeHuntState.Overtime)
        {
            //overtime sayacı
            overtimeElapsed += Time.unscaledDeltaTime;

            //patlayan borulardan gelir biriktir (overtime'da da devam eder)
            AccumulateIncome();

            //şüphe biriktir
            AccumulateSuspicion();
        }
    }

    // ==================== UI'IN ÇAĞIRDIĞI METODLAR ====================

    /// <summary>
    /// Oyuncu alet seçim ekranından bir alet seçip "Başla" butonuna bastığında UI bu metodu çağırır.
    /// Seçilen aletin maliyeti wealth'den kesilir, borular zemine yerleştirilir,
    /// süre aletin stealth değerine göre hesaplanır ve minigame başlar.
    /// </summary>
    public void StartGame(HuntTool tool)
    {
        Debug.Log($"[PipeHunt] StartGame called with tool: {tool?.displayName ?? "NULL"}");

        if (currentState != PipeHuntState.Idle)
        {
            Debug.Log($"[PipeHunt] BLOCKED - State is {currentState}, not Idle");
            return;
        }

        if (tool == null)
        {
            Debug.Log("[PipeHunt] BLOCKED - Tool is null");
            return;
        }

        if (MinigameManager.Instance != null && !bypassUnlockCheck)
        {
            if (!MinigameManager.Instance.IsMinigameUnlocked(minigameData))
            {
                Debug.Log("[PipeHunt] BLOCKED - Minigame not unlocked");
                return;
            }
            if (MinigameManager.Instance.IsOnCooldown(minigameData))
            {
                Debug.Log("[PipeHunt] BLOCKED - Minigame on cooldown");
                return;
            }
        }

        if (GameStatManager.Instance != null)
        {
            if (!GameStatManager.Instance.HasEnoughWealth(tool.cost))
            {
                Debug.Log($"[PipeHunt] BLOCKED - Not enough wealth for cost {tool.cost}");
                return;
            }
            GameStatManager.Instance.TrySpendWealth(tool.cost);
        }

        Debug.Log("[PipeHunt] All checks passed, starting game...");

        //seçilen aleti kaydet
        currentTool = tool;

        //boruları yerleştir
        GeneratePipes();

        //alet dayanıklılığı ve süre hesabı
        toolRemainingDurability = tool.durability;
        gameDuration = Mathf.Lerp(database.minGameDuration, database.maxGameDuration, tool.stealth);
        gameTimer = gameDuration;
        accumulatedIncome = 0f;
        overtimeElapsed = 0f;
        totalSuspicionAdded = 0f;

        currentState = PipeHuntState.Active;

        //ana oyunu duraklat — minigame kendi zamanını unscaled ile yönetir
        if (GameManager.Instance != null)
            GameManager.Instance.PauseGame();

        OnGameStarted?.Invoke(new List<PipeInstance>(pipes), gameDuration, currentTool);
    }

    /// <summary>
    /// Oyuncu ekrana dokundu ve dokunduğu yer bir borunun üzerine denk geldi.
    /// UI dokunma pozisyonunu kontrol eder, bir boruya denk geldiyse o borunun id'sini
    /// bu metoda gönderir. Boru hasar alır, alet aşınır. Boru patlarsa gelir üretmeye başlar.
    /// </summary>
    public void HitPipe(int pipeId)
    {
        if (!IsPlaying()) return;
        if (toolRemainingDurability <= 0) return; //alet kırıksa vuruş yapılamaz

        PipeInstance pipe = GetPipeById(pipeId);
        if (pipe == null || pipe.isBurst) return;

        //boruya hasar ver (aletin vuruş hasarı kadar)
        pipe.remainingDurability -= currentTool.damagePerHit;

        //alete hasar ver (her vuruş 1 dayanıklılık düşürür)
        toolRemainingDurability--;
        OnToolDamaged?.Invoke(toolRemainingDurability);

        if (pipe.remainingDurability <= 0)
        {
            //boru patladı
            pipe.remainingDurability = 0;
            pipe.isBurst = true;
            pipe.burstTime = Time.unscaledTime;
            OnPipeBurst?.Invoke(pipe);
        }
        else
        {
            OnPipeHit?.Invoke(pipe, pipe.remainingDurability);
        }

        //alet kırıldı — oyun devam eder, sadece vuruş yapılamaz
        if (toolRemainingDurability <= 0)
        {
            toolRemainingDurability = 0;
            OnToolBroken?.Invoke();
        }
    }

    /// <summary>
    /// Oyuncu ekrana dokundu ama dokunduğu yer hiçbir borunun üzerinde değil.
    /// UI dokunma pozisyonunu kontrol eder, hiçbir boruya denk gelmediyse bu metodu çağırır.
    /// Alet yine aşınır ama hiçbir boru hasar almaz — boşa vuruş.
    /// </summary>
    public void HitEmpty()
    {
        if (!IsPlaying()) return;
        if (toolRemainingDurability <= 0) return; //alet kırıksa vuruş yapılamaz

        //alete hasar ver (her vuruş 1 dayanıklılık düşürür)
        toolRemainingDurability--;
        OnToolDamaged?.Invoke(toolRemainingDurability);
        OnEmptyHit?.Invoke(toolRemainingDurability);

        //alet kırıldı — oyun devam eder, sadece vuruş yapılamaz
        if (toolRemainingDurability <= 0)
        {
            toolRemainingDurability = 0;
            OnToolBroken?.Invoke();
        }
    }

    /// <summary>
    /// Oyuncu "Çık" butonuna bastığında UI bu metodu çağırır.
    /// Süre dolmadan da çıkabilir (kazancını alır), overtime'da da çıkabilir (şüphe durur).
    /// Minigame sonlanır, gelir wealth'e eklenir, ana oyun devam eder.
    /// </summary>
    public void LeaveGame()
    {
        if (!IsPlaying()) return;

        FinishGame(PipeHuntEndReason.PlayerLeft);
    }

    // ==================== UI İÇİN GETTER'LAR ====================

    /// <summary>
    /// Oyuncunun seçebileceği aletlerin listesini döner.
    /// </summary>
    public List<HuntTool> GetAvailableTools()
    {
        return database.tools;
    }

    /// <summary>
    /// Belirli bir alet için hesaplanan oyun süresini döner.
    /// </summary>
    public float GetToolDuration(HuntTool tool)
    {
        if (tool == null) return 0f;
        return Mathf.Lerp(database.minGameDuration, database.maxGameDuration, tool.stealth);
    }

    // ==================== İÇ MANTIK ====================

    /// <summary>
    /// Active veya Overtime state'inde mi kontrolü.
    /// </summary>
    private bool IsPlaying()
    {
        return currentState == PipeHuntState.Active || currentState == PipeHuntState.Overtime;
    }

    /// <summary>
    /// Süre dolduğunda Overtime state'ine geçer.
    /// </summary>
    private void EnterOvertime()
    {
        currentState = PipeHuntState.Overtime;
        overtimeElapsed = 0f;
        OnOvertimeStarted?.Invoke();
    }

    /// <summary>
    /// Boruları rastgele yerleştirir. Borular arası minimum mesafe kontrolü yapar.
    /// </summary>
    private void GeneratePipes()
    {
        pipes.Clear();

        if (database.pipeTypes == null || database.pipeTypes.Count == 0) return;

        int maxAttempts = 100; //sonsuz döngü koruması

        int pipeCount = UnityEngine.Random.Range(database.minPipeCount, database.maxPipeCount + 1);
        for (int i = 0; i < pipeCount; i++)
        {
            //rastgele boru tipi
            PipeType pipeType = database.pipeTypes[UnityEngine.Random.Range(0, database.pipeTypes.Count)];

            //rastgele pozisyon — minimum mesafe kontrolü
            Vector2 position = Vector2.zero;
            bool validPosition = false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                position = new Vector2(
                    UnityEngine.Random.Range(0.05f, 0.95f),
                    UnityEngine.Random.Range(0.05f, 0.95f)
                );

                if (IsPositionValid(position))
                {
                    validPosition = true;
                    break;
                }
            }

            if (!validPosition) continue; //yer bulunamadıysa bu boruyu atla

            PipeInstance pipe = new PipeInstance(i, pipeType, position);
            pipes.Add(pipe);
        }
    }

    /// <summary>
    /// Pozisyonun mevcut borulara yeterince uzak olup olmadığını kontrol eder.
    /// </summary>
    private bool IsPositionValid(Vector2 position)
    {
        for (int i = 0; i < pipes.Count; i++)
        {
            if (Vector2.Distance(position, pipes[i].position) < database.minPipeDistance)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Patlayan borulardan gelir biriktirir.
    /// </summary>
    private void AccumulateIncome()
    {
        float incomeThisFrame = 0f;

        for (int i = 0; i < pipes.Count; i++)
        {
            if (pipes[i].isBurst)
            {
                incomeThisFrame += pipes[i].pipeType.incomePerSecond * Time.unscaledDeltaTime;
            }
        }

        if (incomeThisFrame > 0f)
        {
            accumulatedIncome += incomeThisFrame;
            OnIncomeUpdate?.Invoke(accumulatedIncome);
        }
    }

    /// <summary>
    /// Overtime sırasında şüphe biriktirir.
    /// Formül: base * (overtimePercent / 100) ^ exponent
    /// Her frame hedef toplam şüpheyi hesaplar, farkı GameStatManager'a ekler.
    /// </summary>
    private void AccumulateSuspicion()
    {
        float overtimePercent = (overtimeElapsed / gameDuration) * 100f;
        float targetSuspicion = database.suspicionBase * Mathf.Pow(overtimePercent / 100f, database.suspicionExponent);
        float delta = targetSuspicion - totalSuspicionAdded;

        if (delta > 0f)
        {
            totalSuspicionAdded = targetSuspicion;

            if (GameStatManager.Instance != null)
                GameStatManager.Instance.AddSuspicion(delta);

            //AddSuspicion game over tetiklemiş olabilir — state değiştiyse devam etme
            if (currentState != PipeHuntState.Overtime) return;

            OnOvertimeUpdate?.Invoke(overtimeElapsed, totalSuspicionAdded);
        }
    }

    /// <summary>
    /// Minigame'i bitirir. Toplam geliri wealth'e ekler, cooldown başlatır.
    /// </summary>
    private void FinishGame(PipeHuntEndReason reason)
    {
        currentState = PipeHuntState.Finished;

        //patlayan boru sayısı
        int burstCount = 0;
        for (int i = 0; i < pipes.Count; i++)
        {
            if (pipes[i].isBurst) burstCount++;
        }

        //wealth'e ekle
        if (GameStatManager.Instance != null && accumulatedIncome > 0f)
            GameStatManager.Instance.AddWealth(accumulatedIncome);

        //cooldown başlat
        if (MinigameManager.Instance != null)
            MinigameManager.Instance.StartCooldown(minigameData);

        //sonucu bildir
        PipeHuntResult result = new PipeHuntResult();
        result.totalIncome = accumulatedIncome;
        result.burstPipeCount = burstCount;
        result.totalPipeCount = pipes.Count;
        result.endReason = reason;
        result.remainingTime = gameTimer;
        result.toolUsed = currentTool;
        result.toolCostPaid = currentTool != null ? currentTool.cost : 0;
        result.overtimeElapsed = overtimeElapsed;
        result.suspicionAdded = totalSuspicionAdded;

        OnGameFinished?.Invoke(result);

        //ana oyunu devam ettir (game over değilse — game over'da oyun zaten bitti, resume yapma)
        if (reason != PipeHuntEndReason.GameOver && GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        //state'i sıfırla
        currentState = PipeHuntState.Idle;
        currentTool = null;
        pipes.Clear();
    }

    private PipeInstance GetPipeById(int id)
    {
        for (int i = 0; i < pipes.Count; i++)
        {
            if (pipes[i].id == id) return pipes[i];
        }
        return null;
    }

    // ==================== GETTER'LAR ====================

    public PipeHuntState GetCurrentState()
    {
        return currentState;
    }

    public List<PipeInstance> GetPipes()
    {
        return new List<PipeInstance>(pipes);
    }

    public int GetToolRemainingDurability()
    {
        return toolRemainingDurability;
    }

    public float GetAccumulatedIncome()
    {
        return accumulatedIncome;
    }

    public float GetRemainingTime()
    {
        return gameTimer;
    }

    public HuntTool GetCurrentTool()
    {
        return currentTool;
    }

    public float GetOvertimeElapsed()
    {
        return overtimeElapsed;
    }

    public float GetTotalSuspicionAdded()
    {
        return totalSuspicionAdded;
    }
}

public enum PipeHuntState
{
    Idle,       //minigame aktif değil
    Active,     //oyun devam ediyor (süre içinde)
    Overtime,   //süre doldu ama oyuncu devam ediyor (şüphe artıyor)
    Finished    //oyun bitti (geçiş anı)
}

public enum PipeHuntEndReason
{
    PlayerLeft,   //oyuncu kendi çıktı
    GameOver      //şüphe %100'e ulaştı — tüm oyun bitti
}

[System.Serializable]
public class PipeHuntResult
{
    public float totalIncome;       //toplam kazanılan gelir
    public int burstPipeCount;      //patlayan boru sayısı
    public int totalPipeCount;      //toplam boru sayısı
    public PipeHuntEndReason endReason; //bitiş sebebi
    public float remainingTime;     //kalan süre (süre içinde çıktıysa > 0)
    public HuntTool toolUsed;       //kullanılan alet
    public int toolCostPaid;        //ödenen alet maliyeti
    public float overtimeElapsed;   //süre aşımında ne kadar kaldı
    public float suspicionAdded;    //eklenen toplam şüphe
}
