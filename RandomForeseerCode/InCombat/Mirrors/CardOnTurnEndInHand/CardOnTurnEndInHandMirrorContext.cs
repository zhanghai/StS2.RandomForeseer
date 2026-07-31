using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnTurnEndInHand;

internal sealed class CardOnTurnEndInHandMirrorContext : CombatPredictionCardMirrorContext<CardModel>
{
    // The dispatch trace belongs to the real card, not its optional detached preview.
    protected override AbstractModel GetDispatchSource(CardModel _) => OriginalCard;
}
