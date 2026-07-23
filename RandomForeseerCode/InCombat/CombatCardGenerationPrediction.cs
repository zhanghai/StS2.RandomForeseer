using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionPredictionContext context)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionCardPrediction) ||
            !ShouldShowPotionCardPrediction(context))
        {
            return [];
        }

        return [.. PredictPotionCards(context).ToPredictionHoverTips()];
    }

    private static bool ShouldShowPotionCardPrediction(PotionPredictionContext context)
    {
        return RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes) ||
            CombatManager.Instance.IsInProgress &&
            !context.SourceOwner.Creature.IsDead &&
            !context.Target.Creature.IsDead;
    }

    private static IReadOnlyList<CardModel> PredictPotionCards(PotionPredictionContext context)
    {
        var source = context.Source;
        var target = context.Target;
        var previewRng = target.RunState.Rng.CombatCardGeneration.Clone();

        return source switch
        {
            AttackPotion => PredictCharacterCards(target, CardType.Attack, 3, previewRng),
            SkillPotion => PredictCharacterCards(target, CardType.Skill, 3, previewRng),
            PowerPotion => PredictCharacterCards(target, CardType.Power, 3, previewRng),
            ColorlessPotion => PredictColorlessCards(target, 3, previewRng),
            CosmicConcoction => PredictColorlessCards(target, source.DynamicVars.Cards.IntValue, previewRng)
                .Select(PredictionUtils.ToUpgradedCard)
                .ToList(),
            OrobicAcid => new[] { CardType.Attack, CardType.Skill, CardType.Power }
                .SelectMany(type => PredictCharacterCards(target, type, 1, previewRng))
                .ToList(),
            _ => []
        };
    }

    private static List<CardModel> PredictCharacterCards(Player player, CardType type, int count, Rng previewRng)
    {
        return player.GetUnlockedCharacterCards()
            .Where(candidate => candidate.Type == type)
            .TakeRandomDistinctForCombat(player, count, previewRng)
            .ToList();
    }

    private static List<CardModel> PredictColorlessCards(Player player, int count, Rng previewRng)
    {
        return player.GetUnlockedColorlessCards()
            .TakeRandomDistinctForCombat(player, count, previewRng)
            .ToList();
    }
}
