using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.PotionOnUse;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PotionPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(PotionModel potion)
    {
        if (potion.Owner is not { } owner)
        {
            return [];
        }

        if (owner.Creature.CombatState is not null)
        {
            return CombatPotionPrediction.GetHoverTips(potion);
        }

        try
        {
            return GetOutOfCombatHoverTips(potion, owner);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Out-of-combat potion prediction failed for {potion.Id}: {ex}");
            return [];
        }
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Player player, PotionModel potion)
    {
        return GetHoverTips(PredictionUtils.CreatePotion(potion, player));
    }

    /// <summary>Builds only the pure RNG previews supported outside combat.</summary>
    private static List<IHoverTip> GetOutOfCombatHoverTips(PotionModel potion, Player target)
    {
        List<IHoverTip> hoverTips = [];

        if (RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionCardPrediction) &&
            RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes))
        {
            var rng = target.RunState.Rng.CombatCardGeneration.Clone();
            if (CardGenerationPotionMirrors.Generate(potion, target, rng) is { } result)
            {
                hoverTips.AddRange(result.Cards.SelectPreviews().ToPredictionHoverTips());
            }
        }

        if (RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction) &&
            potion is EntropicBrew)
        {
            var rng = target.RunState.Rng.CombatPotionGeneration.Clone();
            hoverTips.AddRange(EntropicBrewMirrors.Generate(target, rng).ToPredictionHoverTips());
        }

        return hoverTips;
    }
}

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.HoverTips), MethodType.Getter)]
internal static class PotionPredictionHoverTipsPatch
{
    private static void Postfix(PotionModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!__instance.IsMutable || __instance.Owner == null || __instance.Owner.RunState is not RunState)
        {
            return;
        }

        var predictionTips = PotionPrediction.GetHoverTips(__instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}
