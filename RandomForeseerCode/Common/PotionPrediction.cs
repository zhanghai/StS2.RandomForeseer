using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PotionPrediction
{
    private static readonly PredictionHoverTipRegistry<PotionPredictionContext> PredictionProviders = CreateRegistry();

    private static PredictionHoverTipRegistry<PotionPredictionContext> CreateRegistry()
    {
        var registry = new PredictionHoverTipRegistry<PotionPredictionContext>();

        registry.Register("potion card generation", CombatCardGenerationPrediction.GetPotionHoverTips);
        registry.Register("potion generation", PotionGenerationPrediction.GetPotionHoverTips);
        registry.Register("potion auto-play from draw pile", AutoPlayFromDrawPilePrediction.GetPotionHoverTips);
        registry.Register("potion draw", PotionDrawPrediction.GetPotionHoverTips);

        return registry;
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(PotionModel potion)
    {
        return GetHoverTips(potion, potion.Owner);
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(PotionModel potion, Player target)
    {
        return PredictionProviders.GetHoverTips(new PotionPredictionContext(potion, target));
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Player player, PotionModel potion)
    {
        var previewPotion = PredictionUtils.CreatePotion(potion, player);
        return GetHoverTips(previewPotion, player);
    }
}

internal readonly record struct PotionPredictionContext(PotionModel Source, Player Target)
{
    public Player SourceOwner => Source.Owner;
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
