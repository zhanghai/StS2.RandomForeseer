using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PotionGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionPredictionContext context)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction))
        {
            return [];
        }

        return [.. PredictPotions(context).ToPredictionHoverTips()];
    }

    private static IReadOnlyList<PotionModel> PredictPotions(PotionPredictionContext context)
    {
        var source = context.Source;
        var target = context.Target;

        return source switch
        {
            EntropicBrew => PredictionUtils.PredictPotionRewards(
                target,
                // The player may discard existing potions before drinking Entropic Brew, so show
                // enough future results to fill the entire potion belt rather than only open slots.
                target.PotionSlots.Count,
                target.RunState.Rng.CombatPotionGeneration.Clone()),
            _ => []
        };
    }
}
