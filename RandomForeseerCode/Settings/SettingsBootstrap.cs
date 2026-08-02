using RandomForeseer.RandomForeseerCode.Localization;
using STS2RitsuLib;
using STS2RitsuLib.Settings;

namespace RandomForeseer.RandomForeseerCode.Settings;

internal static class SettingsBootstrap
{
    public static void Register()
    {
        RegisterMainPage();
        RegisterOutOfCombatSettingsPage();
        RegisterInCombatSettingsPage();
        RegisterDebugPage();
    }

    private static void RegisterMainPage()
    {
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .WithModDisplayName(T("mod.name"))
            .WithTitle(T("page.title"))
            .WithDescription(T("page.description"))
            .WithSortOrder(0)
            .AddSection("general_prediction", section => section
                .WithTitle(T("section.general_prediction.title"))
                .WithDescription(T("section.general_prediction.description"))
                .AddToggle("singleplayer_prediction_enabled", SettingsUiBindings.SingleplayerPredictionEnabled)
                .AddToggle("multiplayer_prediction_enabled", SettingsUiBindings.MultiplayerPredictionEnabled)
                .AddToggle("fair_mode_enabled", SettingsUiBindings.FairModeEnabled)
                .AddToggle("show_drift_warnings", SettingsUiBindings.ShowDriftWarnings))
            .AddSection("prediction_page_navigation", section => section
                .WithTitle(T("section.prediction_page_navigation.title"))
                .WithDescription(T("section.prediction_page_navigation.description"))
                .AddSubPage("out_of_combat_prediction")
                .AddSubPage("in_combat_prediction")));
    }

    private static void RegisterOutOfCombatSettingsPage()
    {
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .AsChildOf(Entry.ModId)
            .WithTitle(T("page.out_of_combat_prediction.title"))
            .WithDescription(T("page.out_of_combat_prediction.description"))
            .WithSortOrder(0)
            .AddSection("out_of_combat_prediction", section => section
                .WithTitle(T("page.out_of_combat_prediction.title"))
                .AddToggle("deck_transform_prediction_enabled", SettingsUiBindings.DeckTransformPredictionEnabled)
                .AddToggle("relic_pickup_prediction_enabled", SettingsUiBindings.RelicPickupPredictionEnabled)
                .AddToggle("event_option_prediction_enabled", SettingsUiBindings.EventOptionPredictionEnabled)
                .AddIntSlider(
                    "slippery_bridge_reroll_preview_count",
                    T("slider.slippery_bridge_reroll_preview_count.label"),
                    SettingsUiBindings.SlipperyBridgeRerollPreviewCount,
                    minValue: 1,
                    maxValue: 10,
                    description: T("slider.slippery_bridge_reroll_preview_count.description"))
                .WithEntryEnabledWhen(
                    "slippery_bridge_reroll_preview_count",
                    () => SettingsUiBindings.EventOptionPredictionEnabled.Read())
                .AddToggle("crystal_sphere_clairvoyance_enabled", SettingsUiBindings.CrystalSphereClairvoyanceEnabled)
                .AddToggle("driftwood_reroll_prediction_enabled", SettingsUiBindings.DriftwoodRerollPredictionEnabled)
                .AddToggle("paels_wing_sacrifice_prediction_enabled", SettingsUiBindings.PaelsWingSacrificePredictionEnabled)
                .AddToggle("rest_site_prediction_enabled", SettingsUiBindings.RestSitePredictionEnabled)
                .AddToggle("next_act_prediction_enabled", SettingsUiBindings.NextActPredictionEnabled)),
            "out_of_combat_prediction");
    }

    private static void RegisterInCombatSettingsPage()
    {
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .AsChildOf(Entry.ModId)
            .WithTitle(T("page.in_combat_prediction.title"))
            .WithDescription(T("page.in_combat_prediction.description"))
            .WithSortOrder(1)
            .AddSection("prediction_sources", section => section
                .WithTitle(T("section.prediction_sources.title"))
                .WithDescription(T("section.prediction_sources.description"))
                .AddHeader("card_and_potion_predictions")
                .AddToggle("card_play_prediction_enabled", SettingsUiBindings.CardPlayPredictionEnabled)
                .AddToggle("potion_prediction_enabled", SettingsUiBindings.PotionPredictionEnabled)
                .AddToggle("combat_transform_prediction_enabled", SettingsUiBindings.CombatTransformPredictionEnabled)
                .AddHeader("end_turn_predictions")
                .AddToggle("end_turn_prediction_enabled", SettingsUiBindings.EndTurnPredictionEnabled)
                .AddEnumChoice("end_turn_prediction_display_mode", SettingsUiBindings.EndTurnPredictionDisplayMode)
                .AddEnumChoice("end_turn_health_bar_forecast_display_mode", SettingsUiBindings.EndTurnHealthBarForecastDisplayMode)
                .WithEntryEnabledWhen(
                    "end_turn_prediction_display_mode",
                    () => SettingsUiBindings.EndTurnPredictionEnabled.Read())
                .WithEntryEnabledWhen(
                    "end_turn_health_bar_forecast_display_mode",
                    () => SettingsUiBindings.EndTurnPredictionEnabled.Read())
                .AddHeader("frozen_eye_predictions")
                .AddToggle("frozen_eye_enabled", SettingsUiBindings.FrozenEyeEnabled)
                .AddToggle("shuffle_prediction_enabled", SettingsUiBindings.ShufflePredictionEnabled)
                .WithEntryEnabledWhen(
                    "shuffle_prediction_enabled",
                    () => SettingsUiBindings.FrozenEyeEnabled.Read()))
            .AddSection("card_effect_prediction", section => section
                .WithTitle(T("section.card_effect_prediction.title"))
                .WithDescription(T("section.card_effect_prediction.description"))
                .WithEnabledWhen(() => SettingsUiBindings.CardPlayPredictionEnabled.Read())
                .AddToggle("combat_card_generation_prediction_enabled", SettingsUiBindings.CombatCardGenerationPredictionEnabled)
                .AddToggle("card_draw_prediction_enabled", SettingsUiBindings.CardDrawPredictionEnabled)
                .AddToggle("combat_card_selection_prediction_enabled", SettingsUiBindings.CombatCardSelectionPredictionEnabled)
                .AddToggle("combat_orb_generation_prediction_enabled", SettingsUiBindings.CombatOrbGenerationPredictionEnabled))
            .AddSection("potion_effect_prediction", section => section
                .WithTitle(T("section.potion_effect_prediction.title"))
                .WithDescription(T("section.potion_effect_prediction.description"))
                .WithEnabledWhen(() => SettingsUiBindings.PotionPredictionEnabled.Read())
                .AddToggle("potion_card_generation_prediction_enabled", SettingsUiBindings.PotionCardGenerationPredictionEnabled)
                .AddToggle("potion_draw_prediction_enabled", SettingsUiBindings.PotionDrawPredictionEnabled))
            .AddSection("shared_prediction", section => section
                .WithTitle(T("section.shared_prediction.title"))
                .WithDescription(T("section.shared_prediction.description"))
                .WithEnabledWhen(() =>
                    SettingsUiBindings.CardPlayPredictionEnabled.Read() ||
                    SettingsUiBindings.PotionPredictionEnabled.Read())
                .AddToggle("potion_generation_prediction_enabled", SettingsUiBindings.PotionGenerationPredictionEnabled)
                .AddToggle("auto_play_from_draw_pile_prediction_enabled", SettingsUiBindings.AutoPlayFromDrawPilePredictionEnabled))
            .AddSection("damage_prediction", section => section
                .WithTitle(T("section.damage_prediction.title"))
                .WithDescription(T("section.damage_prediction.description"))
                .AddToggle("combat_damage_prediction_enabled", SettingsUiBindings.CombatDamagePredictionEnabled)
                .AddToggle("orb_damage_prediction_enabled", SettingsUiBindings.OrbDamagePredictionEnabled)
                .AddToggle("random_target_attack_prediction_enabled", SettingsUiBindings.RandomTargetAttackPredictionEnabled)
                .AddColor(
                    "damage_prediction_health_bar_color",
                    T("color.damage_prediction_health_bar_color.label"),
                    SettingsUiBindings.DamagePredictionHealthBarColor,
                    T("color.damage_prediction_health_bar_color.description"),
                    editAlpha: true,
                    editIntensity: false)
                .WithEntryEnabledWhen(
                    "orb_damage_prediction_enabled",
                    () => SettingsUiBindings.CombatDamagePredictionEnabled.Read())
                .WithEntryEnabledWhen(
                    "random_target_attack_prediction_enabled",
                    () => SettingsUiBindings.CombatDamagePredictionEnabled.Read())
                .WithEntryEnabledWhen(
                    "damage_prediction_health_bar_color",
                    () => SettingsUiBindings.CombatDamagePredictionEnabled.Read()))
            .AddSection("experimental_features", section => section
                .WithTitle(T("section.experimental_features.title"))
                .WithDescription(T("section.experimental_features.description"))
                .Collapsible(startCollapsed: true)
                .AddToggle(
                    "experimental_best_effort_card_play_prediction_enabled",
                    SettingsUiBindings.ExperimentalBestEffortCardPlayPredictionEnabled)
                .AddToggle(
                    "experimental_chained_card_effect_prediction_enabled",
                    SettingsUiBindings.ExperimentalChainedCardEffectPredictionEnabled)
                .WithEntryEnabledWhen(
                    "experimental_best_effort_card_play_prediction_enabled",
                    () => SettingsUiBindings.CardPlayPredictionEnabled.Read())
                .WithEntryEnabledWhen(
                    "experimental_chained_card_effect_prediction_enabled",
                    () =>
                        SettingsUiBindings.CardPlayPredictionEnabled.Read() ||
                        SettingsUiBindings.PotionPredictionEnabled.Read())),
            "in_combat_prediction");
    }

    private static void RegisterDebugPage()
    {
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page => page
            .WithTitle(T("page.debug.title"))
            .WithDescription(T("page.debug.description"))
            .WithSortOrder(1)
            .WithVisibleWhen(() => SettingsUiBindings.DebugSettingsEnabled.Read())
            .AddSection("ancient_event_debug", section => section
                .WithTitle(T("section.ancient_event_debug.title"))
                .WithDescription(T("section.ancient_event_debug.description"))
                .AddToggle("ancient_event_debug_reroll_enabled", SettingsUiBindings.AncientEventDebugRerollEnabled))
            .AddSection("relic_pickup_debug", section => section
                .WithTitle(T("section.relic_pickup_debug.title"))
                .WithDescription(T("section.relic_pickup_debug.description"))
                .AddButton(
                    "offer_predicted_non_ancient_relics",
                    Debug.RelicPickupDebugRewards.OfferPredictedNonAncientRelics,
                    ModSettingsButtonTone.Danger)
                .AddButton(
                    "open_predicted_treasure_room",
                    Debug.RelicPickupDebugRewards.OpenPredictedTreasureRoom,
                    ModSettingsButtonTone.Danger)
                .AddButton(
                    "open_relic_trader_pickup_test",
                    Debug.RelicPickupDebugRewards.OpenRelicTraderPickupTest,
                    ModSettingsButtonTone.Danger)),
            "debug");
    }

    private static ModSettingsText T(string key)
    {
        return ModSettingsText.I18N(ModLocalization.SettingsLocalization, key, key);
    }

    private static ModSettingsSectionBuilder AddToggle(
        this ModSettingsSectionBuilder section,
        string id,
        IModSettingsValueBinding<bool> binding)
    {
        return section.AddToggle(id, T($"toggle.{id}.label"), binding, T($"toggle.{id}.description"));
    }

    private static ModSettingsSectionBuilder AddSubPage(this ModSettingsSectionBuilder section, string pageId)
    {
        return section.AddSubpage(
            $"open_{pageId}_page",
            T($"page.{pageId}.title"),
            pageId,
            T("button.open_page.text"),
            T($"page.{pageId}.description"));
    }

    private static ModSettingsSectionBuilder AddHeader(this ModSettingsSectionBuilder section, string id)
    {
        return section.AddHeader(id, T($"header.{id}.label"));
    }

    private static ModSettingsSectionBuilder AddEnumChoice<TValue>(
        this ModSettingsSectionBuilder section,
        string id,
        IModSettingsValueBinding<TValue> binding,
        ModSettingsChoicePresentation presentation = ModSettingsChoicePresentation.Dropdown)
        where TValue : struct, Enum
    {
        return section.AddEnumChoice(
            id,
            T($"choice.{id}.label"),
            binding,
            value => T($"choice.{id}.option.{value}"),
            T($"choice.{id}.description"),
            presentation);
    }

    private static ModSettingsSectionBuilder AddButton(
        this ModSettingsSectionBuilder section,
        string id,
        Action action,
        ModSettingsButtonTone tone = ModSettingsButtonTone.Normal)
    {
        return section.AddButton(
            id,
            T($"button.{id}.label"),
            T($"button.{id}.text"),
            action,
            tone,
            T($"button.{id}.description"));
    }
}
