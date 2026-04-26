using UnityEngine;

// C5 Doji — kararsizlik mum'u + (kosullu) reversal confirmation.
//
// Gercek trading davranisi:
//  - Doji her context'te ortaya cikabilir (filler noise olarak veya trend ucunda anlamli sinyal)
//  - UpTrend/DownTrend'in tepesi/dibinde doji gorulurse %65 ihtimalle ters yonde
//    confirmation mum'u gelir (klasik "reversal doji" + onay seti)
//  - Diger context'lerde (Sideways, HighVolatility, NewUpTrend, NewDownTrend) sadece tek mum

public class DojiCandle : ChartPattern
{
    public override string Id => "C5_Doji";
    public override float Weight => 5f;
    public override int TotalCandles => totalCandles;

    int totalCandles;
    int candleIndex;
    bool reversalMode;
    bool wasUpTrendAtStart;  // confirmation yonu icin

    const float ReversalChanceAtExtreme = 0.65f;

    public override void Init(MarketState marketState, float p0, float volatilityMultiplier)
    {
        base.Init(marketState, p0, volatilityMultiplier);
        candleIndex = 0;

        MarketContext ctx = marketState.GetContext();
        bool atTrendExtreme = ctx == MarketContext.UpTrend || ctx == MarketContext.DownTrend;
        reversalMode = atTrendExtreme && Random.value < ReversalChanceAtExtreme;
        wasUpTrendAtStart = ctx == MarketContext.UpTrend;
        totalCandles = reversalMode ? 2 : 1;
    }

    public override bool MatchesContext(MarketContext context) => true;

    public override CandleOHLC GenerateNextCandle(float prevClose)
    {
        if (candleIndex == 0)
            return BuildDoji(prevClose);
        return BuildConfirmation(prevClose);
    }

    CandleOHLC BuildDoji(float prevClose)
    {
        float open = prevClose;

        // Body cok kucuk: open ile close neredeyse esit
        float bodyPct = Random.Range(0f, 0.05f) / 100f;
        int sign = Random.value > 0.5f ? 1 : -1;
        float close = open + sign * open * bodyPct;

        float upperWickPct = Random.Range(0.30f, 0.60f) / 100f;
        float lowerWickPct = Random.Range(0.30f, 0.60f) / 100f;

        float high = Mathf.Max(open, close) + open * upperWickPct;
        float low = Mathf.Min(open, close) - open * lowerWickPct;
        low = Mathf.Max(low, 1f);

        return new CandleOHLC(open, high, low, close);
    }

    CandleOHLC BuildConfirmation(float prevClose)
    {
        // Trend tersine medium govde:
        //  - UpTrend tepesinde doji vardi -> bearish confirmation (kirmizi)
        //  - DownTrend dibinde doji vardi -> bullish confirmation (yesil)
        float open = prevClose;
        float bodyPct = Random.Range(0.40f, 0.80f) / 100f * volatilityMultiplier;
        int sign = wasUpTrendAtStart ? -1 : 1;
        float close = open + sign * open * bodyPct;

        float upperWickPct = Random.Range(0.10f, 0.25f) / 100f;
        float lowerWickPct = Random.Range(0.10f, 0.25f) / 100f;

        float high = Mathf.Max(open, close) + open * upperWickPct;
        float low = Mathf.Min(open, close) - open * lowerWickPct;
        low = Mathf.Max(low, 1f);

        return new CandleOHLC(open, high, low, close);
    }

    public override void OnCandleClosed()
    {
        candleIndex++;
    }

    public override bool IsDone() => candleIndex >= totalCandles;
}
