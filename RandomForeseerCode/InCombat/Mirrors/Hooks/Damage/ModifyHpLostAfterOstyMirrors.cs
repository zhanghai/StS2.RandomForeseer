using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Damage;

using Registry = ModelMethodMirrorRegistry<AbstractModel, ModifyHpLostAfterOstyMirrorContext, decimal>;

// Mirrors the early and late listener passes inside Hook.ModifyHpLost's AfterOsty phase.
internal static class ModifyHpLostAfterOstyMirrors
{
    private static readonly MirrorMethodSpec ModifyHpLostAfterOsty = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyHpLostAfterOsty),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly MirrorMethodSpec ModifyHpLostAfterOstyLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyHpLostAfterOstyLate),
        [
            typeof(Creature),
            typeof(decimal),
            typeof(ValueProp),
            typeof(Creature),
            typeof(CardModel)
        ]);

    private static readonly Registry Registry = CreateRegistry();
    private static readonly Registry LateRegistry = CreateLateRegistry();

    public static decimal Invoke(AbstractModel listener, ModifyHpLostAfterOstyMirrorContext context)
    {
        return Registry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginal(listener, context);
    }

    public static decimal InvokeLate(AbstractModel listener, ModifyHpLostAfterOstyMirrorContext context)
    {
        return LateRegistry.TryInvokeRegistered(listener, context, out var result)
            ? result.Value
            : InvokeOriginalLate(listener, context);
    }

    private static decimal InvokeOriginal(AbstractModel listener, ModifyHpLostAfterOstyMirrorContext context)
    {
        return listener.ModifyHpLostAfterOsty(
            context.Target,
            context.Amount,
            context.Props,
            context.Dealer,
            context.CardSource?.Preview);
    }

    private static decimal InvokeOriginalLate(AbstractModel listener, ModifyHpLostAfterOstyMirrorContext context)
    {
        return listener.ModifyHpLostAfterOstyLate(
            context.Target,
            context.Amount,
            context.Props,
            context.Dealer,
            context.CardSource?.Preview);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(ModifyHpLostAfterOsty);

        registry.Register<SlipperyPower>(HandleSlipperyPower);

        return registry;
    }

    private static Registry CreateLateRegistry()
    {
        var registry = new Registry(ModifyHpLostAfterOstyLate);

        registry.Register<BufferPower>(HandleBufferPower);

        return registry;
    }

    private static decimal HandleSlipperyPower(
        SlipperyPower power,
        ModifyHpLostAfterOstyMirrorContext context)
    {
        return context.StateStore.GetPowerAmount(power).IsActive
            ? InvokeOriginal(power, context)
            : context.Amount;
    }

    private static decimal HandleBufferPower(
        BufferPower power,
        ModifyHpLostAfterOstyMirrorContext context)
    {
        return context.StateStore.GetPowerAmount(power).IsActive
            ? InvokeOriginalLate(power, context)
            : context.Amount;
    }
}

internal sealed class ModifyHpLostAfterOstyMirrorContext : CombatPredictionMirrorContext
{
    public required Creature Target { get; init; }

    public required decimal Amount { get; set; }

    public required ValueProp Props { get; init; }

    public required Creature? Dealer { get; init; }

    public required PredictedCard? CardSource { get; init; }
}
