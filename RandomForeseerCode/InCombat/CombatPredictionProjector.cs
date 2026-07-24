using System.Diagnostics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>
/// Contains the presentation payload produced from one unified combat-action simulation.
/// </summary>
/// <param name="HoverTips">Ordered prediction tips before the game separates text and card containers.</param>
/// <param name="DamagePrediction">Damage overlay and health-bar payload accepted by damage-specific gates.</param>
/// <param name="HighlightedCards">Original live cards selected by accepted card-selection entries.</param>
/// <param name="Risk">Shared risk accumulated through the completion boundaries of accepted results.</param>
internal sealed record CombatPredictionProjection(
    IReadOnlyList<IHoverTip> HoverTips,
    DamagePrediction DamagePrediction,
    IReadOnlySet<CardModel> HighlightedCards,
    PredictionRisk Risk);

/// <summary>
/// Streams a completed combat prediction history into the enabled HoverTip, damage, highlight, causal, and risk views.
/// </summary>
/// <remarks>
/// Projection follows history order until a disabled state-changing result, or the first nested action while chained
/// prediction is disabled, truncates the remaining visible timeline.
/// </remarks>
internal sealed class CombatPredictionProjector
{
    private const int MaxHoverTips = 10;

    private readonly CombatPredictionHistory _history;
    private readonly PredictionTraceFrame _rootFrame;
    private readonly PredictionActionKind _rootAction;

    private readonly Dictionary<Type, EntryProjectionRule> _rules = [];

    private readonly List<IHoverTip> _hoverTips = [];
    private readonly HashSet<CardModel> _highlightedCards = [];
    private readonly List<CombatPredictionDamageReceivedEntry> _damageEntries = [];
    private readonly List<CombatPredictionHistoryEntry> _relevantEntries = [];

    // Once a hidden result can affect later history, no later entry may contribute to the projection.
    private bool _projectionTruncated;

    private readonly CombatPredictionCausalTipBuilder _causalTips;

    /// <summary>
    /// Returns whether at least one category reachable from the specified root action is currently enabled.
    /// </summary>
    /// <param name="action">The card-play or potion-use root action whose source-specific settings should be checked.</param>
    /// <remarks>The result includes the current single-player or multiplayer prediction master gate.</remarks>
    public static bool HasAnyEnabledFeature(PredictionActionKind action)
    {
        var enabled = action switch
        {
            PredictionActionKind.CardPlay =>
                RandomForeseerSettings.EnableCombatCardPrediction ||
                RandomForeseerSettings.EnablePotionGenerationPrediction ||
                RandomForeseerSettings.EnableCombatCardSelectionPrediction ||
                RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction ||
                RandomForeseerSettings.EnableCardDrawPrediction ||
                RandomForeseerSettings.EnableOrbPrediction ||
                RandomForeseerSettings.EnableCombatDamagePrediction,

            PredictionActionKind.PotionUse =>
                RandomForeseerSettings.EnablePotionCardPrediction ||
                RandomForeseerSettings.EnablePotionGenerationPrediction ||
                RandomForeseerSettings.EnableCombatCardSelectionPrediction ||
                RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction ||
                RandomForeseerSettings.EnablePotionDrawPrediction ||
                RandomForeseerSettings.EnableOrbPrediction ||
                RandomForeseerSettings.EnableCombatDamagePrediction,

            _ => false
        };

        return IsSettingEnabled(enabled);
    }

    /// <summary>
    /// Projects one completed combat-action history relative to its exact root action frame.
    /// </summary>
    /// <param name="history">The completed history produced by the same simulation as <paramref name="rootFrame"/>.</param>
    /// <param name="rootFrame">
    /// The root <see cref="PredictionActionKind.CardPlay"/> or <see cref="PredictionActionKind.PotionUse"/> frame.
    /// Its source is the original card or potion identity used by scope and causal classification.
    /// </param>
    /// <returns>A presentation projection containing only results accepted by enabled feature policies.</returns>
    /// <remarks>
    /// Callers must pass an action frame returned by the simulation entry point and must not combine a frame and
    /// history from different simulations. Entries are dispatched in timeline order; deferred entries use their
    /// resolved snapshot and completion boundary.
    /// </remarks>
    public static CombatPredictionProjection Project(CombatPredictionHistory history, PredictionTraceFrame rootFrame)
    {
        return new CombatPredictionProjector(history, rootFrame).Project();
    }

