using System.Collections.Generic;
using UnityEngine;

// A10 Bear Flag — bearish continuation, A9'un tersi.

public class BearFlag : PhasedPattern
{
    public override string Id => "A10_BearFlag";
    public override float Weight => 3f;
    protected override float FailureChance => 0.15f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.NewDownTrend;
    }

    protected override void BuildPhases()
    {
        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Flagpole",
                targetOffsetPercent = -6f,
                durationRange = new Vector2Int(3, 6),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            },
            new PhaseDefinition
            {
                name = "Flag",
                targetOffsetPercent = -4f,
                durationRange = new Vector2Int(6, 12),
                character = CandleCharacter.Small
            }
        };

        if (isFailedRun)
        {
            phases.Add(new PhaseDefinition
            {
                name = "FailedRally",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(4, 8),
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
                targetOffsetPercent = -10f,
                durationRange = new Vector2Int(4, 8),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            });
        }
    }
}
