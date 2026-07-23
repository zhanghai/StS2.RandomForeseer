using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>
/// Collects causal lines for chained results that were accepted by the projector.
/// </summary>
/// <remarks>
/// Consecutive results are grouped by immutable trace frames and effect kind, so separate card replays remain
/// distinct and non-consecutive timeline effects are not reordered. The builder receives prediction model snapshots
/// and creates one localized text tip during projection finalization.
/// </remarks>
internal sealed class CombatCardPredictionCausalTipBuilder(PredictionTraceFrame rootFrame)
{
    private const int MaxLines = 10;

    private readonly List<CausalGroup> _groups = [];

    /// <summary>
    /// Adds one accepted semantic result in projector dispatch order.
    /// </summary>
    /// <remarks>
    /// The entry must be the started/semantic entry rather than a deferred resolution entry so grouping follows the
    /// order perceived by the player. Models should be the snapshots used by the corresponding presentation result.
    /// </remarks>
    public void AddEffect(
        CombatPredictionHistoryEntry entry,
        CausalEffectKind effect,
        IEnumerable<AbstractModel> models)
    {
        if (TryGetCause(entry.Trace) is not { } cause)
        {
            return;
        }

        AddGroup(cause, effect, models);
    }

    /// <summary>
    /// Finalizes the accumulated groups into one localized causal HoverTip.
    /// </summary>
    /// <returns>
    /// A causal tip when accepted results include a non-root cause, otherwise <see langword="null"/>.
    /// </returns>
    /// <remarks>This method must be called only after all relevant history entries have been added in timeline order.</remarks>
    public IHoverTip? Build()
    {
        if (_groups.Count == 0 ||
            _groups.All(group => group.SourceFrame.Parent == rootFrame && group.ListenerFrame is null))
        {
            return null;
        }

        var lines = _groups.Take(MaxLines).Select(FormatGroup).ToList();

        if (_groups.Count > MaxLines)
        {
            var more = PredictionLocalization.Text("causal_prediction.more");
            more.Add("Count", _groups.Count - MaxLines);
            lines.Add(more.GetFormattedText());
        }

        return PredictionHoverTipFactory.Text("causal_prediction", description =>
        {
            description.Add("Lines", lines);
        });
    }

    private static CausalCause? TryGetCause(PredictionTraceFrame? trace)
    {
        if (trace?.FindOriginatingCardPlay() is not { Source: CardModel sourceCard } sourceFrame)
        {
            return null;
        }

        var listenerFrame = trace.Source == sourceCard ? null : trace;
        return new CausalCause(sourceFrame, listenerFrame, sourceCard);
    }

    private void AddGroup(CausalCause cause, CausalEffectKind effect, IEnumerable<AbstractModel> models)
    {
        CausalGroup group;
        if (_groups is [.., var last] && last.Cause == cause && last.Effect == effect)
        {
            group = _groups[^1];
        }
        else
        {
            group = new CausalGroup(cause, effect);
            _groups.Add(group);
        }

        group.Models.AddRange(models);
    }

    private string FormatGroup(CausalGroup group)
    {
        var line = PredictionLocalization.Text($"causal_prediction.{GetEffectKey(group.Effect)}");
        line.Add("Source", group.SourceCard.Title);
        line.Add("Listener", group.Listener?.GetTitle() ?? string.Empty);
        line.Add("Models", [.. group.Models.Select(static model => model.GetTitle())]);
        return line.GetFormattedText();
    }

    private static string GetEffectKey(CausalEffectKind effect)
    {
        return effect switch
        {
            CausalEffectKind.ChannelOrbs => "channel_orbs",
            CausalEffectKind.DrawCards => "draw_cards",
            CausalEffectKind.GenerateCards => "generate_cards",
            CausalEffectKind.GeneratePotion => "generate_potion",
            CausalEffectKind.PlayCard => "play_card",
            CausalEffectKind.SelectCards => "select_cards",
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null)
        };
    }

    private sealed class CausalGroup(CausalCause cause, CausalEffectKind effect)
    {
        public CausalCause Cause { get; } = cause;

        public CausalEffectKind Effect { get; } = effect;

        public List<AbstractModel> Models { get; } = [];

        public PredictionTraceFrame SourceFrame => Cause.SourceFrame;

        public PredictionTraceFrame? ListenerFrame => Cause.ListenerFrame;

        public CardModel SourceCard => Cause.SourceCard;

        public AbstractModel? Listener => Cause.Listener;
    }

    private readonly record struct CausalCause(
        PredictionTraceFrame SourceFrame,
        PredictionTraceFrame? ListenerFrame,
        CardModel SourceCard)
    {
        public AbstractModel? Listener => ListenerFrame?.Source;
    }
}

/// <summary>
/// Semantic result kinds that can be described by the chained-effect projection.
/// </summary>
internal enum CausalEffectKind
{
    ChannelOrbs,
    DrawCards,
    GenerateCards,
    GeneratePotion,
    PlayCard,
    SelectCards
}