    private CombatPredictionProjector(CombatPredictionHistory history, PredictionTraceFrame rootFrame)
    {
        var action = rootFrame.Invocation.Action;
        if (action is not (PredictionActionKind.CardPlay or PredictionActionKind.PotionUse))
        {
            throw new ArgumentException(
                $"Root frame must be a card-play or potion-use action, but was {action}.",
                nameof(rootFrame));
        }

        _history = history;
        _rootFrame = rootFrame;
        _rootAction = action.Value;

        _causalTips = new(rootFrame);

        RegisterRules();
    }

    private CombatPredictionProjection Project()
    {
        foreach (var entry in _history.Entries)
        {
            if (_projectionTruncated)
            {
                break;
            }

            Dispatch(entry);
        }

        return FinalizeProjection();
    }

    /// <summary>
    /// Declares the single projection rule for each supported semantic history entry type.
    /// </summary>
    /// <remarks>
    /// A disabled result truncates later projection only when its entry-specific policy says the hidden outcome can
    /// reveal downstream state. Choice options, fixed/contextual card generation, generated potions, orb results, and
    /// damage results do not establish a general truncation boundary.
    /// </remarks>
    private void RegisterRules()
    {
        Register<CombatPredictionCardGeneratedEntry>(
            HandleCardGenerated,
            cardSetting: RandomForeseerSettings.EnableCombatCardPrediction,
            potionSetting: RandomForeseerSettings.EnablePotionCardPrediction,
            shouldTruncateWhenDisabled: static entry => entry.ResultKind is CardGenerationResultKind.Random);

        Register<CombatPredictionCardGenerationOptionsEntry>(
            HandleCardGenerationOptions,
            cardSetting: RandomForeseerSettings.EnableCombatCardPrediction,
            potionSetting: RandomForeseerSettings.EnablePotionCardPrediction);

        Register<CombatPredictionPotionGeneratedEntry>(
            HandlePotionGenerated,
            sharedSetting: RandomForeseerSettings.EnablePotionGenerationPrediction);

        Register<CombatPredictionCardsSelectedEntry>(
            HandleCardsSelected,
            sharedSetting: RandomForeseerSettings.EnableCombatCardSelectionPrediction,
            shouldTruncateWhenDisabled: static entry => entry.Cards.Count > 0);

        Register<CombatPredictionAutoPlayFromDrawPileEntry>(
            HandleDrawPileAutoPlay,
            sharedSetting: RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction,
            shouldTruncateWhenDisabled: static _ => true);

        Register<CombatPredictionCardDrawnEntry>(
            HandleCardDrawn,
            cardSetting: RandomForeseerSettings.EnableCardDrawPrediction,
            potionSetting: RandomForeseerSettings.EnablePotionDrawPrediction,
            shouldTruncateWhenDisabled: static _ => true);

        Register<CombatPredictionCardCostsRandomizedEntry>(
            HandleCardCostsRandomized,
            cardSetting: RandomForeseerSettings.EnableCardDrawPrediction,
            potionSetting: RandomForeseerSettings.EnablePotionDrawPrediction,
            shouldTruncateWhenDisabled: static entry => entry.Cards.Count > 0);

        Register<CombatPredictionOrbChanneledEntry>(
            HandleOrbChanneled,
            sharedSetting: RandomForeseerSettings.EnableOrbPrediction);

        Register<CombatPredictionDamageReceivedEntry>(
            HandleDamage,
            sharedSetting: RandomForeseerSettings.EnableCombatDamagePrediction);
    }

    private void Register<TEntry>(
        Func<TEntry, CombatPredictionHistoryEntry?> handler,
        bool? cardSetting = null,
        bool? potionSetting = null,
        bool? sharedSetting = null,
        Predicate<TEntry>? shouldTruncateWhenDisabled = null)
        where TEntry : CombatPredictionHistoryEntry
    {
        // A shared setting supplies both action gates; an explicit action setting overrides it. Any omitted gate is
        // treated as enabled, so every registration must deliberately provide the gates relevant to its source kinds.
        _rules.Add(typeof(TEntry), new EntryProjectionRule(
            entry => handler((TEntry)entry),
            cardSetting ?? sharedSetting ?? true,
            potionSetting ?? sharedSetting ?? true,
            entry => shouldTruncateWhenDisabled?.Invoke((TEntry)entry) ?? false));
    }

