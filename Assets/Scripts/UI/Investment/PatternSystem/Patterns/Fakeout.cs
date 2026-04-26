using System.Collections.Generic;
using UnityEngine;

// D4 Fakeout (yalanci kirilim) — 8-14 mum. False break -> rejection -> true move (ters yon).

public class Fakeout : PhasedPattern
{
    public override string Id => "D4_Fakeout";
    public override float Weight => 4f;

    bool falseUp;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.HighVolatility
            || context == MarketContext.Sideways;
    }

    public override void Init(MarketState ms, float p0, float vm)
    {
        falseUp = Random.value > 0.5f;
        base.Init(ms, p0, vm);
    }

    protected override void BuildPhases()
    {
        // FALSE UP: yukari kirilir gibi, sonra reject + asagi true move
        // FALSE DOWN: tersi
        CandleProfile rejection = falseUp
            ? CandleProfiles.Get(CandleCharacter.LongUpperWick, ColorBias.Red)
            : CandleProfiles.Get(CandleCharacter.LongLowerWick, ColorBias.Green);

        CandleProfile trueMoveOpening = CandleProfiles.Get(
            CandleCharacter.Marubozu,
            falseUp ? ColorBias.Red : ColorBias.Green);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "FalseBreak",
                targetOffsetPercent = falseUp ? 2f : -2f,
                durationRange = new Vector2Int(2, 3),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "Rejection",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(1, 2),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = rejection
            },
            new PhaseDefinition
            {
                name = "TrueMove",
                targetOffsetPercent = falseUp ? -5f : 5f,
                durationRange = new Vector2Int(5, 9),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = trueMoveOpening
            }
        };
    }
}
