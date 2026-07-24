using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.HoverTips;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.PotionOnUse;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionPredictionContext context)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionCardPrediction) ||
            !ShouldShowPotionCardPrediction(context))
        {
            return [];
        }

        var rng = context.Target.RunState.Rng.CombatCardGeneration.Clone();
        var result = CardGenerationPotionMirrors.Generate(context.Source, context.Target, rng);
        return result is not null
            ? [.. result.Cards.SelectPreviews().ToPredictionHoverTips()]
            : [];
    }

    private static bool ShouldShowPotionCardPrediction(PotionPredictionContext context)
    {
        return RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes) ||
            CombatManager.Instance.IsInProgress &&
            !context.SourceOwner.Creature.IsDead &&
            !context.Target.Creature.IsDead;
    }
}
