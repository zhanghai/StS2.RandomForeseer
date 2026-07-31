using MegaCrit.Sts2.Core.ValueProps;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnTurnEndInHand;

internal static class CardOnTurnEndInHandMirrorContextExtensions
{
    /// <summary>
    /// Mirrors the shared damage behavior for turn-end-in-hand cards.
    /// </summary>
    public static void DamageOwner(
        this CardOnTurnEndInHandMirrorContext context,
        decimal amount,
        ValueProp props)
    {
        var owner = context.PreviewCard.Owner.Creature;
        context.Simulator.Damage([owner], amount, props, owner, context.Card, cardPlay: null);
    }
}
