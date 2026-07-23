using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardPredictionController
{
    private static CardPredictionSession? _session;

    private static bool _hasDamagePrediction;

    public static void OnCardHover(NHandCardHolder holder, bool isHovered)
    {
        if (isHovered)
        {
            StartPrediction(CardPredictionSource.Hover, holder);
        }
        else
        {
            ClearPredictions(CardPredictionSource.Hover, holder);
        }
    }

    public static void OnCardPlayStarted(NHandCardHolder holder)
    {
        StartPrediction(CardPredictionSource.CardPlay, holder);
    }

    public static void OnCardPlayTargetingStarting(Control control)
    {
        if (_session is not { Source: CardPredictionSource.CardPlay } session ||
            !ReferenceEquals(control, session.Holder.CardNode))
        {
            return;
        }

        session.TargetObserver?.Dispose();
        session.Target = null;
        session.Projection = null;
        ApplyProjection(null);
        ClearCardPlayHoverTips(session.Holder);

        var targetObserver = new CombatPredictionTargetObserver(NTargetManager.Instance);
        session.TargetObserver = targetObserver;
        targetObserver.TargetChanged += target => OnCardPlayTargetChanged(session, target);
    }

    public static void OnCardPlayCleanedUp(NHandCardHolder holder)
    {
        ClearCardPlayHoverTips(holder);
        ClearPredictions(CardPredictionSource.CardPlay, holder);
    }

    /// <summary>
    /// Reuses the projection already produced for the active local hand hover.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the active hover session owns this card, including when that session produced no
    /// projection. Callers must not run a fallback simulation in that case.
    /// </returns>
    public static bool TryGetActiveHoverTips(CardModel card, out IReadOnlyList<IHoverTip> hoverTips)
    {
        if (_session is { Source: CardPredictionSource.Hover } session &&
            ReferenceEquals(session.Card, card))
        {
            hoverTips = session.Projection?.HoverTips ?? [];
            return true;
        }

        hoverTips = [];
        return false;
    }

    private static void OnCardPlayTargetChanged(CardPredictionSession session, Creature? target)
    {
        if (!ReferenceEquals(session, _session))
        {
            return;
        }

        session.Target = target;
        if (target is null)
        {
            session.Projection = null;
            ApplyProjection(null);
            ClearCardPlayHoverTips(session.Holder);
        }
        else
        {
            RefreshPrediction(session);
            ShowCardPlayHoverTips(session);
        }
    }

    private static void StartPrediction(CardPredictionSource source, NHandCardHolder holder)
    {
        if (holder.CardModel is not { } card ||
            (source is CardPredictionSource.Hover && _session?.Source is CardPredictionSource.CardPlay))
        {
            return;
        }

        _session?.Dispose();
        _session = new CardPredictionSession
        {
            Source = source,
            Holder = holder,
            Card = card
        };
        RefreshPrediction(_session);
    }

    private static void RefreshPrediction(CardPredictionSession session)
    {
        try
        {
            session.Projection = CombatCardPrediction.Predict(session.Card, session.Target);
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn(
                $"Combat card prediction failed for {session.Card.Id} " +
                $"targeting {session.Target?.Name}: {ex}");
            session.Projection = null;
        }

        ApplyProjection(session.Projection);
    }

    private static void ApplyProjection(CombatCardPredictionProjection? prediction)
    {
        if (prediction is not null)
        {
            ShowDamagePrediction(prediction.DamagePrediction, prediction.Risk);
            CombatCardPredictionHighlight.Show(prediction.HighlightedCards);
        }
        else
        {
            ClearDamagePrediction();
            CombatCardPredictionHighlight.Clear();
        }

        // Card damage predictions share the same display surfaces as end-turn prediction.
        EndTurnPredictionController.SetCardDamageOverride(_hasDamagePrediction);
    }

    private static void ClearPredictions(CardPredictionSource source, NHandCardHolder holder)
    {
        // On a successful play, vanilla reparents the NCard away from the holder before
        // NCardPlay.Cleanup postfix runs, so holder.CardModel may already be null here.
        // The NHandCardHolder reference itself is stable for the play lifecycle.
        if (_session is not { } session || session.Source != source || !ReferenceEquals(session.Holder, holder))
        {
            return;
        }

        _session = null;
        session.Dispose();
        ApplyProjection(null);
    }

    private static void ShowCardPlayHoverTips(CardPredictionSession session)
    {
        var holder = session.Holder;
        ClearCardPlayHoverTips(holder);
        if (session.Projection?.HoverTips is not { Count: > 0 } hoverTips)
        {
            return;
        }

        // NTargetManager blocks normal hover tips while selecting a target. This tooltip is an
        // explicit card-play prediction surface, so temporarily bypass that global block.
        var shouldBlockHoverTips = NHoverTipSet.shouldBlockHoverTips;
        NHoverTipSet.shouldBlockHoverTips = false;
        try
        {
            NHoverTipSet.CreateAndShow(holder, hoverTips)?.SetAlignmentForCardHolder(holder);
        }
        finally
        {
            NHoverTipSet.shouldBlockHoverTips = shouldBlockHoverTips;
        }
    }

    private static void ClearCardPlayHoverTips(NHandCardHolder holder)
    {
        NHoverTipSet.Remove(holder);
    }

    private static void ShowDamagePrediction(DamagePrediction damagePrediction, PredictionRisk risk)
    {
        if (!damagePrediction.HasTargets)
        {
            ClearDamagePrediction();
            return;
        }

        CombatPredictionOverlay.Show(damagePrediction, risk);
        DamagePredictionHealthBarForecast.Set(damagePrediction);
        _hasDamagePrediction = true;
    }

    private static void ClearDamagePrediction()
    {
        if (_hasDamagePrediction)
        {
            CombatPredictionOverlay.Clear();
            DamagePredictionHealthBarForecast.Clear();
            _hasDamagePrediction = false;
        }
    }

    private sealed class CardPredictionSession : IDisposable
    {
        public required CardPredictionSource Source { get; init; }

        public required NHandCardHolder Holder { get; init; }

        public required CardModel Card { get; init; }

        public Creature? Target { get; set; }

        public CombatCardPredictionProjection? Projection { get; set; }

        public CombatPredictionTargetObserver? TargetObserver { get; set; }

        public void Dispose()
        {
            TargetObserver?.Dispose();
            TargetObserver = null;
        }
    }

    private enum CardPredictionSource
    {
        Hover,
        CardPlay
    }
}

