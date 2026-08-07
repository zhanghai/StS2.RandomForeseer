using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Cards;

namespace RandomForeseer.RandomForeseerCode.InCombat.Simulation;

internal sealed partial class CombatPredictionSimulator
{
    /// <summary>
    /// Currently mirrors the prediction-relevant parts of <see cref="CombatManager.EndPlayerTurnPhaseOneInternal()"/>.
    /// </summary>
    public void SimulateEndPlayerTurn()
    {
        var playersEndingTurn = CombatManager.Instance.PlayersTakingExtraTurn switch
        {
            { Count: > 0 } extraTurnPlayers => extraTurnPlayers,
            _ => State.CombatState.Players
        };

        foreach (var player in playersEndingTurn)
        {
            HookMirrors.AfterAutoPostPlayPhaseEntered(this, player);
        }

        HookMirrors.BeforeSideTurnEnd(
            this,
            State.CombatState.CurrentSide,
            [.. playersEndingTurn.Select(static player => player.Creature)]);

        // TODO: Mirror CombatManager's win-condition check here once combat-ending checks are centralized.

        foreach (var player in playersEndingTurn)
        {
            DoTurnEnd(player);
        }

        // Vanilla next calls Hook.BeforeFlush for each ending player. Its only vanilla listener is
        // SlumberingEssence, which is not used by the current version of the base game, so the hook is omitted.
    }

    /// <summary>
    /// Mirrors the prediction-relevant parts of <see cref="CombatManager.DoTurnEnd"/>.
    /// </summary>
    private void DoTurnEnd(Player player)
    {
        var playerState = State.GetPlayerCombatState(player);
        playerState.OrbQueue.BeforeTurnEnd(this);

        // TODO: Mirror CombatManager.DoTurnEnd's combat-ending check here once those checks are centralized.

        List<PredictedCard> turnEndCards = [];
        List<PredictedCard> etherealCards = [];

        foreach (var card in playerState.Hand.Cards)
        {
            if (card.Preview.HasTurnEndInHandEffect)
            {
                turnEndCards.Add(card);
            }
            else if (card.GetKeywords(State).Contains(CardKeyword.Ethereal) &&
                     Hook.ShouldEtherealTrigger(State.CombatState, card.Preview))
            {
                etherealCards.Add(card);
            }
        }

        foreach (var card in etherealCards)
        {
            Exhaust(card, causedByEthereal: true);
        }

        foreach (var card in turnEndCards)
        {
            OnTurnEndInHandWrapper(card);
        }
    }

    /// <summary>
    /// Mirrors the prediction-relevant parts of <see cref="CardModel.OnTurnEndInHandWrapper"/>.
    /// </summary>
    private void OnTurnEndInHandWrapper(PredictedCard card)
    {
        AddToPile(card, PileType.Play);
        CardOnTurnEndInHandMirrors.Invoke(this, card);

        // Vanilla does not check Hook.ShouldEtherealTrigger here, so we keep the same behavior.
        if (card.GetKeywords(State).Contains(CardKeyword.Ethereal))
        {
            Exhaust(card, causedByEthereal: true);
        }
        else
        {
            AddToPile(card, PileType.Discard);
        }
    }
}
