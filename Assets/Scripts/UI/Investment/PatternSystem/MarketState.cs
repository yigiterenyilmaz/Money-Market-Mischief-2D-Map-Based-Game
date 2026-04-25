using System.Collections.Generic;
using UnityEngine;

// Pattern scheduler ve pattern'lar bu state'e bakar.
// Mum kapanislari ile guncellenir; pattern context'i (UpTrend, DownTrend, Sideways, vs) buradan turetilir.

public enum MarketContext
{
    UpTrend,
    DownTrend,
    NewUpTrend,
    NewDownTrend,
    Sideways,
    HighVolatility
}

public class MarketState
{
    public float startPrice;
    public float currentPrice;
    public float momentum;     // -1..+1, son ContextWindow muma gore yon orani + net degisim
    public float recentHigh;
    public float recentLow;

    public List<CandleOHLC> candleHistory = new List<CandleOHLC>();

    const int HistoryLimit = 50;
    const int ContextWindow = 15;

    public MarketState(float startPrice)
    {
        this.startPrice = startPrice;
        this.currentPrice = startPrice;
        this.recentHigh = startPrice;
        this.recentLow = startPrice;
        this.momentum = 0f;
    }

    public void OnCandleClosed(CandleOHLC ohlc)
    {
        candleHistory.Add(ohlc);
        while (candleHistory.Count > HistoryLimit)
            candleHistory.RemoveAt(0);

        currentPrice = ohlc.close;
        Recompute();
    }

    void Recompute()
    {
        int n = candleHistory.Count;
        if (n == 0)
        {
            recentHigh = currentPrice;
            recentLow = currentPrice;
            momentum = 0f;
            return;
        }

        int start = Mathf.Max(0, n - ContextWindow);

        recentHigh = float.NegativeInfinity;
        recentLow = float.PositiveInfinity;
        int greens = 0, totalInWindow = 0;
        for (int i = start; i < n; i++)
        {
            CandleOHLC c = candleHistory[i];
            if (c.high > recentHigh) recentHigh = c.high;
            if (c.low < recentLow) recentLow = c.low;
            if (c.IsGreen) greens++;
            totalInWindow++;
        }

        if (totalInWindow == 0)
        {
            recentHigh = currentPrice;
            recentLow = currentPrice;
        }

        // Yon orani: yesil/toplam → -1..+1
        float ratio = totalInWindow > 0 ? ((float)greens / totalInWindow) * 2f - 1f : 0f;

        // Net degisim: window basi vs son kapanis (% baslangic uzerinden)
        float netChangePct = 0f;
        if (totalInWindow >= 2)
        {
            float firstClose = candleHistory[start].close;
            float lastClose = candleHistory[n - 1].close;
            if (firstClose > 0f) netChangePct = (lastClose - firstClose) / firstClose;
        }

        momentum = Mathf.Clamp((ratio + netChangePct * 5f) * 0.5f, -1f, 1f);
    }

    public MarketContext GetContext()
    {
        if (candleHistory.Count < 5) return MarketContext.Sideways;

        // Volatilite kontrolu: son window'un range / ortalama
        float meanPrice = (recentHigh + recentLow) * 0.5f;
        float range = recentHigh - recentLow;
        float volatility = meanPrice > 0f ? range / meanPrice : 0f;
        if (volatility > 0.04f) return MarketContext.HighVolatility;

        if (momentum > 0.5f)
        {
            int trendLen = CountConsecutiveTrend(true);
            return trendLen > 10 ? MarketContext.UpTrend : MarketContext.NewUpTrend;
        }
        if (momentum < -0.5f)
        {
            int trendLen = CountConsecutiveTrend(false);
            return trendLen > 10 ? MarketContext.DownTrend : MarketContext.NewDownTrend;
        }
        return MarketContext.Sideways;
    }

    int CountConsecutiveTrend(bool isUp)
    {
        int count = 0;
        for (int i = candleHistory.Count - 1; i >= 0; i--)
        {
            if (candleHistory[i].IsGreen == isUp) count++;
            else break;
        }
        return count;
    }
}
