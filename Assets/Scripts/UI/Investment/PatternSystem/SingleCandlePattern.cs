// Tek mumluk patterns icin ortak base.
// Subclass sadece GenerateNextCandle + Id + Weight + MatchesContext implement eder.

public abstract class SingleCandlePattern : ChartPattern
{
    public override int TotalCandles => 1;
    bool finished;

    public override void Init(MarketState marketState, float p0, float volatilityMultiplier)
    {
        base.Init(marketState, p0, volatilityMultiplier);
        finished = false;
    }

    public override void OnCandleClosed()
    {
        finished = true;
    }

    public override bool IsDone() => finished;
}
