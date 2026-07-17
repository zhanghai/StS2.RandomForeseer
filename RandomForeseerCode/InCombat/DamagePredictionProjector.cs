using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class DamagePredictionProjector
{
    public static DamagePrediction FromHistory(IEnumerable<CombatPredictionDamageReceivedEntry> history)
    {
        var targets = history
            .GroupBy(static entry => entry.Receiver)
            .Select(group => new DamagePredictionTarget(
                group.Key,
                group
                    .Select(static entry => new DamagePredictionLine(
                        entry.Result.TotalDamage,
                        entry.Result.UnblockedDamage,
                        entry.Result.WasTargetKilled,
                        entry.Trace!.Source))
                    .ToList()))
            .ToList();

        return targets.Count == 0 ? DamagePrediction.Empty : new DamagePrediction(targets);
    }
}
