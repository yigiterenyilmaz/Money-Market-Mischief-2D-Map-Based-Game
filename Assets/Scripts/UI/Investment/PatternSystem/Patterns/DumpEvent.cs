using System.Collections.Generic;
using UnityEngine;

// D2 Dump (Capitulation) — bearish event, 5-10 mum.
// BREAK -> ACCELERATION -> CAPITULATION -> BOUNCE (dead cat).
// Toplam hareket P0-8% to P0-12%.

public class DumpEvent : PhasedPattern
{
    public override string Id => "D2_Dump";
    public override float Weight => 1f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend
            || context == MarketContext.DownTrend
            || context == MarketContext.Sideways;
    }

    protected override void BuildPhases()
    {
        CandleProfile breakOpening = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Break",
                targetOffsetPercent = -2f,
                durationRange = new Vector2Int(1, 2),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = breakOpening
            },
            new PhaseDefinition
            {
                name = "Acceleration",
                targetOffsetPercent = -6f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Large
            },
            new PhaseDefinition
            {
                name = "Capitulation",
                targetOffsetPercent = -11f,
                durationRange = new Vector2Int(2, 4),
                character = CandleCharacter.Marubozu,
                hasClosingOverride = true,
                closingOverride = CandleProfiles.Get(CandleCharacter.LongLowerWick, ColorBias.Red)
            },
            new PhaseDefinition
            {
                name = "Bounce",
                targetOffsetPercent = -9f,
                durationRange = new Vector2Int(1, 2),
                character = CandleCharacter.Small
            }
        };
    }
}