    /// <summary>
    /// Applies the exact-type projection rule selected by the entry's nearest card-play or potion-use action.
    /// </summary>
    /// <remarks>
    /// Entries outside the supplied root trace are ignored. When chained prediction is disabled, the first entry owned
    /// by a nested action truncates the remaining timeline even if that entry type has no projection rule.
    /// </remarks>
    private void Dispatch(CombatPredictionHistoryEntry entry)
    {
        if (entry.Trace?.FindOriginatingAction() is not { } actionFrame ||
            !actionFrame.Ancestors().Contains(_rootFrame))
        {
            return;
        }

        if (!IsSettingEnabled(RandomForeseerSettings.EnableChainedCardEffectPrediction) &&
            actionFrame != _rootFrame)
        {
            _projectionTruncated = true;
            return;
        }

        if (!_rules.TryGetValue(entry.GetType(), out var rule))
        {
            return;
        }

        var setting = actionFrame.Invocation.Action switch
        {
            PredictionActionKind.CardPlay => rule.CardSetting,
            PredictionActionKind.PotionUse => rule.PotionSetting,
            var action => throw new UnreachableException($"Unexpected action kind {action}.")
        };
        if (!IsSettingEnabled(setting))
        {
            _projectionTruncated |= rule.ShouldTruncateWhenDisabled(entry);
            return;
        }

        if (rule.Handler(entry) is { } relevantEntry)
        {
            _relevantEntries.Add(relevantEntry);
        }
    }

    private CombatPredictionHistoryEntry? HandleCardGenerated(CombatPredictionCardGeneratedEntry entry)
    {
        if (entry.ResultKind is CardGenerationResultKind.Fixed)
        {
            return null;
        }

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

    private CombatPredictionHistoryEntry? HandleCardCostsRandomized(CombatPredictionCardCostsRandomizedEntry entry)
    {
        if (entry.Cards.Count == 0)
        {
            return null;
        }

        // Snecko Oil's final full-hand snapshot supersedes the draw tips recorded before cost randomization.
        _hoverTips.Clear();
        _hoverTips.AddRange(entry.Cards.SelectPreviews().ToPredictionHoverTips());
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleOrbChanneled(CombatPredictionOrbChanneledEntry entry)
    {
        // Deterministic channels are already described by their source; only random orb identities need a tip.
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
        if (!IsSettingEnabled(RandomForeseerSettings.EnableOrbPrediction) &&
            entry.Trace!.Ancestors().Any(static frame => frame.Source is OrbModel))
        {
            return null;
        }

        if (!IsSettingEnabled(RandomForeseerSettings.EnableRandomTargetAttackPrediction) &&
            entry.Trace!.Ancestors().Any(static frame =>
                frame.Source is CardModel { Type: CardType.Attack, TargetType: TargetType.RandomEnemy }))
        {
            return null;
        }

        _damageEntries.Add(entry);
        return entry;
    }

    private CombatPredictionProjection FinalizeProjection()
    {
        var damagePrediction = DamagePredictionProjector.Project(_damageEntries);
        var risk = _history.GetRisk(_relevantEntries);
        if (_causalTips.Build() is { } causalTip)
        {
            _hoverTips.Insert(0, causalTip);
        }
        _hoverTips.AddDriftWarning(GetDriftWarningKey(), risk);

        return new CombatPredictionProjection(
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

    private string GetDriftWarningKey()
    {
        return _rootAction switch
        {
            PredictionActionKind.CardPlay => "combat_card",
            PredictionActionKind.PotionUse => "combat_potion",
            var action => throw new InvalidOperationException($"Unexpected root action kind {action}.")
        };
    }

    private static bool IsSettingEnabled(bool setting)
    {
        return RandomForeseerSettings.IsPredictionFeatureEnabled(setting);
    }

    private delegate CombatPredictionHistoryEntry? EntryHandler(CombatPredictionHistoryEntry entry);

    /// <summary>
    /// Couples one exact history-entry handler with its root-action gates and hidden-result truncation policy.
    /// </summary>
    /// <param name="Handler">Projects an enabled entry and returns the history boundary consumed for shared risk.</param>
    /// <param name="CardSetting">The feature setting used when the nearest action frame is a card play.</param>
    /// <param name="PotionSetting">The feature setting used when the nearest action frame is a potion use.</param>
    /// <param name="ShouldTruncateWhenDisabled">
    /// Determines from the concrete entry whether hiding it also hides the remaining timeline.
    /// </param>
    private readonly record struct EntryProjectionRule(
        EntryHandler Handler,
        bool CardSetting,
        bool PotionSetting,
        Predicate<CombatPredictionHistoryEntry> ShouldTruncateWhenDisabled);
}
