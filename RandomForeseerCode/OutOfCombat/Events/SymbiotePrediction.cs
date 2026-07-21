using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class SymbiotePrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(Symbiote symbiote, EventOption option)
    {
        return option.TextKey == "SYMBIOTE.pages.INITIAL.options.KILL_WITH_FIRE"
            ? [.. PredictKillWithFire(symbiote).ToPredictionHoverTips()]
            : [];
    }

    private static IReadOnlyList<CardModel> PredictKillWithFire(Symbiote symbiote)
    {
        return OutOfCombatPredictionUtils.PredictDistinctDeckTransformResults(symbiote.Owner!, symbiote.Rng);
    }
}
