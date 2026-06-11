using UnityEngine;

// C7 Bearish Engulfing — 2 mum, uptrend sonu.
// Mum1: small yesil (%0.5 body).
// Mum2: large kirmizi (%1.0 body), body Mum1'in body'sini tamamen ortuyor.

public class BearishEngulfing : ChartPattern
{
    public override string Id => "C7_BearishEngulfing";
    public override float Weight => 5f;
    public override int TotalCandles => 2;

    int candleIndex;
    CandleOHLC lastOHLC;

    public override void Init(MarketState ms, float p0, float vm)
    {
        base.Init(ms, p0, vm);
        candleIndex = 0;
    }

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend
            || context == MarketContext.NewUpTrend;
    }

    public override CandleOHLC GenerateNextCandle(float prevClose)
    {
        if (candleIndex == 0)
        {
            float open = prevClose;
            float bodyPct = Random.Range(0.40f, 0.60f) / 100f;
            float close = open + open * bodyPct;
            float upperWickPct = Random.Range(0.05f, 0.15f) / 100f;
            float lowerWickPct = Random.Range(0.05f, 0.15f) / 100f;
            float high = Mathf.Max(open, close) + open * upperWickPct;
            float low = Mathf.Min(open, close) - open * lowerWickPct;
            low = Mathf.Max(low, 1f);
            lastOHLC = new CandleOHLC(open, high, low, close);
            return lastOHLC;
        }

        // Mum2: large kirmizi
        float mum1Open = lastOHLC.open;
        float mum1Close = lastOHLC.close;

        float mum2Open = mum1Close + prevClose * 0.001f;  // hafif gap up
        float bodyPct2 = Random.Range(0.80f, 1.20f) / 100f;
        float mum2Close = mum2Open - mum2Open * bodyPct2;

        // Engulf garanti: close <= mum1.open
        if (mum2Close > mum1Open)
            mum2Close = mum1Open - mum2Open * 0.005f;
        mum2Close = Mathf.Max(mum2Close, 1f);

        float upper2 = Random.Range(0.05f, 0.15f) / 100f;
        float lower2 = Random.Range(0.05f, 0.15f) / 100f;
        float high2 = Mathf.Max(mum2Open, mum2Close) + mum2Open * upper2;
        float low2 = Mathf.Min(mum2Open, mum2Close) - mum2Open * lower2;
        low2 = Mathf.Max(low2, 1f);

        return new CandleOHLC(mum2Open, high2, low2, mum2Close);
    }

    public override void OnCandleClosed()
    {
        candleIndex++;
    }

    public override bool IsDone() => candleIndex >= 2;
}
