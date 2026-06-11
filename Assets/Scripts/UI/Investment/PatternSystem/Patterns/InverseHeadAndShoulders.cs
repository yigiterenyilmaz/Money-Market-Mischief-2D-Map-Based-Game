using System.Collections.Generic;
using UnityEngine;

// A2 Inverse Head and Shoulders — bullish reversal, 30-46 mum.
// A1'in tersi: dipler ters cevrilmis (sol omuz dipte, bas en derinde, sag omuz dipte) + breakout.
// Failed (~%12): breakout yerine -5% selloff.

public class InverseHeadAndShoulders : PhasedPattern
{
    public override string Id => "A2_InverseHeadAndShoulders";
    public override float Weight => 1f;
    protected override float FailureChance => 0.12f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.DownTrend;
    }

    protected override void BuildPhases()
    {
        // Bas dibindeki son mum: hammer veya doji
        CandleCharacter headBottomShape = Random.value > 0.5f
            ? CandleCharacter.Doji
            : CandleCharacter.LongLowerWick;
        CandleProfile headBottomClose = CandleProfiles.Get(headBottomShape);

        CandleProfile breakoutOpening = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Setup",
                targetOffsetPercent = -1f,
                durationRange = new Vector2Int(4, 6),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "LeftShoulderDown",
                targetOffsetPercent = -4f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "LeftShoulderUp",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "HeadDown",
                targetOffsetPercent = -8f,
                durationRange = new Vector2Int(4, 6),
                character = CandleCharacter.Large,
                hasClosingOverride = true,
                closingOverride = headBottomClose
            },
            new PhaseDefinition
            {
                name = "HeadUp",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(4, 6),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "RightShoulderDown",
                targetOffsetPercent = -4f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "RightShoulderUp",
                targetOffsetPercent = 1f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            }
        };

        if (isFailedRun)
        {
            phases.Add(new PhaseDefinition
            {
                name = "FailedSelloff",
                targetOffsetPercent = -5f,
                durationRange = new Vector2Int(6, 10),
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
                durationRange = new Vector2Int(6, 10),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = breakoutOpening
            });
        }
    }
}
