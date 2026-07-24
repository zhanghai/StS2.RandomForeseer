using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.PotionOnUse;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PotionGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionPredictionContext context)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction))
        {
            return [];
        }

        if (context.Source is not EntropicBrew)
        {
            return [];
        }

        var rng = context.Target.RunState.Rng.CombatPotionGeneration.Clone();
        return [.. EntropicBrewMirrors.Generate(context.Target, rng).ToPredictionHoverTips()];
    }
}
