using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Events;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Events;

internal static class DoorsOfLightAndDarkPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(DoorsOfLightAndDark doors, EventOption option)
    {
        return option.TextKey == "DOORS_OF_LIGHT_AND_DARK.pages.INITIAL.options.LIGHT"
            ? [.. OutOfCombatPredictionUtils.PredictUpgradedDeckCards(
                doors.Owner!,
                2,
                card => card.IsUpgradable,
                doors.Rng.Clone()).ToPredictionHoverTips()]
            : [];
    }
}
