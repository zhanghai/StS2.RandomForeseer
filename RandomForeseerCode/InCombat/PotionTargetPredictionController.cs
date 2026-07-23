using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class PotionTargetPredictionController
{
    private static ActivePotionTargeting? _activeTargeting;

    private static long _nextSessionId;

    public static long Begin(NPotionHolder holder, TargetType targetType)
    {
        Clear();

        if (targetType != TargetType.AnyPlayer ||
            holder.Potion?.Model is not { } source ||
            source.TargetType != TargetType.AnyPlayer)
        {
            return 0;
        }

        var targetObserver = new CombatPredictionTargetObserver(NTargetManager.Instance);
        var activeTargeting = new ActivePotionTargeting(++_nextSessionId, holder, source, targetObserver);
        _activeTargeting = activeTargeting;
        NHoverTipSet.Remove(holder);

        targetObserver.TargetChanged += OnTargetChanged;
        targetObserver.TargetingEnded += OnTargetingEnded;
        return activeTargeting.SessionId;
    }

    public static async Task CleanupAfterCompletion(NPotionHolder holder, long sessionId, Task targetingTask)
    {
        try
        {
            await targetingTask;
        }
        finally
        {
            if (_activeTargeting is { } activeTargeting &&
                activeTargeting.SessionId == sessionId &&
                ReferenceEquals(activeTargeting.Holder, holder))
            {
                Clear();
            }
        }
    }

    private static void OnTargetChanged(Creature? target)
    {
        if (_activeTargeting is not { } activeTargeting)
        {
            return;
        }

        if (target?.Player is not { } player || player.Creature.IsDead)
        {
            NHoverTipSet.Remove(activeTargeting.Holder);
            return;
        }

        ShowHoverTips(activeTargeting, player);
    }

    private static void OnTargetingEnded()
    {
        Clear();
    }

    private static void ShowHoverTips(ActivePotionTargeting activeTargeting, Player target)
    {
        IReadOnlyList<IHoverTip> hoverTips;
        try
        {
            hoverTips = PotionPrediction.GetHoverTips(activeTargeting.Source, target);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn(
                $"Potion target prediction failed for {activeTargeting.Source.Id} targeting {target.NetId}: {ex}");
            hoverTips = [];
        }

        NHoverTipSet.Remove(activeTargeting.Holder);
        if (hoverTips.Count == 0)
        {
            return;
        }

        // NTargetManager blocks ordinary hover tips while targeting. This is an explicit
        // target-specific prediction surface, so restore the global flag after showing it.
        var shouldBlockHoverTips = NHoverTipSet.shouldBlockHoverTips;
        NHoverTipSet.shouldBlockHoverTips = false;
        try
        {
            NHoverTipSet.CreateAndShow(activeTargeting.Holder, hoverTips, HoverTipAlignment.Center)
                ?.SetGlobalPosition(
                    activeTargeting.Holder.GlobalPosition +
                    Vector2.Down * activeTargeting.Holder.Size.Y * Mathf.Max(1.5f, activeTargeting.Holder.Scale.Y));
        }
        finally
        {
            NHoverTipSet.shouldBlockHoverTips = shouldBlockHoverTips;
        }
    }

    private static void Clear()
    {
        if (_activeTargeting is not { } activeTargeting)
        {
            return;
        }

        _activeTargeting = null;
        activeTargeting.TargetObserver.TargetChanged -= OnTargetChanged;
        activeTargeting.TargetObserver.TargetingEnded -= OnTargetingEnded;
        activeTargeting.TargetObserver.Dispose();
        NHoverTipSet.Remove(activeTargeting.Holder);
    }

    private sealed class ActivePotionTargeting(
        long sessionId,
        NPotionHolder holder,
        PotionModel source,
        CombatPredictionTargetObserver targetObserver)
    {
        public long SessionId { get; } = sessionId;

        public NPotionHolder Holder { get; } = holder;

        public PotionModel Source { get; } = source;

        public CombatPredictionTargetObserver TargetObserver { get; } = targetObserver;
    }
}

[HarmonyPatch(typeof(NPotionHolder), nameof(NPotionHolder.TargetNode), [typeof(TargetType)])]
internal static class PotionTargetPredictionPatch
{
    private static void Prefix(NPotionHolder __instance, TargetType targetType, out long __state)
    {
        __state = PotionTargetPredictionController.Begin(__instance, targetType);
    }

    private static void Postfix(NPotionHolder __instance, long __state, ref Task __result)
    {
        __result = PotionTargetPredictionController.CleanupAfterCompletion(__instance, __state, __result);
    }
}
