using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.PotionOnUse;

internal static class PotionGenerationPotionMirrors
{
    public static void EntropicBrewOnUse(EntropicBrew _, PotionOnUseMirrorContext context)
    {
        var target = context.TargetPlayer;
        var potions = PredictionUtils.PredictPotionRewards(
            target,
            // Preserve the existing presentation policy: players may discard potions before using the brew,
            // so expose enough deterministic RNG results to fill the whole belt rather than only current gaps.
            target.PotionSlots.Count,
            context.Rng.CombatPotionGeneration);

        foreach (var potion in potions)
        {
            // Potion-slot mutation and procurement hooks are outside the current simulator state domains.
            context.History.PotionGenerated(potion);
        }
    }
}
