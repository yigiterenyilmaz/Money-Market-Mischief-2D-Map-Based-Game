using UnityEngine;

// Tum trading pattern'larin abstract base'i.
// Scheduler'a kaydedilen instance'lar template gorevi gorur (Init cagrilmaz).
// Spawn edilince Activator.CreateInstance ile yeni instance uretilir, Init cagrilir.

public abstract class ChartPattern
{
    public abstract string Id { get; }
    public abstract float Weight { get; }
    public abstract int TotalCandles { get; }

    protected MarketState marketState;
    protected float p0;
    protected float volatilityMultiplier = 1f;

    public virtual void Init(MarketState marketState, float p0, float volatilityMultiplier)
    {
        this.marketState = marketState;
        this.p0 = p0;
        this.volatilityMultiplier = volatilityMultiplier;
    }

    public abstract bool MatchesContext(MarketContext context);

    // Mum baslangicinda cagrilir, mumun nihai OHLC'sini doner.
    public abstract CandleOHLC GenerateNextCandle(float prevClose);

    // Mum kapanisinda cagrilir; pattern phase index'ini ilerletmek icin.
    public abstract void OnCandleClosed();

    public abstract bool IsDone();

    // P0 + offset% absolute fiyata cevir; volatilityMultiplier ile olcekle.
    protected float TargetPrice(float offsetPercent)
    {
        return p0 * (1f + (offsetPercent * volatilityMultiplier) / 100f);
    }
}
