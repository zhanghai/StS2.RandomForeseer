using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class ChooseACardPredictionContext
{
    private static readonly List<Registration> Registrations = [];

    public static bool TryGet(CardModel card, [NotNullWhen(true)] out AbstractModel? source)
    {
        lock (Registrations)
        {
            if (Registrations.FindLast(item => item.Cards.Contains(card)) is { } registration)
            {
                source = registration.Source;
                return true;
            }
        }

        source = null;
        return false;
    }

    public static Registration? Register(IReadOnlyList<CardModel> cards, AbstractModel? source)
    {
        if (cards.Count == 0 || source is null)
        {
            return null;
        }

        var registration = new Registration(cards, source);
        lock (Registrations)
        {
            Registrations.Add(registration);
        }

        return registration;
    }

    public static void Unregister(Registration registration)
    {
        lock (Registrations)
        {
            Registrations.Remove(registration);
        }
    }

    internal sealed class Registration(IEnumerable<CardModel> cards, AbstractModel source)
    {
        public HashSet<CardModel> Cards { get; } = [.. cards];

        public AbstractModel Source { get; } = source;
    }
}

[HarmonyPatch(typeof(CardSelectCmd), nameof(CardSelectCmd.FromChooseACardScreen))]
internal static class ChooseACardPredictionContextPatch
{
    [HarmonyPrefix]
    private static void Prefix(
        PlayerChoiceContext context,
        IReadOnlyList<CardModel> cards,
        out ChooseACardPredictionContext.Registration? __state)
    {
        __state = ChooseACardPredictionContext.Register(cards, context.LastInvolvedModel);
    }

    [HarmonyPostfix]
    private static void Postfix(
        ref Task<CardModel?> __result,
        ChooseACardPredictionContext.Registration? __state)
    {
        if (__state is not null)
        {
            __result = __result.WithFinally(() => ChooseACardPredictionContext.Unregister(__state));
        }
    }
}
