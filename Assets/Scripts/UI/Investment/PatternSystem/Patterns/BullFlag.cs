using System.Collections.Generic;
using UnityEngine;

// A9 Bull Flag — bullish continuation, 12-22 mum.
// FLAGPOLE (3-6 mum, P0+6%, pes pese buyuk yesil) ->
// FLAG (6-12 mum, P0+4%, paralel kanal asagi, kucuk govdeler) ->
// BREAKOUT (4-8 mum, P0+10%, marubozu yesil).
// Failed (~%15): breakout yerine -4% selloff (P0+0%).

public class BullFlag : PhasedPattern
{
    public override string Id => "A9_BullFlag";
    public override float Weight => 3f;
    protected override float FailureChance => 0.15f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.NewUpTrend;
    }

    protected override void BuildPhases()
    {
        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Flagpole",
                targetOffsetPercent = 6f,
                durationRange = new Vector2Int(3, 6),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            },
            new PhaseDefinition
            {
                name = "Flag",
                targetOffsetPercent = 4f,
                durationRange = new Vector2Int(6, 12),
                character = CandleCharacter.Small
            }
        };

        if (isFailedRun)
        {
            phases.Add(new PhaseDefinition
            {
                name = "FailedSelloff",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(4, 8),
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
                targetOffsetPercent = 10f,
                durationRange = new Vector2Int(4, 8),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            });
        }
    }
}
