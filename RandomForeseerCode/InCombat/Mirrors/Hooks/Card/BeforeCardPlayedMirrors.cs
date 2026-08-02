using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Achievements;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = ModelMethodMirrorRegistry<AbstractModel, BeforeCardPlayedMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.BeforeCardPlayed.
internal static class BeforeCardPlayedMirrors
{
    private static readonly MirrorMethodSpec BeforeCardPlayed = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeCardPlayed),
        [typeof(CardPlay)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, BeforeCardPlayedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(BeforeCardPlayed);

        registry.RegisterIgnored<SkillSilent1Achievement>();
        registry.RegisterIgnored<ChemicalX>();
        registry.RegisterIgnored<PaelsEye>();

        return registry;
    }
}

internal sealed class BeforeCardPlayedMirrorContext : CombatPredictionCardMirrorContext
{
    public required CardPlay CardPlay { get; init; }
}
