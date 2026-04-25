using System.Collections.Generic;
using UnityEngine;

// Phase listesi tanimlayan pattern'lar bundan turer (A1-A15, B1-B2, D1-D2 vs).
// Subclass BuildPhases() icinde phases listesini doldurur; geri kalan akis ortak.

public abstract class PhasedPattern : ChartPattern
{
    protected List<PhaseDefinition> phases;

    int currentPhaseIndex;
    int candleIndexInPhase;
    int currentPhaseDuration;
    float currentPhaseTargetPrice;

    int totalDuration;

    public override int TotalCandles => totalDuration;

    public override void Init(MarketState marketState, float p0, float volatilityMultiplier)
    {
        base.Init(marketState, p0, volatilityMultiplier);
        BuildPhases();

        if (phases == null || phases.Count == 0)
        {
            Debug.LogError($"[Pattern] {Id} BuildPhases bos liste dondurdu");
            currentPhaseIndex = 0;
            totalDuration = 0;
            return;
        }

        // Toplam mum sayisi tahmini (orta nokta)
        totalDuration = 0;
        foreach (PhaseDefinition p in phases)
        {
            int avg = (p.durationRange.x + p.durationRange.y) / 2;
            totalDuration += Mathf.Max(1, avg);
        }

        currentPhaseIndex = 0;
        SetupCurrentPhase();
    }

    protected abstract void BuildPhases();

    void SetupCurrentPhase()
    {
        PhaseDefinition phase = phases[currentPhaseIndex];
        int min = Mathf.Max(1, phase.durationRange.x);
        int max = Mathf.Max(min, phase.durationRange.y);
        currentPhaseDuration = Random.Range(min, max + 1);
        currentPhaseTargetPrice = TargetPrice(phase.targetOffsetPercent);
        candleIndexInPhase = 0;
    }

    public override CandleOHLC GenerateNextCandle(float prevClose)
    {
        PhaseDefinition phase = phases[currentPhaseIndex];
        int remaining = currentPhaseDuration - candleIndexInPhase;
        if (remaining < 1) remaining = 1;

        // Hedefe lineer adim + organik gurultu
        float targetClose;
        if (remaining <= 1)
        {
            targetClose = currentPhaseTargetPrice;
        }
        else
        {
            float step = (currentPhaseTargetPrice - prevClose) / remaining;
            float noise = (Random.value - 0.5f) * Mathf.Abs(step) * 0.7f;
            targetClose = prevClose + step + noise;
        }

        // Karakter sec: ilk mum opening override, son mum closing override, digerleri default
        bool isFirstInPhase = candleIndexInPhase == 0;
        bool isLastInPhase = remaining == 1;

        CandleProfile profile;
        if (isFirstInPhase && phase.hasOpeningOverride)
            profile = phase.openingOverride;
        else if (isLastInPhase && phase.hasClosingOverride)
            profile = phase.closingOverride;
        else
            profile = CandleProfiles.Get(phase.character);

        return BuildCandleFromProfile(prevClose, targetClose, profile);
    }

    // Profile'in istedigi gövde/fitil yuzdelerini target close ile uzlastirip OHLC olusturur.
    CandleOHLC BuildCandleFromProfile(float prevClose, float targetClose, CandleProfile profile)
    {
        float open = prevClose;
        float close = targetClose;

        // Renk biasi: target ters yondeyse profile yonune cek
        float profileBodyPct = Random.Range(profile.bodyPercentRange.x, profile.bodyPercentRange.y);
        if (profile.colorBias == ColorBias.Green && close <= open)
        {
            close = open + open * profileBodyPct / 100f;
        }
        else if (profile.colorBias == ColorBias.Red && close >= open)
        {
            close = open - open * profileBodyPct / 100f;
        }

        // Gövde profile'a gore biraz olcekle (target dogal gövde profile'in cok altindaysa buyut)
        float naturalBodyPct = open > 0f ? Mathf.Abs(close - open) / open * 100f : 0f;
        if (naturalBodyPct < profileBodyPct * 0.3f && profileBodyPct > 0.01f)
        {
            int sign;
            if (close > open) sign = 1;
            else if (close < open) sign = -1;
            else sign = Random.value > 0.5f ? 1 : -1;
            close = open + sign * open * profileBodyPct / 100f;
        }

        float upperWickPct = Random.Range(profile.upperWickPercentRange.x, profile.upperWickPercentRange.y);
        float lowerWickPct = Random.Range(profile.lowerWickPercentRange.x, profile.lowerWickPercentRange.y);

        float high = Mathf.Max(open, close) + open * upperWickPct / 100f;
        float low = Mathf.Min(open, close) - open * lowerWickPct / 100f;
        low = Mathf.Max(low, 1f);

        return new CandleOHLC(open, high, low, close);
    }

    public override void OnCandleClosed()
    {
        candleIndexInPhase++;
        if (candleIndexInPhase >= currentPhaseDuration)
        {
            currentPhaseIndex++;
            if (currentPhaseIndex < phases.Count)
                SetupCurrentPhase();
        }
    }

    public override bool IsDone()
    {
        return phases == null || currentPhaseIndex >= phases.Count;
    }
}
