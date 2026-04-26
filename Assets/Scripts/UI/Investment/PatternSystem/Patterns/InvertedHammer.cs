using UnityEngine;

// C2 Inverted Hammer — bullish reversal (downtrend sonu).
// Body kucuk yesil, upper wick uzun, lower wick mikro.

public class InvertedHammer : SingleCandlePattern
{
    public override string Id => "C2_InvertedHammer";
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
        float close = open + open * bodyPct;

        float upperWickPct = Random.Range(0.60f, 1.20f) / 100f;
        float lowerWickPct = Random.Range(0f, 0.10f) / 100f;

        float high = Mathf.Max(open, close) + open * upperWickPct;
        float low = Mathf.Min(open, close) - open * lowerWickPct;
        low = Mathf.Max(low, 1f);

        return new CandleOHLC(open, high, low, close);
    }
}
