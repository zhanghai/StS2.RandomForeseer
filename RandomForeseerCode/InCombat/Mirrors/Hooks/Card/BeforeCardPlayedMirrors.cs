using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Achievements;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

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

        registry.Register<Stomp>(HandleStomp);

        registry.Register<AfterimagePower>(HandleAfterimagePower);
        registry.Register<CalamityPower>(HandleCalamityPower);
        registry.Register<DanseMacabrePower>(HandleDanseMacabrePower);
        registry.Register<GravityPower>(HandleGravityPower);
        registry.Register<JugglingPower>(HandleJugglingPower);
        registry.Register<SerpentFormPower>(HandleSerpentFormPower);
        registry.Register<SpiritOfAshPower>(HandleSpiritOfAshPower);
        registry.Register<StormPower>(HandleStormPower);
        registry.Register<StranglePower>(HandleStranglePower);
        registry.Register<SubroutinePower>(HandleSubroutinePower);
        registry.Register<TheSealedThronePower>(HandleTheSealedThronePower);

        registry.RegisterIgnored<ChemicalX>();
        registry.Register<IntimidatingHelmet>(HandleIntimidatingHelmet);
        registry.Register<MusicBox>(HandleMusicBox);
        registry.RegisterIgnored<PaelsEye>();

        return registry;
    }

    private static void HandleStomp(Stomp stomp, BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == stomp.Owner &&
            context.PreviewCard.Type == CardType.Attack &&
            context.State.FindCard(stomp) is { } predictedStomp)
        {
            predictedStomp.MutablePreview.EnergyCost.AddThisTurn(-1);
        }
    }

    private static void HandleAfterimagePower(AfterimagePower power, BeforeCardPlayedMirrorContext context)
    {
        SnapshotOwnerCard(power, context, power.Amount);
    }

    private static void HandleCalamityPower(CalamityPower power, BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Type == CardType.Attack)
        {
            SnapshotOwnerCard(power, context, power.Amount);
        }
    }

    private static void HandleDanseMacabrePower(DanseMacabrePower power, BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner.Creature == power.Owner &&
            context.CardPlay.Resources.EnergyValue >= power.DynamicVars.Energy.IntValue)
        {
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleGravityPower(GravityPower power, BeforeCardPlayedMirrorContext context)
    {
        SnapshotOwnerCard(power, context, power.Amount);
    }

    private static void HandleJugglingPower(JugglingPower power, BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner != power.Owner?.Player || context.PreviewCard.Type != CardType.Attack)
        {
            return;
        }

        var state = context.StateStore.Get(power, () => new JugglingPredictionState(power));
        state.AttacksPlayedThisTurn++;
        if (state.AttacksPlayedThisTurn != 3)
        {
            return;
        }

        for (var i = 0; i < power.Amount; i++)
        {
            context.Simulator.AddGeneratedCardToCombat(
                context.Card.CreateClone(),
                PileType.Hand,
                power.Owner.Player,
                resultKind: CardGenerationResultKind.Contextual);
        }
    }

    private static void HandleSerpentFormPower(SerpentFormPower power, BeforeCardPlayedMirrorContext context)
    {
        SnapshotOwnerCard(power, context, power.Amount);
    }

    private static void HandleSpiritOfAshPower(SpiritOfAshPower power, BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == power.Owner?.Player &&
            context.Card.GetKeywords(context.State).Contains(CardKeyword.Ethereal))
        {
            context.Simulator.GainBlock(power.Owner, power.Amount, ValueProp.Unpowered);
        }
    }

    private static void HandleStormPower(StormPower power, BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Type == CardType.Power)
        {
            SnapshotOwnerCard(power, context, power.Amount);
        }
    }

    private static void HandleStranglePower(StranglePower power, BeforeCardPlayedMirrorContext context)
    {
        if (power.Applier?.Player == context.PreviewCard.Owner)
        {
            GetPairState(power, context).Amounts.Add(context.CardPlay, power.Amount);
        }
    }

    private static void HandleSubroutinePower(SubroutinePower power, BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Type == CardType.Power)
        {
            SnapshotOwnerCard(power, context, power.Amount);
        }
    }

    private static void HandleTheSealedThronePower(
        TheSealedThronePower power,
        BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == power.Owner?.Player)
        {
            context.State.GetPlayerCombatState(context.PreviewCard.Owner).GainStars(power.Amount);
        }
    }

    private static void HandleIntimidatingHelmet(
        IntimidatingHelmet relic,
        BeforeCardPlayedMirrorContext context)
    {
        if (context.PreviewCard.Owner == relic.Owner &&
            context.CardPlay.Resources.EnergyValue >= relic.DynamicVars.Energy.IntValue)
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
        }
    }

    private static void HandleMusicBox(MusicBox relic, BeforeCardPlayedMirrorContext context)
    {
        var state = context.StateStore.Get(relic, () => new MusicBoxPredictionState(relic));
        if (state.CardBeingPlayed is null &&
            !state.WasUsedThisTurn &&
            context.PreviewCard.Owner == relic.Owner &&
            context.PreviewCard.Type == CardType.Attack)
        {
            state.CardBeingPlayed = context.CardPlay;
        }
    }

    private static void SnapshotOwnerCard(
        PowerModel power,
        BeforeCardPlayedMirrorContext context,
        int amount)
    {
        if (context.PreviewCard.Owner == power.Owner?.Player)
        {
            GetPairState(power, context).Amounts.Add(context.CardPlay, amount);
        }
    }

    private static CardPlayPairPredictionState GetPairState(
        AbstractModel model,
        BeforeCardPlayedMirrorContext context)
    {
        return context.StateStore.Get<CardPlayPairPredictionState>(model);
    }
}

internal sealed class BeforeCardPlayedMirrorContext : CombatPredictionCardMirrorContext
{
    public required CardPlay CardPlay { get; init; }
}

internal sealed class CardPlayPairPredictionState
{
    public Dictionary<CardPlay, int> Amounts { get; } = new(ReferenceEqualityComparer.Instance);
}

internal sealed class JugglingPredictionState(JugglingPower power)
{
    public int AttacksPlayedThisTurn { get; set; } =
        power.GetInternalData<JugglingPower.Data>().attacksPlayedThisTurn;
}

internal sealed class MusicBoxPredictionState(MusicBox relic)
{
    public bool WasUsedThisTurn { get; set; } = relic._wasUsedThisTurn;

    public CardPlay? CardBeingPlayed { get; set; }
}
