using System.Collections.Generic;
using UnityEngine;

// Bir mumun nihai OHLC'si verildiginde, mum suresi boyunca currentPrice'in
// gezecegi yolu (waypoint listesi) uretir ve smooth interpolation ile sample eder.
//
// Mum karakterine gore yol farkli olusur:
//  - Marubozu: monoton open->close
//  - Hammer (long lower wick): open -> low (~0.4) -> close
//  - Shooting star (long upper wick): open -> high (~0.4) -> close
//  - Doji: high-low arasi salinim, son nokta close
//  - Yesil generic: open -> low (erken) -> high (orta-gec) -> close
//  - Kirmizi generic: open -> high (erken) -> low (orta-gec) -> close

public class CandlePathPlayer
{
    struct Waypoint
    {
        public float t;     // 0..1 normalize edilmis zaman
        public float price;
    }

    readonly List<Waypoint> waypoints = new List<Waypoint>();
    float duration;
    bool isLoaded;

    public void LoadCandle(CandleOHLC ohlc, float durationSec)
    {
        duration = Mathf.Max(0.01f, durationSec);
        waypoints.Clear();
        isLoaded = true;

        float open = ohlc.open;
        float close = ohlc.close;
        float high = ohlc.high;
        float low = ohlc.low;

        bool isGreen = close >= open;
        float bodySize = Mathf.Abs(close - open);
        float upperWickSize = high - Mathf.Max(open, close);
        float lowerWickSize = Mathf.Min(open, close) - low;

        // Karakter teshisi (OHLC'den geri okuma)
        float epsilon = open * 0.0001f;
        bool isDoji = bodySize <= open * 0.001f;
        bool isMarubozu = !isDoji
                          && upperWickSize <= bodySize * 0.1f
                          && lowerWickSize <= bodySize * 0.1f;
        bool isLongLowerWick = !isDoji && lowerWickSize > bodySize * 1.5f && lowerWickSize > upperWickSize * 2f;
        bool isLongUpperWick = !isDoji && upperWickSize > bodySize * 1.5f && upperWickSize > lowerWickSize * 2f;

        waypoints.Add(new Waypoint { t = 0f, price = open });

        if (isDoji)
        {
            // Salinim: high -> low -> high pattern, son close
            waypoints.Add(new Waypoint { t = 0.25f, price = high });
            waypoints.Add(new Waypoint { t = 0.5f, price = low });
            waypoints.Add(new Waypoint { t = 0.75f, price = high });
            waypoints.Add(new Waypoint { t = 1f, price = close });
        }
        else if (isMarubozu)
        {
            // Monoton, ic noktalar arasinda hafif noise smoothstep ile gelir
            waypoints.Add(new Waypoint { t = 1f, price = close });
        }
        else if (isLongLowerWick)
        {
            float dipT = Random.Range(0.30f, 0.50f);
            waypoints.Add(new Waypoint { t = dipT, price = low });
            // Eger gozle gorulur ust fitil varsa
            if (upperWickSize > epsilon)
            {
                float spikeT = Mathf.Min(0.92f, dipT + Random.Range(0.15f, 0.30f));
                waypoints.Add(new Waypoint { t = spikeT, price = high });
            }
            waypoints.Add(new Waypoint { t = 1f, price = close });
        }
        else if (isLongUpperWick)
        {
            float peakT = Random.Range(0.30f, 0.50f);
            waypoints.Add(new Waypoint { t = peakT, price = high });
            if (lowerWickSize > epsilon)
            {
                float dipT = Mathf.Min(0.92f, peakT + Random.Range(0.15f, 0.30f));
                waypoints.Add(new Waypoint { t = dipT, price = low });
            }
            waypoints.Add(new Waypoint { t = 1f, price = close });
        }
        else
        {
            // Generic mum: yon agirlikli sira
            if (isGreen)
            {
                // Onceyi alt fitil, sonra ust fitil, sonra kapanis
                if (lowerWickSize > epsilon)
                    waypoints.Add(new Waypoint { t = Random.Range(0.15f, 0.35f), price = low });
                if (upperWickSize > epsilon)
                {
                    float lastT = waypoints[waypoints.Count - 1].t;
                    float minNext = Mathf.Min(0.85f, lastT + 0.15f);
                    float maxNext = Mathf.Min(0.90f, lastT + 0.45f);
                    if (maxNext > minNext)
                        waypoints.Add(new Waypoint { t = Random.Range(minNext, maxNext), price = high });
                }
            }
            else
            {
                // Once ust fitil, sonra alt fitil, sonra kapanis
                if (upperWickSize > epsilon)
                    waypoints.Add(new Waypoint { t = Random.Range(0.15f, 0.35f), price = high });
                if (lowerWickSize > epsilon)
                {
                    float lastT = waypoints[waypoints.Count - 1].t;
                    float minNext = Mathf.Min(0.85f, lastT + 0.15f);
                    float maxNext = Mathf.Min(0.90f, lastT + 0.45f);
                    if (maxNext > minNext)
                        waypoints.Add(new Waypoint { t = Random.Range(minNext, maxNext), price = low });
                }
            }
            waypoints.Add(new Waypoint { t = 1f, price = close });
        }
    }

    public float GetPriceAt(float elapsedSec)
    {
        if (!isLoaded || waypoints.Count == 0) return 0f;

        float t = Mathf.Clamp01(elapsedSec / duration);

        // Segment bul ve smooth interpole et
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (t >= waypoints[i].t && t <= waypoints[i + 1].t)
            {
                float segLen = waypoints[i + 1].t - waypoints[i].t;
                float segT = segLen > 0.0001f ? (t - waypoints[i].t) / segLen : 0f;
                float smoothT = Mathf.SmoothStep(0f, 1f, segT);
                return Mathf.Lerp(waypoints[i].price, waypoints[i + 1].price, smoothT);
            }
        }

        return waypoints[waypoints.Count - 1].price;
    }
}