[HarmonyPatch(typeof(NHandCardHolder))]
internal static class CombatCardPredictionHandPatches
{
    [HarmonyPatch("DoCardHoverEffects")]
    [HarmonyPrefix]
    private static void UpdatePredictionOnCardHover(NHandCardHolder __instance, bool isHovered)
    {
        CombatCardPredictionController.OnCardHover(__instance, isHovered);
    }
}

[HarmonyPatch(typeof(NPlayerHand))]
internal static class CombatCardPredictionPlayerHandPatches
{
    [HarmonyPatch(nameof(NPlayerHand.StartCardPlay))]
    [HarmonyPrefix]
    private static void UpdatePredictionsOnCardPlayStarted(NHandCardHolder holder)
    {
        CombatCardPredictionController.OnCardPlayStarted(holder);
    }
}

[HarmonyPatch(typeof(NCardPlay))]
internal static class CombatCardPredictionCardPlayPatches
{
    [HarmonyPatch("Cleanup")]
    [HarmonyPostfix]
    private static void CleanupPredictions(NCardPlay __instance)
    {
        CombatCardPredictionController.OnCardPlayCleanedUp(__instance.Holder);
    }
}

[HarmonyPatch(typeof(NTargetManager))]
internal static class CombatCardPredictionTargetManagerPatches
{
    // NTargetManager also has a Vector2 overload for target pickers that only know a
    // screen position. Card play uses the Control overload with the card node, which
    // lets us verify that this targeting session belongs to the active dragged card.
    // This must run as a prefix: StartTargeting synchronously calls OnTargetingStarted
    // on every NCreature, and an already focused creature emits CreatureHovered there.
    // Subscribing in a postfix would miss that initial target event.
    [HarmonyPatch(
        nameof(NTargetManager.StartTargeting),
        [
            typeof(TargetType),
            typeof(Control),
            typeof(TargetMode),
            typeof(Func<bool>),
            typeof(Func<Node, bool>)
        ])]
    [HarmonyPrefix]
    private static void ObservePredictionTargetsBeforeTargetingStarts(Control control)
    {
        CombatCardPredictionController.OnCardPlayTargetingStarting(control);
    }
}
