using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class GeneralCardMirrors
{
    /// <summary>
    /// Simulates a general attack when a card is played.
    /// </summary>
    /// <remarks>
    /// Targeting examples:
    /// <list type="bullet">
    /// <item><see cref="StrikeIronclad"/> targets any enemy.</item>
    /// <item><see cref="Breakthrough"/> targets all enemies.</item>
    /// <item><see cref="SwordBoomerang"/> targets random enemies.</item>
    /// </list>
    /// </remarks>
    public static void GeneralAttackOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        AttackCommand? command;
        if (card.DynamicVars.ContainsKey("CalculatedDamage"))
        {
            command = DamageCmd.Attack(card.DynamicVars.CalculatedDamage)
                .FromCard(card, context.CardPlay);
        }
        else if (card.DynamicVars.ContainsKey("Damage"))
        {
            command = DamageCmd.Attack(card.DynamicVars.Damage.BaseValue)
                .FromCard(card, context.CardPlay);
        }
        else if (card.DynamicVars.ContainsKey("OstyDamage"))
        {
            if (card.Owner.Osty is not { } osty || !context.State.GetCreature(osty).IsAlive)
            {
                return;
            }

            command = DamageCmd.Attack(card.DynamicVars.OstyDamage.BaseValue)
                .FromOsty(osty, card, context.CardPlay);
        }
        else
        {
            Entry.Logger.Warn($"Card {card.Title} has no damage var to simulate an attack command.");
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        if (card.DynamicVars.ContainsKey("Repeat"))
        {
            command.WithHitCount(card.DynamicVars.Repeat.IntValue);
        }

        switch (card.TargetType)
        {
            case TargetType.AnyEnemy:
                command.Targeting(context.CardPlay.Target!);
                break;

            case TargetType.AllEnemies:
                command.TargetingAllOpponents(context.CombatState);
                break;

            case TargetType.RandomEnemy:
                command.TargetingRandomOpponents(context.CombatState);
                break;

            default:
                Entry.Logger.Warn($"Attack {card.Title} has an unsupported target type: {card.TargetType}");
                context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                return;
        }

        command.Simulate(context.Simulator);
    }

    /// <summary>
    /// Simulates a general block gain when a card is played.
    /// </summary>
    /// <remarks>
    /// Targeting examples:
    /// <list type="bullet">
    /// <item><see cref="DefendIronclad"/> targets self.</item>
    /// <item><see cref="Lift"/> targets any ally.</item>
    /// <item><see cref="Rally"/> targets all allies.</item>
    /// <item>
    /// <see cref="IronWave"/> is a combined attack-and-block card that targets an enemy while its block effect targets
    /// the owner.
    /// </item>
    /// </list>
    /// </remarks>
    public static void GeneralBlockOnPlay(CardModel card, CardOnPlayMirrorContext context)
    {
        Action<Creature> blockAction;
        if (card.DynamicVars.ContainsKey("CalculatedBlock"))
        {
            var amount = context.Calculate(card.DynamicVars.CalculatedBlock);
            var props = card.DynamicVars.CalculatedBlock.Props;
            blockAction = target => context.GainBlock(target, amount, props);
        }
        else if (card.DynamicVars.ContainsKey("Block"))
        {
            blockAction = target => context.GainBlock(target);
        }
        else
        {
            Entry.Logger.Warn($"Card {card.Title} has no block var to simulate a block gain.");
            context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
            return;
        }

        switch (card.TargetType)
        {
            case TargetType.Self:
            case TargetType.AnyEnemy or TargetType.AllEnemies or TargetType.RandomEnemy
                when card.Type is CardType.Attack:
                blockAction(card.Owner.Creature);
                break;

            case TargetType.AnyAlly:
                if (context.CardPlay.Target is { } target)
                {
                    blockAction(target);
                }
                break;

            case TargetType.AllAllies:
                var allies = context.CombatState.GetTeammatesOf(card.Owner.Creature)
                    .Where(creature => creature.IsPlayer && context.State.GetCreature(creature).IsAlive);
                foreach (var ally in allies)
                {
                    blockAction(ally);
                }
                break;

            default:
                Entry.Logger.Warn($"Block {card.Title} has an unsupported target type: {card.TargetType}");
                context.History.RecordRisk(PredictionRiskReason.MethodMirrorIncomplete);
                return;
        }
    }
}
