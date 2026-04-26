using System.Collections.Generic;
using UnityEngine;

// A14 Cup and Handle — bullish continuation, 40-70 mum.
// Yumusak U-sekli iniş + cikis + kucuk handle pullback + breakout.

public class CupAndHandle : PhasedPattern
{
    public override string Id => "A14_CupAndHandle";
    public override float Weight => 1f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.Sideways
            || context == MarketContext.UpTrend
            || context == MarketContext.NewUpTrend;
    }

    protected override void BuildPhases()
    {
        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition { name = "LeftRim", targetOffsetPercent = 0f, durationRange = new Vector2Int(4, 6), character = CandleCharacter.Micro },
            new PhaseDefinition { name = "IntoCup", targetOffsetPercent = -8f, durationRange = new Vector2Int(8, 14), character = CandleCharacter.Small },
            new PhaseDefinition { name = "CupBottom", targetOffsetPercent = -8f, durationRange = new Vector2Int(5, 8), character = CandleCharacter.Micro },
            new PhaseDefinition { name = "OutOfCup", targetOffsetPercent = 0f, durationRange = new Vector2Int(8, 14), character = CandleCharacter.Small },
            new PhaseDefinition { name = "Handle", targetOffsetPercent = -2f, durationRange = new Vector2Int(4, 8), character = CandleCharacter.Small },
            new PhaseDefinition
            {
                name = "Breakout",
                targetOffsetPercent = 8f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            }
        };
    }
}
