using UnityEngine;

// C9 Evening Star — bearish reversal, 3 mum. C8'in tersi.
// Mum1: yesil %1.20. Mum2: doji/kucuk, Mum1'in ustunde kucuk gap. Mum3: kirmizi %1.20.

public class EveningStar : ChartPattern
{
    public override string Id => "C9_EveningStar";
    public override float Weight => 5f;
    public override int TotalCandles => 3;

    int candleIndex;
    CandleOHLC mum1;
    CandleOHLC mum2;

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
            // Mum1: large yesil
            float open = prevClose;
            float bodyPct = Random.Range(1.00f, 1.40f) / 100f;
            float close = open + open * bodyPct;
            float wickPct = Random.Range(0.10f, 0.20f) / 100f;
            float high = close + open * wickPct;
            float low = open - open * wickPct;
            low = Mathf.Max(low, 1f);
            mum1 = new CandleOHLC(open, high, low, close);
            return mum1;
        }

        if (candleIndex == 1)
        {
            // Mum2: doji/kucuk gap-up acilis
            float open = mum1.close + prevClose * 0.002f;
            float bodyPct = Random.Range(0.05f, 0.15f) / 100f;
            int sign = Random.value > 0.5f ? 1 : -1;
            float close = open + sign * open * bodyPct;

            float upperWick = Random.Range(0.10f, 0.25f) / 100f;
            float lowerWick = Random.Range(0.10f, 0.25f) / 100f;
            float high = Mathf.Max(open, close) + open * upperWick;
            float low = Mathf.Min(open, close) - open * lowerWick;
            low = Mathf.Max(low, 1f);
            mum2 = new CandleOHLC(open, high, low, close);
            return mum2;
        }

        // Mum3: large kirmizi, Mum1 body ortasina iner
        float mum3Open = mum2.close - prevClose * 0.002f;
        mum3Open = Mathf.Max(mum3Open, 1f);
        float mum1Mid = (mum1.open + mum1.close) * 0.5f;
        float bodyPct3 = Random.Range(1.00f, 1.40f) / 100f;
        float mum3Close = mum3Open - mum3Open * bodyPct3;

        if (mum3Close > mum1Mid)
            mum3Close = mum1Mid - mum3Open * 0.005f;
        mum3Close = Mathf.Max(mum3Close, 1f);

        float wick3 = Random.Range(0.10f, 0.20f) / 100f;
        float high3 = Mathf.Max(mum3Open, mum3Close) + mum3Open * wick3;
        float low3 = Mathf.Min(mum3Open, mum3Close) - mum3Open * wick3;
        low3 = Mathf.Max(low3, 1f);

        return new CandleOHLC(mum3Open, high3, low3, mum3Close);
    }

    public override void OnCandleClosed()
    {
        candleIndex++;
    }

    public override bool IsDone() => candleIndex >= 3;
}
