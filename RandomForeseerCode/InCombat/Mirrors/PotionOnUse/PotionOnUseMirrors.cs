using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.PotionOnUse;

using Registry = ModelMethodMirrorRegistry<PotionModel, PotionOnUseMirrorContext>;

/// <summary>Owns exact-runtime-type dispatch for mirrored <see cref="PotionModel.OnUse"/> behavior.</summary>
internal static class PotionOnUseMirrors
{
    private static readonly MirrorMethodSpec OnUse = new(
        typeof(PotionModel),
        "OnUse",
        BindingFlags.Instance | BindingFlags.NonPublic,
        [typeof(PlayerChoiceContext), typeof(Creature)]);

    private static readonly Registry Registry = CreateRegistry();

    /// <summary>
    /// Returns whether the potion's exact runtime type has a registered handler without opening a trace or recording risk.
    /// </summary>
    public static bool CanMirror(PotionModel potion)
    {
        return Registry.Query(potion) is MirrorDispatchKind.Handled;
    }

    /// <summary>Invokes the exact registered handler inside a potion <c>OnUse</c> method frame.</summary>
    /// <remarks>Unsupported gameplay overrides record <c>MethodNotMirrored</c> risk through the supplied simulator.</remarks>
    public static MirrorDispatchResult Invoke(
        CombatPredictionSimulator simulator,
        PotionModel potion,
        Creature? target)
    {
        return Registry.Invoke(potion, new()
        {
            Simulator = simulator,
            Potion = potion,
            Target = target
        });
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(OnUse);

        registry.Register<AttackPotion>(CardGenerationPotionMirrors.AttackPotionOnUse);
        registry.Register<SkillPotion>(CardGenerationPotionMirrors.SkillPotionOnUse);
        registry.Register<PowerPotion>(CardGenerationPotionMirrors.PowerPotionOnUse);
        registry.Register<ColorlessPotion>(CardGenerationPotionMirrors.ColorlessPotionOnUse);
        registry.Register<CosmicConcoction>(CardGenerationPotionMirrors.CosmicConcoctionOnUse);
        registry.Register<OrobicAcid>(CardGenerationPotionMirrors.OrobicAcidOnUse);

        registry.Register<EntropicBrew>(PotionGenerationPotionMirrors.EntropicBrewOnUse);

        registry.Register<BottledPotential>(DrawPotionMirrors.BottledPotentialOnUse);
        registry.Register<Clarity>(DrawPotionMirrors.ClarityOnUse);
        registry.Register<CureAll>(DrawPotionMirrors.CureAllOnUse);
        registry.Register<GlowwaterPotion>(DrawPotionMirrors.GlowwaterPotionOnUse);
        registry.Register<SneckoOil>(DrawPotionMirrors.SneckoOilOnUse);
        registry.Register<SwiftPotion>(DrawPotionMirrors.SwiftPotionOnUse);

        registry.Register<DistilledChaos>(AutoPlayPotionMirrors.DistilledChaosOnUse);

        return registry;
    }
}
