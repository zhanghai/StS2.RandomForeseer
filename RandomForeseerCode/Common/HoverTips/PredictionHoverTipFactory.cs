using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common.HoverTips;

/// <summary>
/// Creates individual prediction HoverTips without advancing RNG or changing game model state.
/// </summary>
/// <remarks>
/// Model-based methods accept either canonical or prediction/mutable models as supplied by the caller. The factory
/// does not clone or convert the model; callers must choose the model source appropriate for the prediction semantics.
/// </remarks>
internal static class PredictionHoverTipFactory
{
    /// <summary>
    /// Prefix used to identify prediction-owned text and model HoverTips.
    /// </summary>
    public const string HoverTipIdPrefix = $"{Entry.ModId}:Prediction";

    private const int MaxDriftWarningModelNames = 3;

    /// <summary>
    /// Creates a non-dimmed prediction tip for one card.
    /// </summary>
    public static IHoverTip Card(CardModel card)
    {
        return Card(card, isDimmed: false);
    }

    /// <summary>
    /// Creates a prediction tip for one card, optionally dimmed for transform-result presentation.
    /// </summary>
    public static IHoverTip Card(CardModel card, bool isDimmed)
    {
        return new PredictionCardHoverTip(card, isDimmed);
    }

    /// <summary>
    /// Creates one logical prediction bundle tip from a non-empty card list.
    /// </summary>
    /// <remarks>
    /// <paramref name="cards"/> must be in prediction/semantic order and must contain at least one card. Do not
    /// reverse the list for visual stacking; <paramref name="kind"/> is consumed by the presentation layer.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="cards"/> is empty.</exception>
    public static IHoverTip CardBundle(IReadOnlyList<CardModel> cards, PredictionCardBundleKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfZero(cards.Count);
        return new PredictionCardBundleHoverTip(cards, kind);
    }

    /// <summary>
    /// Creates a prediction tip from a potion's HoverTip.
    /// </summary>
    /// <remarks>
    /// The returned HoverTip is marked instanced and detached from its canonical model so prediction display cannot
    /// mark the potion as discovered.
    /// </remarks>
    public static IHoverTip Potion(PotionModel potion)
    {
        return FromModelHoverTip(potion.HoverTip);
    }

    /// <summary>
    /// Creates a prediction tip from a relic's HoverTip.
    /// </summary>
    /// <remarks>
    /// The returned HoverTip is marked instanced and detached from its canonical model so prediction display cannot
    /// mark the relic as discovered.
    /// </remarks>
    public static IHoverTip Relic(RelicModel relic)
    {
        return FromModelHoverTip(relic.HoverTip);
    }

    /// <summary>
    /// Creates a prediction tip from an orb's dumb HoverTip.
    /// </summary>
    /// <remarks>
    /// The returned HoverTip is marked instanced and detached from its canonical model.
    /// </remarks>
    public static IHoverTip Orb(OrbModel orb)
    {
        return FromModelHoverTip(orb.DumbHoverTip);
    }

    /// <summary>
    /// Creates a localized, instanced prediction text tip.
    /// </summary>
    /// <param name="key">Localization key stem with <c>.title</c> and <c>.description</c> entries.</param>
    /// <param name="configureDescription">Optional callback to add formatter variables before display.</param>
    public static HoverTip Text(string key, Action<LocString>? configureDescription = null)
    {
        var title = PredictionLocalization.Text($"{key}.title");
        var description = PredictionLocalization.Text($"{key}.description");
        configureDescription?.Invoke(description);

        var tip = new HoverTip(title, description)
        {
            Id = $"{HoverTipIdPrefix}:{key}",
            IsInstanced = true
        };
        return tip;
    }

    /// <summary>
    /// Creates a drift-warning text tip when the risk is non-empty and drift warnings are enabled.
    /// </summary>
    /// <param name="key">Warning key suffix under the <c>drift_warning</c> localization group.</param>
    /// <returns>The warning tip, or <see langword="null"/> when no warning should be displayed.</returns>
    public static HoverTip? DriftWarning(string key, PredictionRisk risk)
    {
        if (!risk.HasRisk || !RandomForeseerSettings.EnableDriftWarnings)
        {
            return null;
        }

        var tip = Text($"drift_warning.{key}", description =>
        {
            var modelNames = risk.Models.Select(model => model.GetTitle()).Distinct().ToList();
            var shownModelNames = modelNames.Take(MaxDriftWarningModelNames).ToList();
            var extraModelCount = modelNames.Count - shownModelNames.Count;

            description.Add("HasModels", shownModelNames.Count > 0);
            description.Add("Models", shownModelNames);
            description.Add("ExtraModelCount", extraModelCount);
        });
        return tip;
    }

    private static HoverTip FromModelHoverTip(HoverTip tip)
    {
        tip.Id = $"{Entry.ModId}:Prediction:{tip.Id}";
        tip.IsInstanced = true;
        // Vanilla records hover tips with canonical models as discovered progress.
        // Prediction tips are informational only, so they must not reveal cards/relics/potions in the save.
        tip.CanonicalModel = null;
        return tip;
    }
}
