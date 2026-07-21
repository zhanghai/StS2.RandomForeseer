using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common;

internal static class PredictedCardExtensions
{
    // Mirrors CardCmd.Upgrade.
    public static PredictedCard Upgrade(this PredictedCard card)
    {
        if (card.Preview.IsUpgradable)
        {
            var previewCard = card.MutablePreview;
            previewCard.UpgradeInternal();
            previewCard.FinalizeUpgradeInternal();
        }

        return card;
    }

    // Upgrades the cards if the condition is true, otherwise returns the original cards.
    public static IEnumerable<PredictedCard> UpgradeIf(
        this IEnumerable<PredictedCard> cards,
        bool shouldUpgrade)
    {
        return shouldUpgrade ? cards.Select(card => card.Upgrade()) : cards;
    }

    /// <summary>
    /// Returns the original cards from the predicted cards.
    /// </summary>
    public static IEnumerable<CardModel> SelectOriginals(this IEnumerable<PredictedCard> cards)
    {
        return cards.Select(static card => card.Original);
    }

    /// <summary>
    /// Returns the preview cards from the predicted cards.
    /// </summary>
    public static IEnumerable<CardModel> SelectPreviews(this IEnumerable<PredictedCard> cards)
    {
        return cards.Select(static card => card.Preview);
    }
}
