using UnityEngine;

// Pattern aktif degilken kullanilan idle driver.
// CandlestickChart'tan extract edilen Ornstein-Uhlenbeck random walk.
// Her mum baslangicinda ic-tick'lerle mumun nihai OHLC'sini sample eder.

public class NoiseDriver
{
    readonly float volatility;
    readonly float trendNoise;
    readonly float trendDecay;
    readonly float maxTrend;
    readonly float startPrice;

    float currentTrend;

    public NoiseDriver(float volatility, float trendNoise, float trendDecay, float maxTrend, float startPrice)
    {
        this.volatility = volatility;
        this.trendNoise = trendNoise;
        this.trendDecay = trendDecay;
        this.maxTrend = maxTrend;
        this.startPrice = startPrice;
        this.currentTrend = 0f;
    }

    // Bir mum suresince OU yuruyusunu simule et, OHLC'yi turet.
    public CandleOHLC GenerateNextCandle(float prevClose, float candleDurationSec)
    {
        float open = prevClose;
        float price = prevClose;
        float high = open;
        float low = open;

        // ~30 ic-tick (mevcut kodun 60fps'de her 2 frame'de UpdatePrice cagirmasina yakin)
        const int subTicks = 30;
        float dt = candleDurationSec / subTicks;

        for (int i = 0; i < subTicks; i++)
        {
            // Trend evrim: rastgele itme + sifira donus
            currentTrend += Random.Range(-trendNoise, trendNoise) * dt;
            currentTrend -= currentTrend * trendDecay * dt;
            currentTrend = Mathf.Clamp(currentTrend, -maxTrend, maxTrend);

            // Tick degisimi
            float change = Random.Range(-volatility, volatility) * dt;
            change += currentTrend * dt;

            // Ani spike
            if (Random.value < 0.003f)
                change += Random.Range(-volatility * 4f, volatility * 4f) * dt * 10f;

            // Ortalamaya donus
            float deviation = (price - startPrice) / startPrice;
            change -= deviation * 0.5f * dt;

            price += change;
            price = Mathf.Max(price, 1f);

            if (price > high) high = price;
            if (price < low) low = price;
        }

        return new CandleOHLC(open, high, low, price);
    }
}
