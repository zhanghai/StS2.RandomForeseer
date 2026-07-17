using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed record DamagePredictionResult(
    IReadOnlyList<DamagePredictionTarget> Targets,
    PredictionRisk Risk)
{
    public static DamagePredictionResult Empty { get; } = new([], PredictionRisk.None);

    public bool HasTargets => Targets.Count > 0;

    public bool HasRisk => Risk.HasRisk;

    public static DamagePredictionResult FromDamageHistory(CombatPredictionSimulator simulator)
    {
        var history = simulator.History
            .OfType<CombatPredictionDamageReceivedEntry>()
            .ToList();
        return new DamagePredictionResult(
            DamagePredictionProjector.FromHistory(history).Targets,
            simulator.History.GetRisk(history));
    }
}
