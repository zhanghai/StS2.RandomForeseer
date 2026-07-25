using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using STS2RitsuLib.Utils.HarmonyIl;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

/// <summary>
/// Infers simple, directly invoked vanilla command templates from an unregistered <see cref="CardModel.OnPlay" />.
/// </summary>
internal sealed class CardOnPlayInferer : IModelMethodMirrorInferer<CardModel, CardOnPlayMirrorContext>
{
    public static CardOnPlayInferer Instance { get; } = new();

    private CardOnPlayInferer() { }

    public bool TryInfer(
        Type runtimeType,
        MethodInfo overrideMethod,
        [NotNullWhen(true)] out Action<CardModel, CardOnPlayMirrorContext>? handler)
    {
        IReadOnlyList<MethodInfo> calledMethods;
        try
        {
            calledMethods = overrideMethod.GetOriginalIl().CalledMethods;
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn(
                $"Could not inspect original OnPlay IL for inferred card mirror {runtimeType.FullName}: {ex}");
            handler = null;
            return false;
        }

        var hasExecutedAttack = calledMethods.Any(IsAttackExecution);
        List<EffectKind> effects = [];

        foreach (var calledMethod in calledMethods)
        {
            EffectKind? effect = calledMethod switch
            {
                _ when hasExecutedAttack && IsAttackCreation(calledMethod) => EffectKind.Attack,
                _ when IsBlockGain(calledMethod) => EffectKind.Block,
                _ => null
            };

            if (effect is { } kind && !effects.Contains(kind))
            {
                effects.Add(kind);
            }
        }

        if (effects.Count == 0)
        {
            handler = null;
            return false;
        }

        var inferredEffects = effects.ToArray();
        handler = (card, context) => Invoke(inferredEffects, card, context);
        return true;
    }

    private static void Invoke(
        IReadOnlyList<EffectKind> effects,
        CardModel card,
        CardOnPlayMirrorContext context)
    {
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case EffectKind.Attack when card.Type is CardType.Attack:
                    GeneralCardMirrors.GeneralAttackOnPlay(card, context);
                    break;

                case EffectKind.Block when card.GainsBlock:
                    GeneralCardMirrors.GeneralBlockOnPlay(card, context);
                    break;
            }
        }
    }

    private static bool IsAttackCreation(MethodInfo method)
    {
        return method.DeclaringType == typeof(DamageCmd) && method.Name == nameof(DamageCmd.Attack);
    }

    private static bool IsAttackExecution(MethodInfo method)
    {
        return method.DeclaringType == typeof(AttackCommand) && method.Name == nameof(AttackCommand.Execute);
    }

    private static bool IsBlockGain(MethodInfo method)
    {
        return method.DeclaringType == typeof(CreatureCmd) && method.Name == nameof(CreatureCmd.GainBlock);
    }

    private enum EffectKind
    {
        Attack,
        Block
    }
}
