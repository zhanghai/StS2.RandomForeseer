using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.PotionOnUse;

internal static class CardGenerationPotionMirrors
{
    public static void AttackPotionOnUse(AttackPotion _, PotionOnUseMirrorContext context)
    {
        RecordCharacterCardOptions(context, CardType.Attack, 3);
    }

    public static void SkillPotionOnUse(SkillPotion _, PotionOnUseMirrorContext context)
    {
        RecordCharacterCardOptions(context, CardType.Skill, 3);
    }

    public static void PowerPotionOnUse(PowerPotion _, PotionOnUseMirrorContext context)
    {
        RecordCharacterCardOptions(context, CardType.Power, 3);
    }

    public static void ColorlessPotionOnUse(ColorlessPotion _, PotionOnUseMirrorContext context)
    {
        var player = context.TargetPlayer;
        var cards = player.GetUnlockedColorlessCards()
            .GetDistinctForCombat(player, 3, context.Rng.CombatCardGeneration)
            .ToList();

        RecordOptions(context, cards);
    }

    public static void CosmicConcoctionOnUse(CosmicConcoction potion, PotionOnUseMirrorContext context)
    {
        var player = context.TargetPlayer;
        var cards = player.GetUnlockedColorlessCards()
            .GetDistinctForCombat(player, potion.DynamicVars.Cards.IntValue, context.Rng.CombatCardGeneration)
            .Select(static card => card.Upgrade())
            .ToList();

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, potion.Owner);
    }

    public static void OrobicAcidOnUse(OrobicAcid potion, PotionOnUseMirrorContext context)
    {
        var player = context.TargetPlayer;
        List<PredictedCard> cards = [];
        CardType[] types = [CardType.Attack, CardType.Skill, CardType.Power];

        foreach (var type in types)
        {
            cards.AddRange(player.GetUnlockedCharacterCards()
                .Where(candidate => candidate.Type == type)
                .GetDistinctForCombat(player, 1, context.Rng.CombatCardGeneration));
        }

        foreach (var card in cards)
        {
            card.SetToFreeThisTurn();
        }

        context.Simulator.AddGeneratedCardsToCombat(cards, PileType.Hand, potion.Owner);
    }

    private static void RecordCharacterCardOptions(PotionOnUseMirrorContext context, CardType type, int count)
    {
        var player = context.TargetPlayer;
        var cards = player.GetUnlockedCharacterCards()
            .Where(candidate => candidate.Type == type)
            .GetDistinctForCombat(player, count, context.Rng.CombatCardGeneration)
            .ToList();

        RecordOptions(context, cards);
    }

    private static void RecordOptions(PotionOnUseMirrorContext context, IReadOnlyList<PredictedCard> cards)
    {
        if (cards.Count == 0)
        {
            return;
        }

        context.History.CardGenerationOptions(cards);
        // The options are deterministic, but the selected card and its addition to hand remain unresolved.
        context.History.RecordRisk(PredictionRiskReason.UnresolvedPlayerChoice);
    }
}
