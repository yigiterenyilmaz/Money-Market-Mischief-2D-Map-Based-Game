using System.Collections.Generic;
using UnityEngine;

// A12 Rising Wedge — bearish reversal, 18-28 mum.
// Hem dipler hem tepeler yukselir ama tepe egimi az -> daralir, sonra breakdown.

public class RisingWedge : PhasedPattern
{
    public override string Id => "A12_RisingWedge";
    public override float Weight => 2f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend;
    }

    protected override void BuildPhases()
    {
        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition { name = "Touch1Down", targetOffsetPercent = 0f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Touch1Up", targetOffsetPercent = 3f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Touch2Down", targetOffsetPercent = 1f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Small },
            new PhaseDefinition { name = "Touch2Up", targetOffsetPercent = 3.5f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Small },
            new PhaseDefinition { name = "Touch3Down", targetOffsetPercent = 1.8f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Micro },
            new PhaseDefinition { name = "Touch3Up", targetOffsetPercent = 3.8f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Micro },
            new PhaseDefinition
            {
                name = "Breakdown",
                targetOffsetPercent = -4f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            }
        };
    }
}
