using UnityEngine;

// C4 Hanging Man — bearish reversal (uptrend zirvesinde).
// Hammer sekli (lower wick uzun) ama kirmizi tercih + uptrend tepesinde.

public class HangingMan : SingleCandlePattern
{
    public override string Id => "C4_HangingMan";
    public override float Weight => 5f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend
            || context == MarketContext.NewUpTrend;
    }

    public override CandleOHLC GenerateNextCandle(float prevClose)
    {
        float open = prevClose;
        float bodyPct = Random.Range(0.10f, 0.30f) / 100f;
        // Kirmizi tercih
        float close = open - open * bodyPct;

        float upperWickPct = Random.Range(0f, 0.10f) / 100f;
        float lowerWickPct = Random.Range(0.60f, 1.20f) / 100f;

        float high = Mathf.Max(open, close) + open * upperWickPct;
        float low = Mathf.Min(open, close) - open * lowerWickPct;
        low = Mathf.Max(low, 1f);

        return new CandleOHLC(open, high, low, close);
    }
}
