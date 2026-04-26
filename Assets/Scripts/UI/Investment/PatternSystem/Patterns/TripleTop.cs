using System.Collections.Generic;
using UnityEngine;

// A5 Triple Top — bearish reversal, 25-45 mum. Double Top'un 3 tepeli versiyonu.
// 3 tepe (P0+5%) + 2 ara vadi (P0+1%) + breakdown (P0-3%).
// Failed (~%10): breakdown yerine +8% rally.

public class TripleTop : PhasedPattern
{
    public override string Id => "A5_TripleTop";
    public override float Weight => 2f;
    protected override float FailureChance => 0.10f;

    public override bool MatchesContext(MarketContext context)
    {
        return context == MarketContext.UpTrend;
    }

    protected override void BuildPhases()
    {
        CandleProfile rejection = CandleProfiles.Get(CandleCharacter.Small);

        phases = new List<PhaseDefinition>
        {
            new PhaseDefinition { name = "Top1Up", targetOffsetPercent = 5f, durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Top1Down", targetOffsetPercent = 1f, durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Top2Up", targetOffsetPercent = 5f + Random.Range(-0.3f, 0.3f), durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium },
            new PhaseDefinition { name = "Top2Down", targetOffsetPercent = 1f, durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium },
            new PhaseDefinition
            {
                name = "Top3Up",
                targetOffsetPercent = 5f + Random.Range(-0.3f, 0.3f),
                durationRange = new Vector2Int(3, 5),
                character = CandleCharacter.Medium,
                hasClosingOverride = true,
                closingOverride = rejection
            },
            new PhaseDefinition { name = "Top3Down", targetOffsetPercent = 1f, durationRange = new Vector2Int(3, 5), character = CandleCharacter.Medium }
        };

        if (isFailedRun)
        {
            phases.Add(new PhaseDefinition
            {
                name = "FailedRally",
                targetOffsetPercent = 8f,
                durationRange = new Vector2Int(6, 12),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Green)
            });
        }
        else
        {
            phases.Add(new PhaseDefinition
            {
                name = "Breakdown",
                targetOffsetPercent = -3f,
                durationRange = new Vector2Int(6, 12),
                character = CandleCharacter.Medium,
                hasOpeningOverride = true,
                openingOverride = CandleProfiles.Get(CandleCharacter.Marubozu, ColorBias.Red)
            });
        }
    }
}
