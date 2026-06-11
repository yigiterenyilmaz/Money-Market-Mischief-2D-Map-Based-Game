using UnityEngine;

// C11 Three Black Crows — bearish momentum, 3 ardisik kirmizi mum. C10'un tersi.

public class ThreeBlackCrows : ChartPattern
{
    public override string Id => "C11_ThreeBlackCrows";
    public override float Weight => 5f;
    public override int TotalCandles => 3;

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
        float open;
        if (candleIndex == 0)
        {
            open = prevClose;
        }
        else
        {
            open = (lastOHLC.open + lastOHLC.close) * 0.5f;
        }

        float bodyPct = Random.Range(0.70f, 0.90f) / 100f;
        float close = open - open * bodyPct;

        // Garantile: close < onceki close
        if (candleIndex > 0 && close >= lastOHLC.close)
            close = lastOHLC.close - open * 0.005f;
        close = Mathf.Max(close, 1f);

        float upperWick = Random.Range(0.10f, 0.20f) / 100f;
        float lowerWick = Random.Range(0.05f, 0.10f) / 100f;
        float high = open + open * upperWick;
        float low = close - open * lowerWick;
        low = Mathf.Max(low, 1f);

        CandleOHLC ohlc = new CandleOHLC(open, high, low, close);
        lastOHLC = ohlc;
        return ohlc;
    }

    public override void OnCandleClosed()
    {
        candleIndex++;
    }

    public override bool IsDone() => candleIndex >= 3;
}
