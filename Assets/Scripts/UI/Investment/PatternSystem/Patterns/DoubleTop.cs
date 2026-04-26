using System.Collections.Generic;
using UnityEngine;

// A3 Double Top (M formasyonu) — bearish reversal, 18-32 mum.
// 2 tepe (P0+5%) + ara vadi (P0+1%) + breakdown (P0-3%).
// Failed (~%10): ikinci tepede +3% rally daha (P0+8%).

public class DoubleTop : PhasedPattern
{
    public override string Id => "A3_DoubleTop";
    public override float Weight => 2f;
    protected override float FailureChance => 0.10f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend;
    }

    protected override void BuildPhases()
    {
        CandleProfile rejection = CandleProfiles.Get(CandleCharacter.Small);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "FirstTopUp",
                targetOffsetPercent = 5f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "FirstTopDown",
                targetOffsetPercent = 1f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "SecondTopUp",
                targetOffsetPercent = 5f + Random.Range(-0.3f, 0.3f),
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium,
                hasClosingOverride = true,
                closingOverride = rejection
            },
            new PhaseDefinition
            {
                name = "SecondTopDown",
                targetOffsetPercent = 1f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            }
        };

        if (isFailedRun)
        {
            phases.Add(new PhaseDefinition
            {
                name = "FailedRally",
                targetOffsetPercent = 8f,
                durationRange = new Vector2Int(6, 12),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            });
        }
        else
        {
            phases.Add(new PhaseDefinition
            {
                name = "Breakdown",
                targetOffsetPercent = -3f,
                durationRange = new Vector2Int(6, 12),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            });
        }
    }
}
