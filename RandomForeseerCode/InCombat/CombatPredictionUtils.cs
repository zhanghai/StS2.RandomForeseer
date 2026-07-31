using MegaCrit.Sts2.Core.Combat;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatPredictionUtils
{
    public static CombatState? GetCurrentCombatState()
    {
        return CombatManager.Instance._turnState?.State;
    }
}
