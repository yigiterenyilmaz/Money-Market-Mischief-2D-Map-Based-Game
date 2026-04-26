using System.Collections.Generic;
using UnityEngine;

// D3 Liquidity Grab / Stop Hunt — yatay S/R seviyesini hizla asip geri donmus mum + reversal.
// GRAB phase tek mumluk: uzun fitil + kucuk ters body. Sonra REVERSAL.

public class LiquidityGrab : PhasedPattern
{
    public override string Id => "D3_LiquidityGrab";
    public override float Weight => 4f;

    bool grabDown;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.HighVolatility;
    }

    public override void Init(MarketState ms, float p0, float vm)
    {
        // %50 asagi grab (sonra yukari reversal), %50 yukari grab (sonra asagi reversal)
        grabDown = Random.value > 0.5f;
        base.Init(ms, p0, vm);
    }

    protected override void BuildPhases()
    {
        // GRAB mum'u: hammer/shooting star sekli — uzun fitil S/R asar, body ters yonde recovery
        CandleProfile grabCandle = grabDown
            ? CandleProfiles.Get(CandleCharacter.LongLowerWick, ColorBias.Green)
            : CandleProfiles.Get(CandleCharacter.LongUpperWick, ColorBias.Red);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition
            {
                name = grabDown ? "GrabDown" : "GrabUp",
                targetOffsetPercent = 0f,  // close ~ P0 (geri donmus halde)
                durationRange = new Vector2Int(1, 1),
                character = grabDown ? CandleCharacter.LongLowerWick : CandleCharacter.LongUpperWick,
                hasOpeningOverride = true,
                openingOverride = grabCandle
            },
            new PhaseDefinition
            {
                name = "Reversal",
                targetOffsetPercent = grabDown ? 4f : -4f,
                durationRange = new Vector2Int(5, 10),
                character = CandleCharacter.Medium
            }
        };
    }
}
