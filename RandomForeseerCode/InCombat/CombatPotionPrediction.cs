using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.PotionOnUse;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>Provides the unified simulation facade for in-combat potion prediction.</summary>
internal static class CombatPotionPrediction
{
    /// <summary>Builds untargeted combat prediction HoverTips for one live mutable potion.</summary>
    /// <remarks>Simulation and projection failures are contained at this ordinary HoverTip injection boundary.</remarks>
    public static IReadOnlyList<IHoverTip> GetHoverTips(PotionModel potion)
    {
        try
        {
            return Predict(potion, target: null)?.HoverTips ?? [];
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat potion prediction failed for {potion.Id}: {ex}");
            return [];
        }
    }

    /// <summary>Simulates one potion use and projects every enabled result from the same history.</summary>
    /// <param name="potion">The live mutable potion whose exact identity anchors the prediction trace.</param>
    /// <param name="target">The explicit target, or <see langword="null"/> when the potion can resolve its owner.</param>
    /// <returns>
    /// The completed projection, or <see langword="null"/> when no relevant feature is enabled, the potion cannot be
    /// mirrored, the source has no live combat, the explicit target belongs to another combat, or target validation fails.
    /// </returns>
    /// <remarks>Target-aware adapters catch failures through their shared combat prediction session.</remarks>
    public static CombatPredictionProjection? Predict(PotionModel potion, Creature? target)
    {
        if (!CombatPredictionProjector.HasAnyEnabledFeature(PredictionActionKind.PotionUse) ||
            !potion.IsMutable ||
            potion.Owner is not { } owner ||
            owner.Creature.CombatState is not { } combatState ||
            (target is not null && target.CombatState != combatState) ||
            !PotionOnUseMirrors.CanMirror(potion))
        {
            return null;
        }

        var simulator = new CombatPredictionSimulator(combatState);
        return simulator.ManualUse(potion, target, out var frame)
            ? CombatPredictionProjector.Project(simulator.History, frame)
            : null;
    }
}
