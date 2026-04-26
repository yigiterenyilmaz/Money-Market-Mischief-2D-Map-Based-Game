using System.Collections.Generic;
using UnityEngine;

// A11 Pennant — continuation (bull veya bear). Init'te trend yonune gore yon belirler.
// POLE (3-6 mum, +-6%, marubozu) -> PENNANT (6-10 mum, sikisma, kucuk govdeler) -> BREAKOUT (4-8 mum, +-10%).

public class Pennant : PhasedPattern
{
    public override string Id => "A11_Pennant";
    public override float Weight => 3f;

    bool isBullish;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.NewUpTrend
            || context == MarketContext.NewDownTrend;
    }

    public override void Init(MarketState ms, float p0, float vm)
    {
        MarketContext ctx = ms.GetContext();
        isBullish = ctx == MarketContext.NewUpTrend || ctx == MarketContext.UpTrend;
        base.Init(ms, p0, vm);
    }

    protected override void BuildPhases()
    {
        ColorBias poleBias = isBullish ? ColorBias.Green : ColorBias.Red;

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Pole",
                targetOffsetPercent = isBullish ? 6f : -6f,
                durationRange = new Vector2Int(3, 6),
                character = CandleCharacter.Large,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, poleBias)
            },
            new PhaseDefinition
            {
                // Pole hedefi etrafinda sikisik salinim
                name = "Pennant",
                targetOffsetPercent = isBullish ? 5f : -5f,
                durationRange = new Vector2Int(6, 10),
                character = CandleCharacter.Micro
            },
            new PhaseDefinition
            {
                name = "Breakout",
                targetOffsetPercent = isBullish ? 10f : -10f,
                durationRange = new Vector2Int(4, 8),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, poleBias)
            }
        };
    }
}
