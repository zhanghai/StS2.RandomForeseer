using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Achievements;
using MegaCrit.Sts2.Core.Models.Badges;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterCardPlayedMirrorContext>;

// Mirrors the prediction-relevant parts of Hook.AfterCardPlayed and its late phase.
internal static class AfterCardPlayedMirrors
{
    private static readonly MirrorMethodSpec AfterCardPlayed = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterCardPlayed),
        [typeof(PlayerChoiceContext), typeof(CardPlay)]);

    private static readonly MirrorMethodSpec AfterCardPlayedLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterCardPlayedLate),
        [typeof(PlayerChoiceContext), typeof(CardPlay)]);

    private static readonly Registry Registry = CreateRegistry();
    private static readonly Registry LateRegistry = new(AfterCardPlayedLate);

    public static void Invoke(AbstractModel listener, AfterCardPlayedMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    public static void InvokeLate(AbstractModel listener, AfterCardPlayedMirrorContext context)
    {
        LateRegistry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterCardPlayed);

        registry.RegisterIgnored<ArtOfWar>();
        registry.RegisterIgnored<Pocketwatch>();
        registry.RegisterIgnored<RippleBasin>();
        registry.RegisterIgnored<EchoFormPower>();
        registry.RegisterIgnored<PaleBlueDotPower>();
        registry.RegisterIgnored<Play20CardsSingleTurnAchievement>();
        registry.RegisterIgnored<SkillSilent1Achievement>();
        registry.RegisterIgnored<CccComboModel>();

        return registry;
    }
}

internal sealed class AfterCardPlayedMirrorContext : CombatPredictionCardMirrorContext
{
    public required CardPlay CardPlay { get; init; }
}
