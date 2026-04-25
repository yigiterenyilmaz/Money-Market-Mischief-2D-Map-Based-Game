using System;
using System.Collections.Generic;
using UnityEngine;

// Pattern secim ve cooldown yoneticisi.
// CandlestickChart her mum kapanisinda OnIdleCandle veya ActivePattern.OnCandleClosed cagirir.
// Yeni pattern spawn'i: idle mum sayisi cooldown'i gecince agirlikli rastgele secim yapar.

public class PatternScheduler
{
    readonly int cooldownMin;
    readonly int cooldownMax;
    readonly float volatilityMultiplier;

    readonly List<ChartPattern> registered = new List<ChartPattern>();

    ChartPattern activePattern;
    int idleCounter;
    int currentCooldown;
    readonly Queue<string> recentlyUsed = new Queue<string>();
    const int RecentlyUsedLimit = 3;

    public ChartPattern ActivePattern => activePattern;
    public bool HasActivePattern => activePattern != null;

    public PatternScheduler(int cooldownMin, int cooldownMax, float volatilityMultiplier)
    {
        this.cooldownMin = Mathf.Max(1, cooldownMin);
        this.cooldownMax = Mathf.Max(this.cooldownMin, cooldownMax);
        this.volatilityMultiplier = volatilityMultiplier;
        idleCounter = 0;
        currentCooldown = UnityEngine.Random.Range(this.cooldownMin, this.cooldownMax + 1);
    }

    public void RegisterAll(ChartPattern[] patterns)
    {
        if (patterns == null) return;
        foreach (ChartPattern p in patterns)
            if (p != null) registered.Add(p);
    }

    // Pattern aktif degilken her mum kapanisinda cagrilir.
    public void OnIdleCandle(MarketState marketState)
    {
        idleCounter++;
        if (idleCounter < currentCooldown) return;

        // Pattern adaylari: context uygun + son kullanilanda yok
        MarketContext context = marketState.GetContext();
        List<ChartPattern> candidates = new List<ChartPattern>();
        float totalWeight = 0f;
        foreach (ChartPattern p in registered)
        {
            if (recentlyUsed.Contains(p.Id)) continue;
            if (!p.MatchesContext(context)) continue;
            candidates.Add(p);
            totalWeight += p.Weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
        {
            // Bu turda pattern uygun degil, kisa cooldown ile tekrar dene
            idleCounter = 0;
            currentCooldown = Mathf.Max(2, cooldownMin / 2);
            return;
        }

        // Agirlikli rastgele secim
        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;
        ChartPattern selected = candidates[candidates.Count - 1];
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += candidates[i].Weight;
            if (roll <= cumulative)
            {
                selected = candidates[i];
                break;
            }
        }

        ActivateNew(selected, marketState);
    }

    void ActivateNew(ChartPattern template, MarketState marketState)
    {
        ChartPattern instance = (ChartPattern)Activator.CreateInstance(template.GetType());
        instance.Init(marketState, marketState.currentPrice, volatilityMultiplier);
        activePattern = instance;

        recentlyUsed.Enqueue(instance.Id);
        while (recentlyUsed.Count > RecentlyUsedLimit)
            recentlyUsed.Dequeue();

        Debug.Log($"[Pattern] {instance.Id} started, P0={marketState.currentPrice:F2}, totalCandles~{instance.TotalCandles}");
    }

    public void MarkActiveDone()
    {
        if (activePattern != null)
            Debug.Log($"[Pattern] {activePattern.Id} done");
        activePattern = null;
        idleCounter = 0;
        currentCooldown = UnityEngine.Random.Range(cooldownMin, cooldownMax + 1);
    }

    // Debug/test icin: kayitli pattern'i hemen tetikle.
    public bool TryForcePattern(string id, MarketState marketState)
    {
        if (activePattern != null) return false;
        ChartPattern template = registered.Find(p => p.Id == id);
        if (template == null)
        {
            Debug.LogWarning($"[Pattern] '{id}' kayitli degil, force iptal");
            return false;
        }
        ActivateNew(template, marketState);
        return true;
    }

    public string[] GetRegisteredIds()
    {
        string[] ids = new string[registered.Count];
        for (int i = 0; i < registered.Count; i++) ids[i] = registered[i].Id;
        return ids;
    }
}
