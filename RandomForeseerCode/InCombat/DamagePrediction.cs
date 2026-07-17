using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed record DamagePrediction(IReadOnlyList<DamagePredictionTarget> Targets)
{
    public static DamagePrediction Empty { get; } = new([]);

    public bool HasTargets => Targets.Count > 0;
}

internal sealed record DamagePredictionTarget(
    Creature Target,
    IReadOnlyList<DamagePredictionLine> DamageLines)
{
    public decimal TotalDamage => DamageLines.Sum(static line => line.Damage);

    public decimal TotalUnblockedDamage => DamageLines.Sum(static line => line.UnblockedDamage);

    public bool WasTargetKilled => DamageLines.Any(static line => line.WasTargetKilled);
}

internal sealed record DamagePredictionLine(
    decimal Damage,
    decimal UnblockedDamage,
    bool WasTargetKilled,
    AbstractModel Source);
