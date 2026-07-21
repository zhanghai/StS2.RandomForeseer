using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.Integrations.LemonSpire;
using RandomForeseer.RandomForeseerCode.OutOfCombat;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips;

/// <summary>
/// Appends feature-specific prediction tips when vanilla creates a control's HoverTip set.
/// </summary>
/// <remarks>
/// This patch only injects provider results after the supplied vanilla sequence. It does not create or own the
/// HoverTip set; <see cref="PredictionHoverTipSetHelper"/> is used by explicit prediction-only surfaces for that.
/// </remarks>
[HarmonyPatch(
    typeof(NHoverTipSet),
    nameof(NHoverTipSet.CreateAndShow),
    [typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment)])]
internal static class ControlHoverTipPredictionPatch
{
    private static readonly PredictionHoverTipRegistry<Control> Registry = CreateRegistry();

    private static PredictionHoverTipRegistry<Control> CreateRegistry()
    {
        var registry = new PredictionHoverTipRegistry<Control>();

        registry.Register("merchant entry", MerchantEntryHoverTips.GetHoverTips);
        registry.Register("transform selection", TransformSelectionHoverTips.GetHoverTips);
        registry.Register("treasure room relic", TreasureRoomRelicHoverTips.GetHoverTips);
        registry.Register("rest site", RestSiteHoverTips.GetHoverTips);
        registry.Register("combat transform selected holder", CombatTransformSelectedHoverTips.GetHoverTips);
        registry.Register("card reward alternative", CardRewardAlternativeButtonHoverTips.GetHoverTips);
        registry.Register("lemonSpire", LemonSpireControlHoverTips.GetHoverTips);

        return registry;
    }

    /// <summary>
    /// Appends all successful providers registered for the hovered control to vanilla tips.
    /// </summary>
    /// <remarks>
    /// The registry handles provider failures independently. Existing vanilla tips remain the prefix of the resulting
    /// sequence, preserving their order.
    /// </remarks>
    private static void Prefix(Control owner, ref IEnumerable<IHoverTip> hoverTips)
    {
        var predictionTips = Registry.GetHoverTips(owner);
        if (predictionTips.Count > 0)
        {
            hoverTips = hoverTips.Concat(predictionTips);
        }
    }
}

/// <summary>
/// Creates and tracks HoverTip sets that are explicitly owned by prediction-only UI surfaces.
/// </summary>
/// <remarks>
/// This helper never replaces an already active vanilla or prediction HoverTip set. Callers that receive a non-null
/// set from <see cref="EnsureHoverTipSet"/> must later call <see cref="RemoveOwnedHoverTipSet"/> for the same owner.
/// </remarks>
internal static class PredictionHoverTipSetHelper
{
    private static readonly ConditionalWeakTable<Control, NHoverTipSet> OwnedHoverTips = [];

    /// <summary>
    /// Creates an empty-owned HoverTip set, allowing the global prediction injection patch to populate it.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> when the owner already has an active set or vanilla creation fails; it does not
    /// return an existing set. Successful sets are recorded for ownership-aware cleanup.
    /// </remarks>
    public static NHoverTipSet? EnsureHoverTipSet(Control owner, HoverTipAlignment alignment = HoverTipAlignment.None)
    {
        if (NHoverTipSet._activeHoverTips.ContainsKey(owner))
        {
            return null;
        }

        var tipSet = NHoverTipSet.CreateAndShow(owner, [], alignment);
        if (tipSet == null)
        {
            return null;
        }

        OwnedHoverTips.AddOrUpdate(owner, tipSet);
        return tipSet;
    }

    /// <summary>
    /// Removes the prediction-owned set for an owner when it is still the active set.
    /// </summary>
    /// <remarks>
    /// This is safe to call repeatedly and never removes a newer or independently created HoverTip set.
    /// </remarks>
    public static void RemoveOwnedHoverTipSet(Control owner)
    {
        if (!OwnedHoverTips.TryGetValue(owner, out var tipSet))
        {
            return;
        }

        OwnedHoverTips.Remove(owner);

        if (NHoverTipSet._activeHoverTips.TryGetValue(owner, out var activeTipSet) &&
            ReferenceEquals(activeTipSet, tipSet))
        {
            NHoverTipSet.Remove(owner);
        }
    }
}
