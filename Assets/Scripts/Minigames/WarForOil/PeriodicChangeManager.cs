using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Choice'lardan tetiklenen süre bazlı periyodik stat değişikliklerini yönetir.
/// Her drain'in bir origin'i (War / WomanProcess) vardır — origin süreci bittiğinde drain iptal edilir.
/// Birden fazla drain aynı anda aktif olabilir, her biri bağımsız ilerler.
/// </summary>
public class PeriodicChangeManager : MonoBehaviour
{
    public static PeriodicChangeManager Instance { get; private set; }

    private List<ActiveDrain> activeDrains = new List<ActiveDrain>();

    //UI dinleyici — her tick'te action fırlatılır (None değilse)
    public static event Action<PeriodicChangeAction> OnPeriodicChangeTick;

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
        WarForOilManager.OnWarFinished += HandleWarFinished;
        WomanProcessManager.OnWomanProcessEnded += HandleWomanProcessEnded;
        WomanProcessManager.OnWomanProcessGameOver += HandleWomanProcessEnded;
    }

    private void OnDisable()
    {
        WarForOilManager.OnWarFinished -= HandleWarFinished;
        WomanProcessManager.OnWomanProcessEnded -= HandleWomanProcessEnded;
        WomanProcessManager.OnWomanProcessGameOver -= HandleWomanProcessEnded;
    }

    private void Update()
    {
        if (activeDrains.Count == 0) return;

        //ters yön — listeden silmek güvenli
        for (int i = activeDrains.Count - 1; i >= 0; i--)
        {
            ActiveDrain drain = activeDrains[i];
            drain.elapsed += Time.deltaTime;

            bool durationActive = drain.elapsed <= drain.duration + 0.0001f;
            bool anyPending = false;

            //her entry için tick biriktirme + tüketme
            if (drain.entries != null)
            {
                for (int e = 0; e < drain.entries.Count; e++)
                {
                    ActiveEntry entry = drain.entries[e];

                    if (entry.tickIntervalSeconds <= 0f)
                    {
                        if (entry.pendingTicks > 0) anyPending = true;
                        continue;
                    }

                    //tick due'larını biriktir — sadece duration aktif iken
                    if (durationActive)
                    {
                        entry.timer += Time.deltaTime;
                        while (entry.timer >= entry.tickIntervalSeconds)
                        {
                            entry.timer -= entry.tickIntervalSeconds;
                            entry.pendingTicks++;
                        }
                    }

                    //pending tick'leri uygun fazda tüket — frame başına 1
                    //Dusk/Dawn geçişlerinde bekler, fazına geçince ateşler
                    if (entry.pendingTicks > 0 && PhaseMatches(entry.dayPhasePreference))
                    {
                        entry.pendingTicks--;
                        ApplyTick(entry);
                    }

                    if (entry.pendingTicks > 0) anyPending = true;
                    drain.entries[e] = entry;
                }
            }

            //süre dolduktan sonra pending tick'lerin tükenmesini bekle.
            //Faz hiç gelmezse hard cutoff: 1 cycle uzunluğu kadar grace, sonra drain biter.
            if (!durationActive)
            {
                bool graceExpired = drain.elapsed >= drain.duration + drain.dayLengthSeconds;
                if (!anyPending || graceExpired)
                    activeDrains.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Verilen tercih, mevcut day/night cycle fazı için uygun mu?
    /// Either → her zaman uygun. DayNightCycle yoksa kısıtlama yok (Either gibi davranır).
    /// Dusk/Dawn geçişleri Day veya Night sayılmaz.
    /// </summary>
    private static bool PhaseMatches(DayPhasePreference preference)
    {
        if (preference == DayPhasePreference.Either) return true;
        if (DayNightCycle.Instance == null) return true;

        DayNightCycle.Phase current = DayNightCycle.Instance.CurrentPhase;
        if (preference == DayPhasePreference.Day) return current == DayNightCycle.Phase.Day;
        if (preference == DayPhasePreference.Night) return current == DayNightCycle.Phase.Night;
        return true;
    }

    /// <summary>
    /// Choice'ta tanımlı periyodik değişikliği başlatır. Origin, çağrıyı yapan manager tarafından belirlenir.
    /// Süre ve tick aralıkları DayNightCycle.TotalCycleLength üzerinden saniyeye çevrilir (Days birimi için).
    /// Aynı choice tekrar gelirse yeni drain instance olarak eklenir (üst üste biner).
    /// </summary>
    public void StartChange(WarForOilEventChoice choice, PeriodicChangeOrigin origin)
    {
        if (choice == null) return;
        if (!choice.hasPeriodicChanges) return;
        if (choice.periodicChangesDuration <= 0f) return;
        if (choice.periodicChanges == null || choice.periodicChanges.Count == 0) return;

        //dayLength'i bir kerelik fetch et — drain başlangıcında kilitlenir
        float dayLength = DayNightCycle.Instance != null ? DayNightCycle.Instance.TotalCycleLength : 0f;
        if (dayLength <= 0f)
        {
            //fallback: DayNightCycle yok veya henüz init olmamış
            dayLength = 130f;
            if (HasAnyDaysUnit(choice))
                Debug.LogWarning("[PeriodicChangeManager] DayNightCycle aktif degil, Days birimi 130sn fallback ile cevriliyor.");
        }

        float durationSeconds = ToSeconds(choice.periodicChangesDuration, choice.periodicChangesDurationUnit, dayLength);

        ActiveDrain drain = new ActiveDrain
        {
            origin = origin,
            duration = durationSeconds,
            elapsed = 0f,
            dayLengthSeconds = dayLength,
            entries = new List<ActiveEntry>(choice.periodicChanges.Count)
        };

        for (int i = 0; i < choice.periodicChanges.Count; i++)
        {
            PeriodicChangeEntry source = choice.periodicChanges[i];
            drain.entries.Add(new ActiveEntry
            {
                stat = source.stat,
                tickIntervalSeconds = ToSeconds(source.tickInterval, source.tickIntervalUnit, dayLength),
                amountPerTick = source.amountPerTick,
                action = source.action,
                dayPhasePreference = source.dayPhasePreference,
                timer = 0f,
                pendingTicks = 0
            });
        }

        activeDrains.Add(drain);
    }

    private static float ToSeconds(float value, PeriodicTimeUnit unit, float dayLength)
    {
        return unit == PeriodicTimeUnit.Days ? value * dayLength : value;
    }

    private static bool HasAnyDaysUnit(WarForOilEventChoice choice)
    {
        if (choice.periodicChangesDurationUnit == PeriodicTimeUnit.Days) return true;
        if (choice.periodicChanges != null)
        {
            for (int i = 0; i < choice.periodicChanges.Count; i++)
                if (choice.periodicChanges[i].tickIntervalUnit == PeriodicTimeUnit.Days) return true;
        }
        return false;
    }

    /// <summary>
    /// Şu an aktif drain sayısı.
    /// </summary>
    public int GetActiveDrainCount() => activeDrains.Count;

    // ==================== İPTAL ====================

    private void HandleWarFinished(WarForOilResult result)
    {
        CancelByOrigin(PeriodicChangeOrigin.War);
    }

    private void HandleWomanProcessEnded()
    {
        CancelByOrigin(PeriodicChangeOrigin.WomanProcess);
    }

    private void CancelByOrigin(PeriodicChangeOrigin origin)
    {
        for (int i = activeDrains.Count - 1; i >= 0; i--)
        {
            if (activeDrains[i].origin == origin)
                activeDrains.RemoveAt(i);
        }
    }

    // ==================== TICK UYGULAMA ====================

    private void ApplyTick(ActiveEntry entry)
    {
        bool applied = false;

        switch (entry.stat)
        {
            case PermanentMultiplierStatType.Wealth:
                if (GameStatManager.Instance != null)
                {
                    GameStatManager.Instance.AddWealth(entry.amountPerTick);
                    applied = true;
                }
                break;

            case PermanentMultiplierStatType.Suspicion:
                if (GameStatManager.Instance != null)
                {
                    if (entry.amountPerTick >= 0f)
                        GameStatManager.Instance.AddSuspicion(entry.amountPerTick);
                    else
                        GameStatManager.Instance.AddSuspicionRaw(entry.amountPerTick);
                    applied = true;
                }
                break;

            case PermanentMultiplierStatType.Reputation:
                if (GameStatManager.Instance != null)
                {
                    GameStatManager.Instance.AddReputation(entry.amountPerTick);
                    applied = true;
                }
                break;

            case PermanentMultiplierStatType.PoliticalInfluence:
                if (GameStatManager.Instance != null)
                {
                    GameStatManager.Instance.AddPoliticalInfluence(entry.amountPerTick);
                    applied = true;
                }
                break;

            case PermanentMultiplierStatType.WarSupport:
                //sadece savaş aktifken uygulanır — origin=War için savaş aktif olmalı,
                //origin=WomanProcess için savaş aktif olmayabilir
                if (WarForOilManager.Instance != null && WarForOilManager.Instance.IsActive())
                {
                    WarForOilManager.Instance.AddSupportRaw(entry.amountPerTick);
                    applied = true;
                }
                break;

            case PermanentMultiplierStatType.WomanObsession:
                //sadece kadın süreci aktifken uygulanır
                if (WomanProcessManager.Instance != null && WomanProcessManager.Instance.IsActive())
                {
                    WomanProcessManager.Instance.AddObsession(entry.amountPerTick);
                    applied = true;
                }
                break;
        }

        //action fırlatma — stat uygulanamadıysa da fırlatılır mı? Hayır, etki yoksa anlamsız.
        if (applied && entry.action != PeriodicChangeAction.None)
            OnPeriodicChangeTick?.Invoke(entry.action);
    }

    // ==================== İÇ TİPLER ====================

    private class ActiveDrain
    {
        public PeriodicChangeOrigin origin;
        public float duration;            //saniye (hesaplanmış)
        public float elapsed;
        public float dayLengthSeconds;    //drain başlangıcında kilitlenir — pending tick grace period'ı için
        public List<ActiveEntry> entries;
    }

    //timer değiştiği için struct, list içinde kopya-yaz pattern'i ile güncellenir
    private struct ActiveEntry
    {
        public PermanentMultiplierStatType stat;
        public float tickIntervalSeconds;        //saniye (hesaplanmış)
        public float amountPerTick;
        public PeriodicChangeAction action;
        public DayPhasePreference dayPhasePreference;
        public float timer;
        public int pendingTicks;                 //due olmuş ama henüz uygun fazda ateşlenmemiş tick sayısı
    }
}

/// <summary>
/// Bir periyodik değişikliği başlatan kaynak. Origin sürecinin bitmesi drain'i iptal eder.
/// </summary>
public enum PeriodicChangeOrigin
{
    War,            //savaş eventinden başlatıldı — savaş bitince iptal
    WomanProcess    //kadın eventinden (veya kadın eventinin precursor'ından) başlatıldı — kadın süreci bitince iptal
}
