using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal sealed class TransformPreviewPredictor(Rng realRng, bool upgradePreview = false)
{
    private Rng _previewRng = realRng.Clone();

    public static Func<CardModel, CardTransformation>? Make(
        Rng realRng,
        bool upgradePreview = false,
        PredictionFairness fairness = PredictionFairness.Fair,
        RelicModel? relicSource = null)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.DeckTransformPredictionEnabled ||
            !(settings.Allows(fairness) ||
                (relicSource is not null && RewardPagePredictionContext.HasOtherPendingReward(relicSource))))
        {
            return null;
        }

        return new TransformPreviewPredictor(realRng, upgradePreview).PredictNext;
    }

    public void Reset()
    {
        _previewRng = realRng.Clone();
    }

    public IReadOnlyList<IHoverTip> GetHoverTips(CardModel card, IEnumerable<CardModel> selectedCards, int maxSelect)
    {
        return TransformPrediction.GetHoverTips(
            card,
            selectedCards,
            maxSelect,
            realRng,
            isInCombat: false,
            upgradePreview ? PredictionUtils.CreateUpgradedCard : null);
    }

    private CardTransformation PredictNext(CardModel original)
    {
        var predicted = PredictionUtils.PredictTransformResult(original, _previewRng, isInCombat: false);

        return new CardTransformation(
            original,
            upgradePreview ? PredictionUtils.CreateUpgradedCard(predicted) : predicted);
    }
}
