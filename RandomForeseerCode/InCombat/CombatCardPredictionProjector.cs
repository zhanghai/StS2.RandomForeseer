using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>
/// Contains the presentation payload produced from one unified combat-card simulation.
/// </summary>
internal sealed record CombatCardPredictionProjection(
    IReadOnlyList<IHoverTip> HoverTips,
    DamagePrediction DamagePrediction,
    IReadOnlySet<CardModel> HighlightedCards,
    PredictionRisk Risk);

/// <summary>
/// Streams a completed combat prediction history into the enabled HoverTip, damage, highlight, causal, and risk views.
/// </summary>
internal sealed class CombatCardPredictionProjector
{
    private const int MaxHoverTips = 10;

    private readonly CombatPredictionHistory _history;
    private readonly CardModel _rootCard;
    private readonly PredictionTraceFrame _rootFrame;

    private readonly Dictionary<Type, List<EntryHandler>> _handlers = [];

    private readonly List<IHoverTip> _hoverTips = [];
    private readonly HashSet<CardModel> _highlightedCards = [];
    private readonly List<CombatPredictionDamageReceivedEntry> _damageEntries = [];
    private readonly List<CombatPredictionHistoryEntry> _relevantEntries = [];

    private readonly CombatCardPredictionCausalTipBuilder _causalTips;

    /// <summary>
    /// Returns whether at least one category consumed by the unified card-play projector is currently enabled.
    /// </summary>
    public static bool HasEnabledFeature()
    {
        return IsFeatureEnabled(RandomForeseerSettings.EnableCombatCardPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableCombatCardSelectionPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableCardDrawPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableCombatDamagePrediction);
    }

    /// <summary>
    /// Projects one completed card-play history relative to its exact root lifecycle frame.
    /// </summary>
    /// <param name="history">The completed history produced by the same simulation as <paramref name="rootFrame"/>.</param>
    /// <param name="rootCard">The live original card used as the root source identity.</param>
    /// <param name="rootFrame">The root <see cref="PredictionActionKind.CardPlayLifecycle"/> frame returned by manual play.</param>
    /// <returns>A presentation projection containing only results accepted by enabled feature policies.</returns>
    /// <remarks>
    /// Callers must not combine a frame and history from different simulations. Entries are dispatched in timeline
    /// order; deferred entries use their resolved snapshot and completion boundary.
    /// </remarks>
    public static CombatCardPredictionProjection Project(
        CombatPredictionHistory history,
        CardModel rootCard,
        PredictionTraceFrame rootFrame)
    {
        return new CombatCardPredictionProjector(history, rootCard, rootFrame).Project();
    }

    private CombatCardPredictionProjector(
        CombatPredictionHistory history,
        CardModel rootCard,
        PredictionTraceFrame rootFrame)
    {
        _history = history;
        _rootCard = rootCard;
        _rootFrame = rootFrame;

        _causalTips = new(rootFrame);

        RegisterHandlers();
    }

    private CombatCardPredictionProjection Project()
    {
        foreach (var entry in _history.Entries)
        {
            Dispatch(entry);
        }

        return FinalizeProjection();
    }

    private void RegisterHandlers()
    {
        if (IsFeatureEnabled(RandomForeseerSettings.EnableCombatCardPrediction))
        {
            Register<CombatPredictionCardGeneratedEntry>(HandleCardGenerated);
            Register<CombatPredictionCardGenerationOptionsEntry>(HandleCardGenerationOptions);
        }

        if (IsFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction))
        {
            Register<CombatPredictionPotionGeneratedEntry>(HandlePotionGenerated);
        }

        if (IsFeatureEnabled(RandomForeseerSettings.EnableCombatCardSelectionPrediction))
        {
            Register<CombatPredictionCardsSelectedEntry>(HandleCardsSelected);
        }

        if (IsFeatureEnabled(RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction))
        {
            Register<CombatPredictionAutoPlayFromDrawPileEntry>(HandleDrawPileAutoPlay);
        }

        if (IsFeatureEnabled(RandomForeseerSettings.EnableCardDrawPrediction))
        {
            Register<CombatPredictionCardDrawnEntry>(HandleCardDrawn);
        }

