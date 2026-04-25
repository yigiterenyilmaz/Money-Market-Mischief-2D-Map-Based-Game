using System.Collections.Generic;
using UnityEngine;

// A1 Head and Shoulders — bearish reversal, 30-46 mum.
// Setup -> Sol Omuz Up/Down -> Bas Up (Doji veya Shooting Star kapanis) /Down ->
// Sag Omuz Up/Down -> Breakdown (Marubozu kirmizi acilis).

public class HeadAndShoulders : PhasedPattern
{
    public override string Id => "A1_HeadAndShoulders";
    public override float Weight => 1f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend;
    }

    protected override void BuildPhases()
    {
        // Bas tepesindeki son mum: doji veya shooting star
        CandleCharacter headTopShape = Random.value > 0.5f
            ? CandleCharacter.Doji
            : CandleCharacter.LongUpperWick;
        CandleProfile headTopClose = CandleProfiles.Get(headTopShape);

        CandleProfile breakdownOpening = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Setup",
                targetOffsetPercent = 1f,
                durationRange = new Vector2Int(4, 6),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "LeftShoulderUp",
                targetOffsetPercent = 4f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "LeftShoulderDown",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "HeadUp",
                targetOffsetPercent = 8f,
                durationRange = new Vector2Int(4, 6),
                character = CandleCharacter.Large,
                hasClosingOverride = true,
                closingOverride = headTopClose
            },
            new PhaseDefinition
            {
                name = "HeadDown",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(4, 6),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "RightShoulderUp",
                targetOffsetPercent = 4f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "RightShoulderDown",
                targetOffsetPercent = -1f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "Breakdown",
                targetOffsetPercent = -8f,
                durationRange = new Vector2Int(6, 10),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = breakdownOpening
            }
        };
    }
}
