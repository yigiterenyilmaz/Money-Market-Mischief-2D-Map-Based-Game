using System.Collections.Generic;
using UnityEngine;

// D5 Squeeze — volatilite sikismasi. Tightening (mikro mumlar) + Explosion (marubozu).

public class Squeeze : PhasedPattern
{
    public override string Id => "D5_Squeeze";
    public override float Weight => 4f;

    bool explodeUp;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.Sideways;
    }

    public override void Init(MarketState ms, float p0, float vm)
    {
        explodeUp = Random.value > 0.5f;
        base.Init(ms, p0, vm);
    }

    protected override void BuildPhases()
    {
        CandleProfile explosionOpening = CandleProfiles.Get(
            CandleCharacter.Marubozu,
            explodeUp ? ColorBias.Green : ColorBias.Red);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Tightening",
                targetOffsetPercent = 0f,
                durationRange = new Vector2Int(8, 15),
                character = CandleCharacter.Micro
            },
            new PhaseDefinition
            {
                name = "Explosion",
                targetOffsetPercent = explodeUp ? 5f : -5f,
                durationRange = new Vector2Int(4, 8),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = explosionOpening
            }
        };
    }
}
