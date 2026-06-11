using System.Collections.Generic;
using UnityEngine;

// A5 Triple Bottom — bullish reversal, 25-45 mum. Triple Top'un tersi.
// 3 dip (P0-5%) + 2 ara zirve (P0-1%) + breakout (P0+3%).
// Failed (~%10): -8% selloff.

public class TripleBottom : PhasedPattern
{
    public override string Id => "A5_TripleBottom";
    public override float Weight => 2f;
    protected override float FailureChance => 0.10f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.DownTrend;
    }

    protected override void BuildPhases()
    {
        CandleProfile thirdBottomReject = CandleProfiles.Get(CandleCharacter.LongLowerWick);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition { name = "Bottom1Down", targetOffsetPercent = -5f, durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Bottom1Up", targetOffsetPercent = -1f, durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Bottom2Down", targetOffsetPercent = -5f + Random.Range(-0.3f, 0.3f), durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Bottom2Up", targetOffsetPercent = -1f, durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium },
            new PhaseDefinition
            {
                name = "Bottom3Down",
                targetOffsetPercent = -5f + Random.Range(-0.3f, 0.3f),
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium,
                hasClosingOverride = true,
                closingOverride = thirdBottomReject
            },
            new PhaseDefinition { name = "Bottom3Up", targetOffsetPercent = -1f, durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium }
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
