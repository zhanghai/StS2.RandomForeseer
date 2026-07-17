using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed class CombatCardPredictionProjector(
    CardModel rootCard,
    CombatPredictionSimulator simulator)
{
    private readonly Dictionary<Type, List<EntryHandler>> _handlers = [];
    private readonly List<IHoverTip> _hoverTips = [];
    private readonly List<CardModel> _highlightedCards = [];
    private readonly List<CombatPredictionHistoryEntry> _relevantEntries = [];
    private readonly List<CombatPredictionDamageReceivedEntry> _damageEntries = [];

    public static bool HasEnabledFeature()
    {
        return IsFeatureEnabled(RandomForeseerSettings.EnableCombatCardPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnablePotionGenerationPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableCombatCardSelectionPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableAutoPlayFromDrawPilePrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableCardDrawPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableRandomTargetAttackPrediction);
    }

    public CombatCardPredictionProjection Project()
    {
        RegisterHandlers();

        foreach (var entry in simulator.History.Entries)
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
            Register<CombatPredictionOrbChanneledEntry>(HandleRandomOrb);
        }

        if (IsFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) ||
            IsFeatureEnabled(RandomForeseerSettings.EnableRandomTargetAttackPrediction))
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
        if (!_handlers.TryGetValue(entry.GetType(), out var handlers))
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

    private CombatPredictionHistoryEntry? HandleCardGenerated(CombatPredictionCardGeneratedEntry entry)
    {
        if (!IsImmediateRootResult(entry))
        {
            return null;
        }

        var resolved = simulator.History.GetResolvedEntry<CombatPredictionCardGenerationResolvedEntry>(entry);
        _hoverTips.Add(PredictionHoverTipFactory.Card(resolved.Card.Preview));
        return resolved;
    }

    private CombatPredictionHistoryEntry? HandleCardGenerationOptions(CombatPredictionCardGenerationOptionsEntry entry)
    {
        if (!IsImmediateRootResult(entry) || entry.Cards.Count == 0)
        {
            return null;
        }

        _hoverTips.Add(PredictionHoverTipFactory.CardBundle(
            [.. entry.Cards.Select(static card => card.Preview)],
            PredictionCardBundleKind.Regular));
        return entry;
    }

    private CombatPredictionHistoryEntry? HandlePotionGenerated(CombatPredictionPotionGeneratedEntry entry)
    {
        if (!IsImmediateRootResult(entry))
        {
            return null;
        }

        _hoverTips.Add(PredictionHoverTipFactory.Potion(entry.Potion));
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleCardsSelected(CombatPredictionCardsSelectedEntry entry)
    {
        if (!IsImmediateRootResult(entry) || entry.Cards.Count == 0)
        {
            return null;
        }

        _hoverTips.Add(PredictionHoverTipFactory.CardBundle(
            [.. entry.Cards.Select(static card => card.Preview)],
            PredictionCardBundleKind.Regular));
        _highlightedCards.AddRange(entry.Cards.Select(static card => card.Original));
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleDrawPileAutoPlay(CombatPredictionAutoPlayFromDrawPileEntry entry)
    {
        if (!IsImmediateRootResult(entry))
        {
            return null;
        }

        _hoverTips.Add(PredictionHoverTipFactory.Card(entry.Card.Preview));
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleCardDrawn(CombatPredictionCardDrawnEntry entry)
    {
        if (!IsImmediateRootResult(entry))
        {
            return null;
        }

        var resolved = simulator.History.GetResolvedEntry<CombatPredictionCardDrawResolvedEntry>(entry);
        _hoverTips.Add(PredictionHoverTipFactory.Card(resolved.Card.Preview));
        return resolved;
    }

    private CombatPredictionHistoryEntry? HandleRandomOrb(CombatPredictionOrbChanneledEntry entry)
    {
        if (!IsImmediateRootResult(entry) || rootCard is not Chaos)
        {
            return null;
        }

        _hoverTips.Add(PredictionHoverTipFactory.Orb(entry.Orb));
        return entry;
    }

    private CombatPredictionHistoryEntry? HandleDamage(CombatPredictionDamageReceivedEntry entry)
    {
        if (FindOriginatingCardPlay(entry)?.Source is not CardModel originatingCard ||
            !ReferenceEquals(originatingCard, rootCard) ||
            !(IsFeatureEnabled(RandomForeseerSettings.EnableOrbPrediction) && IsOrbCard(originatingCard)) &&
            !(IsFeatureEnabled(RandomForeseerSettings.EnableRandomTargetAttackPrediction) && IsRandomTargetAttackCard(originatingCard)))
        {
            return null;
        }

        _damageEntries.Add(entry);
        return entry;
    }

    private CombatCardPredictionProjection FinalizeProjection()
    {
        var damagePrediction = DamagePredictionProjector.FromHistory(_damageEntries);
        var risk = simulator.History.GetRiskThrough(_relevantEntries.Max());
        _hoverTips.AddDriftWarning("combat_card", risk);

        return new CombatCardPredictionProjection(
            _hoverTips,
            damagePrediction,
            _highlightedCards,
            risk);
    }

    private bool IsImmediateRootResult(CombatPredictionHistoryEntry entry)
    {
        return ReferenceEquals(entry.Trace?.Source, rootCard);
    }

    private static PredictionTraceFrame? FindOriginatingCardPlay(CombatPredictionHistoryEntry entry)
    {
        return entry.Trace?.Ancestors()
            .FirstOrDefault(static frame => CardOnPlayMirrors.IsOnPlayInvocation(frame.Invocation));
    }

    private static bool IsFeatureEnabled(bool setting)
    {
        return RandomForeseerSettings.IsPredictionFeatureEnabled(setting);
    }

    private static bool IsRandomTargetAttackCard(CardModel card)
    {
        return card is
            FlakCannon or
            Ricochet or
            RipAndTear or
            Stardust or
            SweepingGaze or
            SwordBoomerang or
            Volley;
    }

    private static bool IsOrbCard(CardModel card)
    {
        return card is
            BallLightning or
            Chaos or
            Chill or
            ColdSnap or
            ConsumingShadow or
            Coolheaded or
            Darkness or
            Dualcast or
            Fusion or
            Glacier or
            Glasswork or
            IceLance or
            Ignition or
            MeteorStrike or
            MultiCast or
            Null or
            Quadcast or
            Rainbow or
            Refract or
            ShadowShield or
            Shatter or
            Spinner { IsUpgraded: true } or
            Tempest or
            TeslaCoil or
            Voltaic or
            Zap;
    }

    private delegate CombatPredictionHistoryEntry? EntryHandler(CombatPredictionHistoryEntry entry);
}
