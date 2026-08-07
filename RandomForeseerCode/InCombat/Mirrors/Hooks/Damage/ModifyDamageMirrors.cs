using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Attack;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = MethodMirrorRegistry<AbstractModel, ModifyDamageMirrorContext, decimal>;

// Mirrors the additive and multiplicative listener passes inside Hook.ModifyDamage.
internal static class ModifyDamageMirrors
{
    private static readonly MirrorMethodSpec ModifyDamageAdditive = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyDamageAdditive),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel),
            typeof(CardPlay)
        ]);

    private static readonly MirrorMethodSpec ModifyDamageMultiplicative = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyDamageMultiplicative),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel),
            typeof(CardPlay)
        ]);

    private static readonly Registry AdditiveRegistry = CreateAdditiveRegistry();
    private static readonly Registry MultiplicativeRegistry = CreateMultiplicativeRegistry();

    public static decimal InvokeAdditive(AbstractModel listener, ModifyDamageMirrorContext context)
    {
        return AdditiveRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginalAdditive(listener, context);
    }

    public static decimal InvokeMultiplicative(AbstractModel listener, ModifyDamageMirrorContext context)
    {
        return MultiplicativeRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginalMultiplicative(listener, context);
    }

    private static decimal InvokeOriginalAdditive(AbstractModel listener, ModifyDamageMirrorContext context)
    {
        return listener.ModifyDamageAdditive(
            context.Target,
            context.Amount,
            context.Props,
            context.Dealer,
            context.CardSource?.Preview,
            context.CardPlay);
    }

    private static decimal InvokeOriginalMultiplicative(AbstractModel listener, ModifyDamageMirrorContext context)
    {
        return listener.ModifyDamageMultiplicative(
            context.Target,
            context.Amount,
            context.Props,
            context.Dealer,
            context.CardSource?.Preview,
            context.CardPlay);
    }

    private static Registry CreateAdditiveRegistry()
    {
        var registry = new Registry(ModifyDamageAdditive);

        registry.Register<VigorPower>(VigorPowerMirrors.ModifyDamageAdditive);

        return registry;
    }

    private static Registry CreateMultiplicativeRegistry()
    {
        var registry = new Registry(ModifyDamageMultiplicative);

        registry.Register<FlutterPower>(HandleFlutterPower);
        registry.Register<GigantificationPower>(GigantificationPowerMirrors.ModifyDamageMultiplicative);
        registry.Register<SlowPower>(HandleSlowPower);
        registry.Register<SurroundedPower>(HandleSurroundedPower);

        registry.Register<PenNib>(HandlePenNib);

        return registry;
    }

    private static decimal HandleFlutterPower(FlutterPower power, ModifyDamageMirrorContext context)
    {
        return context.StateStore.GetPowerAmount(power).IsActive
            ? InvokeOriginalMultiplicative(power, context)
            : 1;
    }

    private static decimal HandleSlowPower(SlowPower power, ModifyDamageMirrorContext context)
    {
        if (context.Target != power.Owner || !context.Props.IsPoweredAttack())
        {
            return 1;
        }

        var amount = context.StateStore.Get(power,
            () => new CounterPredictionState(power.DynamicVars["SlowAmount"].IntValue)).Value;
        return 1 + 0.1m * amount;
    }

    private static decimal HandleSurroundedPower(SurroundedPower power, ModifyDamageMirrorContext context)
    {
        if (context.Dealer is null || context.Target != power.Owner)
        {
            return 1;
        }

        var facing = context.StateStore.Get(power, () => new SurroundedPredictionState(power)).Facing;
        return facing switch
        {
            SurroundedPower.Direction.Right when context.Dealer.HasPower<BackAttackLeftPower>() => 1.5m,
            SurroundedPower.Direction.Left when context.Dealer.HasPower<BackAttackRightPower>() => 1.5m,
            _ => 1
        };
    }

    private static decimal HandlePenNib(PenNib relic, ModifyDamageMirrorContext context)
    {
        if (!context.Props.IsPoweredAttack() ||
            context.CardSource is null ||
            context.Dealer != relic.Owner.Creature && context.Dealer != relic.Owner.Osty)
        {
            return 1;
        }

        var state = context.StateStore.Get(relic, () => new PenNibPredictionState(relic));
        if (state.AttackToDouble is not null)
        {
            return state.AttackToDouble == context.CardSource.Original ? 2 : 1;
        }

        return context.CardPlay is null &&
            context.CardSource.GetPile(context.State)?.Type is not PileType.Play &&
            state.AttacksPlayed == 9
                ? 2
                : 1;
    }
}

internal sealed class ModifyDamageMirrorContext : CombatMirrorContext
{
    public required Creature? Target { get; init; }

    public required Creature? Dealer { get; init; }

    public required decimal Amount { get; set; }

    public required ValueProp Props { get; init; }

    public required PredictedCard? CardSource { get; init; }

    public required CardPlay? CardPlay { get; init; }
}