        if (IsFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction))
        {
            Register<CombatPredictionOrbChanneledEntry>(HandleOrbChanneled);
        }

        if (IsFeatureEnabled(RandomForeseerSettings.EnableCombatDamagePrediction))
        {
            Register<CombatPredictionDamageReceivedEntry>(HandleDamage);
        }
    }

    private void Register<TEntry>(Func<TEntry, CombatPredictionHistoryEntry?> handler)
        where TEntry : CombatPredictionHistoryEntry
    {
        if (!_handlers.TryGetValue(typeof(TEntry), out var handlers))
        {
            handlers = [];
            _handlers.Add(typeof(TEntry), handlers);
        }

        handlers.Add(entry => handler((TEntry)entry));
    }

    private void Dispatch(CombatPredictionHistoryEntry entry)
    {
        if (!_handlers.TryGetValue(entry.GetType(), out var handlers) || !ShouldDispatch(entry))
        {
            return;
        }

        foreach (var handler in handlers)
        {
            if (handler(entry) is { } relevantEntry)
            {
                _relevantEntries.Add(relevantEntry);
            }
        }
    }

    private bool ShouldDispatch(CombatPredictionHistoryEntry entry)
    {
        return GetProjectionScope(entry) switch
        {
            ProjectionScope.Direct or ProjectionScope.Indirect => true,
            ProjectionScope.Chained => IsChainedPredictionEnabled(),
            _ => false
        };
    }

    private CombatPredictionHistoryEntry? HandleCardGenerated(CombatPredictionCardGeneratedEntry entry)
    {
        var resolved = _history.GetResolvedEntry<CombatPredictionCardGenerationResolvedEntry>(entry);
        AddHoverTip(PredictionHoverTipFactory.Card(resolved.Card.Preview));
        AddCausalEffect(entry, CausalEffectKind.GenerateCards, [resolved.Card.Preview]);
        return resolved;
    }

    private CombatPredictionHistoryEntry? HandleCardGenerationOptions(CombatPredictionCardGenerationOptionsEntry entry)
    {
        if (entry.Cards.Count == 0)
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.CardBundle([.. entry.Cards.SelectPreviews()]));
        AddCausalEffect(entry, CausalEffectKind.GenerateCards, entry.Cards.SelectPreviews());
        return entry;
    }

    private CombatPredictionHistoryEntry? HandlePotionGenerated(CombatPredictionPotionGeneratedEntry entry)
    {
        AddHoverTip(PredictionHoverTipFactory.Potion(entry.Potion));
        AddCausalEffect(entry, CausalEffectKind.GeneratePotion, [entry.Potion]);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleCardsSelected(CombatPredictionCardsSelectedEntry entry)
    {
        if (entry.Cards.Count == 0)
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.CardBundle([.. entry.Cards.SelectPreviews()]));
        _highlightedCards.UnionWith(entry.Cards.SelectOriginals());
        AddCausalEffect(entry, CausalEffectKind.SelectCards, entry.Cards.SelectPreviews());
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleDrawPileAutoPlay(CombatPredictionAutoPlayFromDrawPileEntry entry)
    {
        AddHoverTip(PredictionHoverTipFactory.Card(entry.Card.Preview));
        AddCausalEffect(entry, CausalEffectKind.PlayCard, [entry.Card.Preview]);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleCardDrawn(CombatPredictionCardDrawnEntry entry)
    {
        var resolved = _history.GetResolvedEntry<CombatPredictionCardDrawResolvedEntry>(entry);
        AddHoverTip(PredictionHoverTipFactory.Card(resolved.Card.Preview));
        AddCausalEffect(entry, CausalEffectKind.DrawCards, [resolved.Card.Preview]);
        return resolved;
    }

    private CombatPredictionHistoryEntry? HandleOrbChanneled(CombatPredictionOrbChanneledEntry entry)
    {
        if (entry.Trace!.Source is not (Chaos or TrashToTreasurePower))
        {
            return null;
        }

        AddHoverTip(PredictionHoverTipFactory.Orb(entry.Orb));
        AddCausalEffect(entry, CausalEffectKind.ChannelOrbs, [entry.Orb]);
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleDamage(CombatPredictionDamageReceivedEntry entry)
    {
        if (!IsFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) &&
            entry.Trace!.Ancestors().Any(static frame => frame.Source is OrbModel))
        {
            return null;
        }

        if (!IsFeatureEnabled(RandomForeseerSettings.EnableRandomTargetAttackPrediction) &&
            entry.Trace!.Ancestors().Any(static frame => IsRandomTargetAttackCard(frame.Source)))
        {
            return null;
        }

        _damageEntries.Add(entry);
        return entry;
    }

    private CombatCardPredictionProjection FinalizeProjection()
    {
        var damagePrediction = DamagePredictionProjector.Project(_damageEntries);
        var risk = _history.GetRisk(_relevantEntries);
        if (_causalTips.Build() is { } causalTip)
        {
            _hoverTips.Insert(0, causalTip);
        }
        _hoverTips.AddDriftWarning("combat_card", risk);

        return new CombatCardPredictionProjection(
            _hoverTips,
            damagePrediction,
            _highlightedCards,
            risk);
    }

    private void AddHoverTip(IHoverTip hoverTip)
    {
        if (_hoverTips.Count < MaxHoverTips)
        {
            _hoverTips.Add(hoverTip);
        }
    }

    private void AddCausalEffect(
        CombatPredictionHistoryEntry entry,
        CausalEffectKind effect,
        IEnumerable<AbstractModel> results)
    {
        _causalTips.AddEffect(entry, effect, results);
    }

    private ProjectionScope GetProjectionScope(CombatPredictionHistoryEntry entry)
    {
        if (entry.Trace is not { } trace ||
            trace.FindOriginatingCardPlay() is not { } cardPlayFrame ||
            !cardPlayFrame.Ancestors().Contains(_rootFrame))
        {
            return ProjectionScope.None;
        }

        if (cardPlayFrame.Parent == _rootFrame && cardPlayFrame.Source == _rootCard)
        {
            return trace.Source == _rootCard
                ? ProjectionScope.Direct
                : ProjectionScope.Indirect;
        }

        return ProjectionScope.Chained;
    }

    private static bool IsFeatureEnabled(bool setting)
    {
        return RandomForeseerSettings.IsPredictionFeatureEnabled(setting);
    }

    private static bool IsChainedPredictionEnabled()
    {
        return IsFeatureEnabled(RandomForeseerSettings.EnableChainedCardEffectPrediction);
    }

    private static bool IsRandomTargetAttackCard(AbstractModel model)
    {
        return model is
            FlakCannon or
            Ricochet or
            RipAndTear or
            Stardust or
            SweepingGaze or
            SwordBoomerang or
            Volley;
    }

    private delegate CombatPredictionHistoryEntry? EntryHandler(CombatPredictionHistoryEntry entry);

    private enum ProjectionScope
    {
        None,
        Direct,
        Indirect,
        Chained
    }
}
