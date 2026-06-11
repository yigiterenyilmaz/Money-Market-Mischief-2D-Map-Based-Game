using System.Collections.Generic;
using UnityEngine;

// A15 Rectangle / Range — neutral, 20-40 mum.
// Support P0-2%, Resistance P0+2%, 3 ileri-geri touch + breakout (yon rastgele).

public class Rectangle : PhasedPattern
{
    public override string Id => "A15_Rectangle";
    public override float Weight => 3f;

    bool breakUp;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.Sideways;
    }

    public override void Init(MarketState ms, float p0, float vm)
    {
        breakUp = Random.value > 0.5f;
        base.Init(ms, p0, vm);
    }

    protected override void BuildPhases()
    {
        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition { name = "Touch1Up", targetOffsetPercent = 2f, durationRange = new Vector2Int(2, 4), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Touch1Down", targetOffsetPercent = -2f, durationRange = new Vector2Int(2, 4), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Touch2Up", targetOffsetPercent = 2f + Random.Range(-0.3f, 0.3f), durationRange = new Vector2Int(2, 4), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Touch2Down", targetOffsetPercent = -2f + Random.Range(-0.3f, 0.3f), durationRange = new Vector2Int(2, 4), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Touch3Up", targetOffsetPercent = 2f, durationRange = new Vector2Int(2, 4), character = CandleCharacter.Small },
            new PhaseDefinition { name = "Touch3Down", targetOffsetPercent = -2f, durationRange = new Vector2Int(2, 4), character = CandleCharacter.Small },
            new PhaseDefinition
            {
                name = breakUp ? "BreakoutUp" : "BreakdownDown",
                targetOffsetPercent = breakUp ? 4f : -4f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, breakUp ? ColorBias.Green : ColorBias.Red)
            }
        };
    }
}
