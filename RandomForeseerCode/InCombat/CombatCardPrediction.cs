using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>
/// Provides the single simulation facade used by combat-card HoverTips and target-aware UI surfaces.
/// </summary>
internal static class CombatCardPrediction
{
    /// <summary>
    /// Builds prediction HoverTips for an in-combat card without requiring a selected target.
    /// </summary>
    /// <remarks>
    /// Card-play projection is shown only where an untargeted prediction can be resolved. Combat transform
    /// prediction remains a separate presentation path and is appended after the unified card-play result.
    /// </remarks>
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!card.IsMutable)
        {
            return [];
        }

        List<IHoverTip> predictionTips = [];

        if (ShouldShowCombatPlayPrediction(card))
        {
            try
            {
                predictionTips.AddRange(Predict(card, target: null)?.HoverTips ?? []);
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Combat card play prediction failed for {card.Id}: {ex}");
            }
        }

        try
        {
            predictionTips.AddRange(CombatTransformPrediction.GetCardHoverTips(card));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat transform prediction failed for {card.Id}: {ex}");
        }

        return predictionTips;
    }

    /// <summary>
    /// Simulates one card play and projects every enabled card-play prediction feature from the same history.
    /// </summary>
    /// <param name="card">The live mutable card whose original identity anchors the prediction trace.</param>
    /// <param name="target">The selected target, or <see langword="null"/> when the card can resolve one automatically.</param>
    /// <returns>
    /// The completed presentation projection, or <see langword="null"/> when no feature is enabled, the card cannot
    /// be mirrored, its target cannot be resolved, or the simulated play is invalid.
    /// </returns>
    /// <remarks>Simulation and projection exceptions are intentionally handled by the calling UI injection boundary.</remarks>
    public static CombatCardPredictionProjection? Predict(CardModel card, Creature? target)
    {
        if (!CombatCardPredictionProjector.HasEnabledFeature() ||
            !card.IsMutable ||
            card.Owner?.Creature.CombatState is not { } combatState ||
            !CardOnPlayMirrors.CanMirror(card) ||
            !card.TryResolveTarget(ref target))
        {
            return null;
        }

        var simulator = new CombatPredictionSimulator(combatState);
        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);

        if (simulator.ManualPlay(predictedCard, target, out var frame))
        {
            return CombatCardPredictionProjector.Project(simulator.History, card, frame);
        }

        return null;
    }

    private static bool ShouldShowCombatPlayPrediction(CardModel card)
    {
        if (card.Owner?.Creature.CombatState == null)
        {
            return false;
        }

        if (ChooseACardPredictionContext.Contains(card))
        {
            // Cards shown by NChooseACardSelectionScreen are generated mutable cards with an owner,
            // but they are not in any combat pile yet. Treat them like cards that will enter hand
            // after the player chooses them, so they can show the same play predictions as hand cards.
            return true;
        }

        if (card.Pile is { Type: PileType.Hand })
        {
            if (NPlayerHand.Instance is { } hand && hand.GetCardHolder(card) is { } localHolder)
            {
                // For the local hand UI, only show play predictions in normal play mode, not selection modes.
                return hand.CurrentMode == NPlayerHand.Mode.Play && localHolder is NHandCardHolder;
            }

            // If no local holder exists, fall back to allowing prediction. This preserves existing
            // behavior for non-local or integration-provided hand card views.
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class CombatCardPredictionHoverTipsPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        var predictionTips = CombatCardPrediction.GetHoverTips(__instance);
        if (predictionTips.Count > 0)
        {
            __result = __result.Concat(predictionTips);
        }
    }
}
