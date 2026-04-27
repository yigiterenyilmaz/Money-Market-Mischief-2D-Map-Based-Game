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

            //her entry için tick kontrolü
            if (drain.entryStates != null)
            {
                for (int e = 0; e < drain.entryStates.Count; e++)
                {
                    EntryState state = drain.entryStates[e];
                    PeriodicChangeEntry entry = drain.entries[e];
                    if (entry.tickInterval <= 0f) continue;

                    state.timer += Time.deltaTime;
                    while (state.timer >= entry.tickInterval && drain.elapsed <= drain.duration + 0.0001f)
                    {
                        state.timer -= entry.tickInterval;
                        ApplyTick(entry);
                    }
                    drain.entryStates[e] = state;
                }
            }

            //süre dolduysa drain'i bitir
            if (drain.elapsed >= drain.duration)
            {
                activeDrains.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Choice'ta tanımlı periyodik değişikliği başlatır. Origin, çağrıyı yapan manager tarafından belirlenir.
    /// Aynı choice tekrar gelirse yeni drain instance olarak eklenir (üst üste biner).
    /// </summary>
    public void StartChange(WarForOilEventChoice choice, PeriodicChangeOrigin origin)
    {
        if (choice == null) return;
        if (!choice.hasPeriodicChanges) return;
        if (choice.periodicChangesDuration <= 0f) return;
        if (choice.periodicChanges == null || choice.periodicChanges.Count == 0) return;

        ActiveDrain drain = new ActiveDrain
        {
            origin = origin,
            duration = choice.periodicChangesDuration,
            elapsed = 0f,
            entries = new List<PeriodicChangeEntry>(choice.periodicChanges),
            entryStates = new List<EntryState>(choice.periodicChanges.Count)
        };
        for (int i = 0; i < choice.periodicChanges.Count; i++)
            drain.entryStates.Add(new EntryState { timer = 0f });

        activeDrains.Add(drain);
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

    private void ApplyTick(PeriodicChangeEntry entry)
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
        public float duration;
        public float elapsed;
        public List<PeriodicChangeEntry> entries;
        public List<EntryState> entryStates;
    }

    private struct EntryState
    {
        public float timer;
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
