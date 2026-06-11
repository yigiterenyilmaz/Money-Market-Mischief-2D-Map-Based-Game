using System.Collections.Generic;
using UnityEngine;

// B2 Wyckoff Distribution — long bearish setup, 60-100 mum. B1'in tersi.
// PSY (Preliminary Supply) -> BC (Buying Climax) -> AR -> ST ->
// UPTHRUST (false breakout, uzun ust fitil) -> TEST -> SOW -> LPSY -> MARKDOWN.

public class WyckoffDistribution : PhasedPattern
{
    public override string Id => "B2_WyckoffDistribution";
    public override float Weight => 0.5f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend;
    }

    protected override void BuildPhases()
    {
        CandleProfile upthrustCandle = CandleProfiles.Get(CandleCharacter.LongUpperWick, ColorBias.Red);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "PreliminarySupply",
                targetOffsetPercent = 3f,
                durationRange = new Vector2Int(5, 8),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "BuyingClimax",
                targetOffsetPercent = 11f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Marubozu,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            },
            new PhaseDefinition
            {
                name = "AutomaticReaction",
                targetOffsetPercent = 3f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Large, ColorBias.Red)
            },
            new PhaseDefinition
            {
                name = "SecondaryTest",
                targetOffsetPercent = 9f,
                durationRange = new Vector2Int(8, 15),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "Upthrust",
                targetOffsetPercent = 9f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.LongUpperWick,
                hasOpeningOverride = true,
                openingOverride = upthrustCandle
            },
            new PhaseDefinition
            {
                name = "Test",
                targetOffsetPercent = 7f,
                durationRange = new Vector2Int(4, 7),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "SignOfWeakness",
                targetOffsetPercent = -1f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            },
            new PhaseDefinition
            {
                name = "LastPointOfSupply",
                targetOffsetPercent = 1f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "Markdown",
                targetOffsetPercent = -10f,
                durationRange = new Vector2Int(10, 15),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            }
        };
    }
}
