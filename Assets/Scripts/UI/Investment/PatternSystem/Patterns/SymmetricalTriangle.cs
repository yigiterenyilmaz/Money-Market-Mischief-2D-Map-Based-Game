using System.Collections.Generic;
using UnityEngine;

// A8 Symmetrical Triangle — neutral, 18-28 mum.
// Hem tepeler alcalir hem dipler yukselir, sikisma. BREAKOUT trend yonune gore +/-6%.
// Init'te trend yonune gore (NewUpTrend ya da NewDownTrend) breakout yon belirler.

public class SymmetricalTriangle : PhasedPattern
{
    public override string Id => "A8_SymmetricalTriangle";
    public override float Weight => 3f;

    bool breakUp;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.NewUpTrend
            || context == MarketContext.NewDownTrend;
    }

    public override void Init(MarketState ms, float p0, float vm)
    {
        MarketContext ctx = ms.GetContext();
        breakUp = ctx == MarketContext.NewUpTrend || ctx == MarketContext.UpTrend;
        base.Init(ms, p0, vm);
    }

    protected override void BuildPhases()
    {
        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition { name = "Touch1Up", targetOffsetPercent = 4f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Touch1Down", targetOffsetPercent = -4f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Touch2Up", targetOffsetPercent = 2.5f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Small },
            new PhaseDefinition { name = "Touch2Down", targetOffsetPercent = -2.5f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Small },
            new PhaseDefinition { name = "Touch3Up", targetOffsetPercent = 1f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Micro },
            new PhaseDefinition { name = "Touch3Down", targetOffsetPercent = -1f, durationRange = new Vector2Int(2, 3), character = CandleCharacter.Micro },
            new PhaseDefinition
            {
                name = breakUp ? "BreakoutUp" : "BreakdownDown",
                targetOffsetPercent = breakUp ? 6f : -6f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, breakUp ? ColorBias.Green : ColorBias.Red)
            }
        };
    }
}
