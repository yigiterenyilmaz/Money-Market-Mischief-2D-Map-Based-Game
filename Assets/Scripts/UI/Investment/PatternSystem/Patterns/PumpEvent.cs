using System.Collections.Generic;
using UnityEngine;

// D1 Pump (FOMO Rally) — bullish event, 5-10 mum.
// Phase'ler: Acceleration -> Climax (Marubozu acilis) -> Exhaustion (Doji veya Shooting Star kapanis).
// Toplam hareket P0+8% to P0+12%.

public class PumpEvent : PhasedPattern
{
    public override string Id => "D1_Pump";
    public override float Weight => 1f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend
            || context == MarketContext.NewUpTrend
            || context == MarketContext.Sideways;
    }

    protected override void BuildPhases()
    {
        CandleProfile climaxOpening = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green);

        // Exhaustion son mumu: Doji veya LongUpperWick (rastgele)
        CandleCharacter exhaustionShape = Random.value > 0.5f
            ? CandleCharacter.Doji
            : CandleCharacter.LongUpperWick;
        CandleProfile exhaustionClosing = CandleProfiles.Get(exhaustionShape);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Acceleration",
                targetOffsetPercent = 3f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "Climax",
                targetOffsetPercent = 10f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = climaxOpening
            },
            new PhaseDefinition
            {
                name = "Exhaustion",
                targetOffsetPercent = 11f,
                durationRange = new Vector2Int(1, 2),
                character = CandleCharacter.Small,
                hasClosingOverride = true,
                closingOverride = exhaustionClosing
            }
        };
    }
}
