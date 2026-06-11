using System.Collections.Generic;
using UnityEngine;

// A4 Double Bottom (W formasyonu) — bullish reversal, 18-32 mum.
// A3'un tersi: 2 dip (P0-5%) + ara zirve (P0-1%) + breakout (P0+3%).
// Failed (~%10): ikinci dipte -3% daha (P0-8%).

public class DoubleBottom : PhasedPattern
{
    public override string Id => "A4_DoubleBottom";
    public override float Weight => 2f;
    protected override float FailureChance => 0.10f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.DownTrend;
    }

    protected override void BuildPhases()
    {
        // Ikinci dipte hammer (uzun alt fitil)
        CandleProfile secondBottomReject = CandleProfiles.Get(CandleCharacter.LongLowerWick);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "FirstBottomDown",
                targetOffsetPercent = -5f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "FirstBottomUp",
                targetOffsetPercent = -1f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "SecondBottomDown",
                targetOffsetPercent = -5f + Random.Range(-0.3f, 0.3f),
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium,
                hasClosingOverride = true,
                closingOverride = secondBottomReject
            },
            new PhaseDefinition
            {
                name = "SecondBottomUp",
                targetOffsetPercent = -1f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            }
        };

        if (isFailedRun)
        {
            phases.Add(new PhaseDefinition
            {
                name = "FailedSelloff",
                targetOffsetPercent = -8f,
                durationRange = new Vector2Int(6, 12),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            });
        }
        else
        {
            phases.Add(new PhaseDefinition
            {
                name = "Breakout",
                targetOffsetPercent = 3f,
                durationRange = new Vector2Int(6, 12),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            });
        }
    }
}
