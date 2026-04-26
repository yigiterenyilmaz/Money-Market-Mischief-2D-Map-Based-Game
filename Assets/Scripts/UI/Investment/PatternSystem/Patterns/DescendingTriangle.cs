using System.Collections.Generic;
using UnityEngine;

// A7 Descending Triangle — bearish continuation, 18-28 mum.
// A6'nin tersi: yatay support (P0-4%), tepeler her seferinde alcalir.
// Failed (~%15): breakdown yerine +4% rally.

public class DescendingTriangle : PhasedPattern
{
    public override string Id => "A7_DescendingTriangle";
    public override float Weight => 3f;
    protected override float FailureChance => 0.15f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.NewDownTrend
            || context == MarketContext.DownTrend;
    }

    protected override void BuildPhases()
    {
        CandleProfile dojiClose = CandleProfiles.Get(CandleCharacter.Doji);
        CandleProfile smallClose = CandleProfiles.Get(CandleCharacter.Small);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition { name = "Touch1Up", targetOffsetPercent = 0f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Medium },
            new PhaseDefinition
            {
                name = "Touch1Down",
                targetOffsetPercent = -4f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Medium,
                hasClosingOverride = true,
                closingOverride = dojiClose
            },
            new PhaseDefinition { name = "Touch2Up", targetOffsetPercent = -1f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Small },
            new PhaseDefinition
            {
                name = "Touch2Down",
                targetOffsetPercent = -4f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Small,
                hasClosingOverride = true,
                closingOverride = smallClose
            },
            new PhaseDefinition { name = "Touch3Up", targetOffsetPercent = -2f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Small },
            new PhaseDefinition { name = "Touch3Down", targetOffsetPercent = -4f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Micro }
        };

        if (isFailedRun)
        {
            phases.Add(new PhaseDefinition
            {
                name = "FailedRally",
                targetOffsetPercent = 4f,
                durationRange = new Vector2Int(5, 10),
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
                targetOffsetPercent = -8f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            });
        }
    }
}
