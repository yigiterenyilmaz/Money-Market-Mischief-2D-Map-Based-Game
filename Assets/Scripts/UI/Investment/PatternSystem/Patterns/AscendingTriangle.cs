using System.Collections.Generic;
using UnityEngine;

// A6 Ascending Triangle — bullish continuation, 18-28 mum.
// Yatay direnc P0+4% (sabit), dipler her seferinde yukselir (P0+0%, +1%, +2%).
// 3 touch + final breakout (P0+8%, marubozu acilis).

public class AscendingTriangle : PhasedPattern
{
    public override string Id => "A6_AscendingTriangle";
    public override float Weight => 3f;
    protected override float FailureChance => 0.15f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.NewUpTrend
            || context == MarketContext.UpTrend;
    }

    protected override void BuildPhases()
    {
        CandleProfile dojiClose = CandleProfiles.Get(CandleCharacter.Doji);
        CandleProfile smallClose = CandleProfiles.Get(CandleCharacter.Small);
        CandleProfile breakoutOpening = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green);

        phases = new List<PhaseDefinition>
        {
            // Touch 1
            new PhaseDefinition
            {
                name = "Touch1Down",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "Touch1Up",
                targetOffsetPercent = 4f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Medium,
                hasClosingOverride = true,
                closingOverride = dojiClose // direnc rejection
            },

            // Touch 2 — dip yukseliyor
            new PhaseDefinition
            {
                name = "Touch2Down",
                targetOffsetPercent = 1f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "Touch2Up",
                targetOffsetPercent = 4f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Medium,
                hasClosingOverride = true,
                closingOverride = smallClose // momentum azaliyor
            },

            // Touch 3 — daha sikisik
            new PhaseDefinition
            {
                name = "Touch3Down",
                targetOffsetPercent = 2f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "Touch3Up",
                targetOffsetPercent = 4f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Small
            }
        };

        if (isFailedRun)
        {
            phases.Add(new PhaseDefinition
            {
                name = "FailedSelloff",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(5, 10),
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
                targetOffsetPercent = 8f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = breakoutOpening
            });
        }
    }
}
