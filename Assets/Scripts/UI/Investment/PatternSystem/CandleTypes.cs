using UnityEngine;

// Pattern sisteminin ortak veri tipleri.
// CandleOHLC: bir mumun nihai 4 degeri.
// CandleCharacter: prompt §2 tablosundaki etiketler (Mikro, Kucuk, Orta, Buyuk, Marubozu, vs).
// CandleProfile: bir karakter icin gövde/fitil yuzde araliklari.
// PhaseDefinition: PhasedPattern'larin phase tanimi.

public struct CandleOHLC
{
    public float open;
    public float high;
    public float low;
    public float close;

    public CandleOHLC(float open, float high, float low, float close)
    {
        this.open = open;
        this.high = high;
        this.low = low;
        this.close = close;
    }

    public bool IsGreen => close >= open;
}

public enum CandleCharacter
{
    Micro,           // 0.05-0.15% body, 0-0.10% wick
    Small,           // 0.15-0.40% body, 0.05-0.20% wick
    Medium,          // 0.40-0.80% body, 0.10-0.30% wick
    Large,           // 0.80-1.50% body, 0.10-0.40% wick
    Marubozu,        // 1.50-3.00% body, 0-0.10% wick
    LongLowerWick,   // hammer (body 0.10-0.30%, lower wick 0.60-1.20%)
    LongUpperWick,   // shooting star (body 0.10-0.30%, upper wick 0.60-1.20%)
    Doji             // body <= 0.05%, wick 0.30-0.60% her iki tarafta
}

public enum ColorBias
{
    None,
    Green,
    Red
}

public struct CandleProfile
{
    public Vector2 bodyPercentRange;
    public Vector2 upperWickPercentRange;
    public Vector2 lowerWickPercentRange;
    public ColorBias colorBias;
}

public struct PhaseDefinition
{
    public string name;
    public float targetOffsetPercent;          // P0'a gore mutlak hedef (% — volatilityMultiplier ile carpilir)
    public Vector2Int durationRange;           // mum sayisi araligi (inclusive)
    public CandleCharacter character;          // genel mum karakteri
    public bool hasOpeningOverride;
    public CandleProfile openingOverride;      // ilk mum farkliysa
    public bool hasClosingOverride;
    public CandleProfile closingOverride;      // son mum farkliysa
}

// Karakter -> profile lookup.
public static class CandleProfiles
{
    public static CandleProfile Get(CandleCharacter character, ColorBias colorBias = ColorBias.None)
    {
        switch (character)
        {
            case CandleCharacter.Micro:
                return new CandleProfile
                {
                    bodyPercentRange = new Vector2(0.05f, 0.15f),
                    upperWickPercentRange = new Vector2(0f, 0.10f),
                    lowerWickPercentRange = new Vector2(0f, 0.10f),
                    colorBias = colorBias
                };
            case CandleCharacter.Small:
                return new CandleProfile
                {
                    bodyPercentRange = new Vector2(0.15f, 0.40f),
                    upperWickPercentRange = new Vector2(0.05f, 0.20f),
                    lowerWickPercentRange = new Vector2(0.05f, 0.20f),
                    colorBias = colorBias
                };
            case CandleCharacter.Medium:
                return new CandleProfile
                {
                    bodyPercentRange = new Vector2(0.40f, 0.80f),
                    upperWickPercentRange = new Vector2(0.10f, 0.30f),
                    lowerWickPercentRange = new Vector2(0.10f, 0.30f),
                    colorBias = colorBias
                };
            case CandleCharacter.Large:
                return new CandleProfile
                {
                    bodyPercentRange = new Vector2(0.80f, 1.50f),
                    upperWickPercentRange = new Vector2(0.10f, 0.40f),
                    lowerWickPercentRange = new Vector2(0.10f, 0.40f),
                    colorBias = colorBias
                };
            case CandleCharacter.Marubozu:
                return new CandleProfile
                {
                    bodyPercentRange = new Vector2(1.50f, 3.00f),
                    upperWickPercentRange = new Vector2(0f, 0.10f),
                    lowerWickPercentRange = new Vector2(0f, 0.10f),
                    colorBias = colorBias
                };
            case CandleCharacter.LongLowerWick:
                return new CandleProfile
                {
                    bodyPercentRange = new Vector2(0.10f, 0.30f),
                    upperWickPercentRange = new Vector2(0f, 0.10f),
                    lowerWickPercentRange = new Vector2(0.60f, 1.20f),
                    colorBias = colorBias
                };
            case CandleCharacter.LongUpperWick:
                return new CandleProfile
                {
                    bodyPercentRange = new Vector2(0.10f, 0.30f),
                    upperWickPercentRange = new Vector2(0.60f, 1.20f),
                    lowerWickPercentRange = new Vector2(0f, 0.10f),
                    colorBias = colorBias
                };
            case CandleCharacter.Doji:
                return new CandleProfile
                {
                    bodyPercentRange = new Vector2(0f, 0.05f),
                    upperWickPercentRange = new Vector2(0.30f, 0.60f),
                    lowerWickPercentRange = new Vector2(0.30f, 0.60f),
                    colorBias = ColorBias.None
                };
        }
        return new CandleProfile
        {
            bodyPercentRange = new Vector2(0.20f, 0.50f),
            upperWickPercentRange = new Vector2(0.10f, 0.25f),
            lowerWickPercentRange = new Vector2(0.10f, 0.25f),
            colorBias = colorBias
        };
    }
}
