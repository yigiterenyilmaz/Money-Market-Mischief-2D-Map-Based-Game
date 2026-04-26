using System.Collections.Generic;
using UnityEngine;

// B1 Wyckoff Accumulation — long bullish setup, 60-100 mum.
// PS (Preliminary Support) -> SC (Selling Climax) -> AR (Automatic Rally) ->
// ST (Secondary Test) -> SPRING (false breakdown, uzun alt fitil) ->
// TEST -> SOS (Sign of Strength) -> LPS (Last Point of Support) -> MARKUP.

public class WyckoffAccumulation : PhasedPattern
{
    public override string Id => "B1_WyckoffAccumulation";
    public override float Weight => 0.5f;

    public override bool MatchesContext(MarketContext context)
    {
        // Uzun downtrend gerektirir
        return context == MarketContext.DownTrend;
    }

    protected override void BuildPhases()
    {
        CandleProfile springCandle = CandleProfiles.Get(CandleCharacter.LongLowerWick, ColorBias.Green);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "PreliminarySupport",
                targetOffsetPercent = -3f,
                durationRange = new Vector2Int(5, 8),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "SellingClimax",
                targetOffsetPercent = -11f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Marubozu,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            },
            new PhaseDefinition
            {
                name = "AutomaticRally",
                targetOffsetPercent = -3f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Large, ColorBias.Green)
            },
            new PhaseDefinition
            {
                name = "SecondaryTest",
                targetOffsetPercent = -9f,
                durationRange = new Vector2Int(8, 15),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "Spring",
                targetOffsetPercent = -9f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.LongLowerWick,
                hasOpeningOverride = true,
                openingOverride = springCandle
            },
            new PhaseDefinition
            {
                name = "Test",
                targetOffsetPercent = -7f,
                durationRange = new Vector2Int(4, 7),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "SignOfStrength",
                targetOffsetPercent = 1f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            },
            new PhaseDefinition
            {
                name = "LastPointOfSupport",
                targetOffsetPercent = -1f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Small
            },
            new PhaseDefinition
            {
                name = "Markup",
                targetOffsetPercent = 10f,
                durationRange = new Vector2Int(10, 15),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            }
        };
    }
}
