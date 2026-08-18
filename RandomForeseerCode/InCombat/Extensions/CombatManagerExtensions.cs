using MegaCrit.Sts2.Core.Combat;

namespace RandomForeseer.RandomForeseerCode.InCombat.Extensions;

internal static class CombatManagerExtensions
{
    extension(CombatManager combatManager)
    {
        public CombatState? LiveCombatState => combatManager._turnState is { IsInProgress: true } turnState
            ? turnState.State
            : null;
    }
}
