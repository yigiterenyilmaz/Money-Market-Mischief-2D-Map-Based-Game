using System.Collections.Generic;

/// <summary>Bir formasyonun beklenen yönü.</summary>
public enum PatternBias
{
    Bullish, //yukarı
    Bearish, //aşağı
    Neutral  //yön formasyon içinde belirlenir
}

/// <summary>Formasyonun beklenen hareket büyüklüğü — insider ipucunda "ne kadar" kısmı.</summary>
public enum PatternImpact
{
    Small,  //%1'in altı, tek-üç mumluk sinyaller
    Medium, //%3-6 bandı
    Large   //%8+ dramatik hareket
}

public struct PatternIntel
{
    public string displayName; //oyuncuya gösterilen Türkçe ad
    public PatternBias bias;
    public PatternImpact impact;

    public PatternIntel(string displayName, PatternBias bias, PatternImpact impact)
    {
        this.displayName = displayName;
        this.bias = bias;
        this.impact = impact;
    }
}

/// <summary>
/// Formasyon Id'si -> oyuncuya gösterilecek istihbarat (ad, yön, büyüklük).
///
/// NEDEN AYRI TABLO: yön/büyüklük bilgisi 38 ChartPattern sınıfının içine dağıtılabilirdi,
/// ama o zaman her yeni formasyon iki yerde birden düzenlenirdi ve chart sistemi (görsel)
/// insider mekaniğine (oyun) bağımlı hale gelirdi. Tablo tek yönlü bağımlılık kurar:
/// piyasa sistemi chart'ı tanır, chart piyasa sistemini tanımaz.
///
/// Değerler chart-pattern-readme.md'deki hedef yüzdelerden türetilmiştir.
/// </summary>
public static class MarketIntel
{
    private static readonly Dictionary<string, PatternIntel> table = new Dictionary<string, PatternIntel>
    {
        // A grubu — klasik formasyonlar
        { "A1_HeadAndShoulders",         new PatternIntel("Omuz-Baş-Omuz",        PatternBias.Bearish, PatternImpact.Large)  },
        { "A2_InverseHeadAndShoulders",  new PatternIntel("Ters Omuz-Baş-Omuz",   PatternBias.Bullish, PatternImpact.Large)  },
        { "A3_DoubleTop",                new PatternIntel("İkili Tepe",           PatternBias.Bearish, PatternImpact.Medium) },
        { "A4_DoubleBottom",             new PatternIntel("İkili Dip",            PatternBias.Bullish, PatternImpact.Medium) },
        { "A5_TripleTop",                new PatternIntel("Üçlü Tepe",            PatternBias.Bearish, PatternImpact.Medium) },
        { "A5_TripleBottom",             new PatternIntel("Üçlü Dip",             PatternBias.Bullish, PatternImpact.Medium) },
        { "A6_AscendingTriangle",        new PatternIntel("Yükselen Üçgen",       PatternBias.Bullish, PatternImpact.Large)  },
        { "A7_DescendingTriangle",       new PatternIntel("İnen Üçgen",           PatternBias.Bearish, PatternImpact.Large)  },
        { "A8_SymmetricalTriangle",      new PatternIntel("Simetrik Üçgen",       PatternBias.Neutral, PatternImpact.Medium) },
        { "A9_BullFlag",                 new PatternIntel("Boğa Bayrağı",         PatternBias.Bullish, PatternImpact.Large)  },
        { "A10_BearFlag",                new PatternIntel("Ayı Bayrağı",          PatternBias.Bearish, PatternImpact.Large)  },
        { "A11_Pennant",                 new PatternIntel("Flama",                PatternBias.Neutral, PatternImpact.Large)  },
        { "A12_RisingWedge",             new PatternIntel("Yükselen Kama",        PatternBias.Bearish, PatternImpact.Medium) },
        { "A13_FallingWedge",            new PatternIntel("Düşen Kama",           PatternBias.Bullish, PatternImpact.Medium) },
        { "A14_CupAndHandle",            new PatternIntel("Fincan ve Kulp",       PatternBias.Bullish, PatternImpact.Large)  },
        { "A15_Rectangle",               new PatternIntel("Yatay Kanal",          PatternBias.Neutral, PatternImpact.Medium) },

        // B grubu — Wyckoff
        { "B1_WyckoffAccumulation",      new PatternIntel("Wyckoff Akümülasyon",  PatternBias.Bullish, PatternImpact.Large)  },
        { "B2_WyckoffDistribution",      new PatternIntel("Wyckoff Distribüsyon", PatternBias.Bearish, PatternImpact.Large)  },

        // C grubu — mum formasyonları (hepsi küçük, 1-3 mumluk sinyaller)
        { "C1_Hammer",                   new PatternIntel("Çekiç",                PatternBias.Bullish, PatternImpact.Small)  },
        { "C2_InvertedHammer",           new PatternIntel("Ters Çekiç",           PatternBias.Bullish, PatternImpact.Small)  },
        { "C3_ShootingStar",             new PatternIntel("Kayan Yıldız",         PatternBias.Bearish, PatternImpact.Small)  },
        { "C4_HangingMan",               new PatternIntel("Asılan Adam",          PatternBias.Bearish, PatternImpact.Small)  },
        { "C5_Doji",                     new PatternIntel("Doji",                 PatternBias.Neutral, PatternImpact.Small)  },
        { "C6_BullishEngulfing",         new PatternIntel("Boğa Yutan",           PatternBias.Bullish, PatternImpact.Small)  },
        { "C7_BearishEngulfing",         new PatternIntel("Ayı Yutan",            PatternBias.Bearish, PatternImpact.Small)  },
        { "C8_MorningStar",              new PatternIntel("Sabah Yıldızı",        PatternBias.Bullish, PatternImpact.Small)  },
        { "C9_EveningStar",              new PatternIntel("Akşam Yıldızı",        PatternBias.Bearish, PatternImpact.Small)  },
        { "C10_ThreeWhiteSoldiers",      new PatternIntel("Üç Beyaz Asker",       PatternBias.Bullish, PatternImpact.Small)  },
        { "C11_ThreeBlackCrows",         new PatternIntel("Üç Siyah Karga",       PatternBias.Bearish, PatternImpact.Small)  },
        { "C12_Marubozu",                new PatternIntel("Marubozu",             PatternBias.Neutral, PatternImpact.Small)  },
        { "C13_BullishHarami",           new PatternIntel("Boğa Harami",          PatternBias.Bullish, PatternImpact.Small)  },
        { "C14_BearishHarami",           new PatternIntel("Ayı Harami",           PatternBias.Bearish, PatternImpact.Small)  },

        // D grubu — price action olayları
        { "D1_Pump",                     new PatternIntel("Pump (FOMO Rallisi)",  PatternBias.Bullish, PatternImpact.Large)  },
        { "D2_Dump",                     new PatternIntel("Dump (Çöküş)",         PatternBias.Bearish, PatternImpact.Large)  },
        { "D3_LiquidityGrab",            new PatternIntel("Likidite Avı",         PatternBias.Neutral, PatternImpact.Medium) },
        { "D4_Fakeout",                  new PatternIntel("Yalancı Kırılım",      PatternBias.Neutral, PatternImpact.Medium) },
        { "D5_Squeeze",                  new PatternIntel("Volatilite Sıkışması", PatternBias.Neutral, PatternImpact.Medium) },
        { "D6_ContinuationPullback",     new PatternIntel("Trend İçi Geri Çekilme", PatternBias.Neutral, PatternImpact.Medium) },
    };

    /// <summary>Formasyon tanınmıyorsa false döner; çağıran tarafın ipucu göstermemesi gerekir.</summary>
    public static bool TryGet(string patternId, out PatternIntel intel)
    {
        if (string.IsNullOrEmpty(patternId))
        {
            intel = default;
            return false;
        }

        return table.TryGetValue(patternId, out intel);
    }

    public static string BiasLabel(PatternBias bias)
    {
        switch (bias)
        {
            case PatternBias.Bullish: return "YUKARI";
            case PatternBias.Bearish: return "AŞAĞI";
            default: return "BELİRSİZ";
        }
    }

    public static string ImpactLabel(PatternImpact impact)
    {
        switch (impact)
        {
            case PatternImpact.Large: return "sert";
            case PatternImpact.Medium: return "orta";
            default: return "hafif";
        }
    }
}
