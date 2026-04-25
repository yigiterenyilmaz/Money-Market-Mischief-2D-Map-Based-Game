using UnityEngine;

// C5 Doji — kararsizlik mum'u, tek mumluk mini-event.
// Body <= %0.05, her iki yanda %0.30-0.60 fitil.
// PhasedPattern degil cunku tek mum + ozel sekil; ChartPattern dogrudan.

public class DojiCandle : ChartPattern
{
    public override string Id => "C5_Doji";
    public override float Weight => 5f;
    public override int TotalCandles => 1;

    bool finished;

    public override void Init(MarketState marketState, float p0, float volatilityMultiplier)
    {
        base.Init(marketState, p0, volatilityMultiplier);
        finished = false;
    }

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.HighVolatility
            || context == MarketContext.Sideways;
    }

    public override CandleOHLC GenerateNextCandle(float prevClose)
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

    public override void OnCandleClosed()
    {
        finished = true;
    }

    public override bool IsDone() => finished;
}
