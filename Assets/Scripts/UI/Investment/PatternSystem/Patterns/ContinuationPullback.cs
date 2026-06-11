using System.Collections.Generic;
using UnityEngine;

// D6 Trend Continuation Pullback — guclu trend icinde geri cekilme + devam.
// Init'te trend yonune gore yon belirler: bullish trend -> pullback asagi, resumption yukari.

public class ContinuationPullback : PhasedPattern
{
    public override string Id => "D6_ContinuationPullback";
    public override float Weight => 4f;

    bool isBullish;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.NewUpTrend
            || context == MarketContext.NewDownTrend
            || context == MarketContext.UpTrend
            || context == MarketContext.DownTrend;
    }

    public override void Init(MarketState ms, float p0, float vm)
    {
        MarketContext ctx = ms.GetContext();
        isBullish = ctx == MarketContext.UpTrend || ctx == MarketContext.NewUpTrend;
        base.Init(ms, p0, vm);
    }

    protected override void BuildPhases()
    {
        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = "Pullback",
                targetOffsetPercent = isBullish ? -2.5f : 2.5f,
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium
            },
            new PhaseDefinition
            {
                name = "Resumption",
                targetOffsetPercent = isBullish ? 5f : -5f,
                durationRange = new Vector2Int(3, 7),
                character = CandleCharacter.Medium
            }
        };
    }
}
