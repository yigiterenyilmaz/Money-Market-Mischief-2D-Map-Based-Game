using UnityEngine;

// C8 Morning Star — bullish reversal, 3 mum.
// Mum1: kirmizi %1.20.
// Mum2: doji veya kucuk %0.10, Mum1'in altinda kucuk gap.
// Mum3: yesil %1.20, Mum1'in body ortasina kadar cikar.

public class MorningStar : ChartPattern
{
    public override string Id => "C8_MorningStar";
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
        return context == MarketContext.DownTrend
            || context == MarketContext.NewDownTrend;
    }

    public override CandleOHLC GenerateNextCandle(float prevClose)
    {
        if (candleIndex == 0)
        {
            // Mum1: large kirmizi
            float open = prevClose;
            float bodyPct = Random.Range(1.00f, 1.40f) / 100f;
            float close = open - open * bodyPct;
            float wickPct = Random.Range(0.10f, 0.20f) / 100f;
            float high = open + open * wickPct;
            float low = close - open * wickPct;
            low = Mathf.Max(low, 1f);
            mum1 = new CandleOHLC(open, high, low, close);
            return mum1;
        }

        if (candleIndex == 1)
        {
            // Mum2: doji/kucuk gap-down acilis
            float open = mum1.close - prevClose * 0.002f;  // hafif gap aşağı
            open = Mathf.Max(open, 1f);
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

        // Mum3: large yesil, Mum1 body ortasina cikar
        float mum3Open = mum2.close + prevClose * 0.002f;  // hafif gap yukari
        float mum1Mid = (mum1.open + mum1.close) * 0.5f;
        float bodyPct3 = Random.Range(1.00f, 1.40f) / 100f;
        float mum3Close = mum3Open + mum3Open * bodyPct3;

        // Garantile: en az Mum1 mid'e ulas
        if (mum3Close < mum1Mid)
            mum3Close = mum1Mid + mum3Open * 0.005f;

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
