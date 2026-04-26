using UnityEngine;

// C12 Marubozu — guclu momentum mum'u, tek mum.
// Body 1.50-2.50%, fitiller neredeyse yok. Trend yonune gore renk.

public class MarubozuPattern : SingleCandlePattern
{
    public override string Id => "C12_Marubozu";
    public override float Weight => 5f;

    bool isBullish;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend
            || context == MarketContext.DownTrend
            || context == MarketContext.NewUpTrend
            || context == MarketContext.NewDownTrend;
    }

    public override void Init(MarketState ms, float p0, float vm)
    {
        MarketContext ctx = ms.GetContext();
        isBullish = ctx == MarketContext.UpTrend || ctx == MarketContext.NewUpTrend;
        base.Init(ms, p0, vm);
    }

    public override CandleOHLC GenerateNextCandle(float prevClose)
    {
        float open = prevClose;
        float bodyPct = Random.Range(1.50f, 2.50f) / 100f;
        float close = isBullish ? open + open * bodyPct : open - open * bodyPct;

        float upperWickPct = Random.Range(0f, 0.10f) / 100f;
        float lowerWickPct = Random.Range(0f, 0.10f) / 100f;

        float high = Mathf.Max(open, close) + open * upperWickPct;
        float low = Mathf.Min(open, close) - open * lowerWickPct;
        low = Mathf.Max(low, 1f);

        return new CandleOHLC(open, high, low, close);
    }
}
