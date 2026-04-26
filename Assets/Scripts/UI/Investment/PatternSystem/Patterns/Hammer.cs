using UnityEngine;

// C1 Hammer — bullish reversal sinyali (downtrend sonu).
// Body kucuk yesil tercih, lower wick uzun (body'nin ~5 kati), upper wick mikro.

public class Hammer : SingleCandlePattern
{
    public override string Id => "C1_Hammer";
    public override float Weight => 5f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.DownTrend
            || context == MarketContext.NewDownTrend;
    }

    public override CandleOHLC GenerateNextCandle(float prevClose)
    {
        float open = prevClose;
        float bodyPct = Random.Range(0.10f, 0.30f) / 100f;
        // Hammer rengi onemsiz ama yesil tercih (recovery)
        float close = open + open * bodyPct;

        float upperWickPct = Random.Range(0f, 0.10f) / 100f;
        float lowerWickPct = Random.Range(0.60f, 1.20f) / 100f;

        float high = Mathf.Max(open, close) + open * upperWickPct;
        float low = Mathf.Min(open, close) - open * lowerWickPct;
        low = Mathf.Max(low, 1f);

        return new CandleOHLC(open, high, low, close);
    }
}
