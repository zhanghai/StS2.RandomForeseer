# Card play hooks

Research baseline: StS2 v0.110.1 (`a421e19`).

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/BeforeCardPlayedMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/AfterCardPlayedMirrors.cs`,
`InCombat/Simulation/CombatPredictionHistory.cs`, and
`InCombat/Simulation/CombatPredictionSimulator.Card.cs`.

The simulator now dispatches the paired hook lifecycle in vanilla order and records both shadow
`CardPlayStarted` and `CardPlayFinished` entries. Exact gameplay listener coverage is being added in the
implementation slices below; reviewed achievement/VFX/later-turn-only listeners are already registered ignored,
while every other unregistered override remains an explicit unsupported risk rather than a silent no-op.

## Hook specs

- `AbstractModel.BeforeCardPlayed(CardPlay)`
- `AbstractModel.AfterCardPlayed(PlayerChoiceContext, CardPlay)`
- `AbstractModel.AfterCardPlayedLate(PlayerChoiceContext, CardPlay)`

`Hook.AfterCardPlayed` is the facade for both after phases, so this document treats
`AfterCardPlayedLate` as part of the same hook family.

## Vanilla order and dispatch semantics

For every generated play index, `CardModel.OnPlayWrapper` does the following:

1. Constructs a new `CardPlay` with the current `PlayIndex` and shared `PlayCount`.
2. Dispatches `Hook.BeforeCardPlayed` through the guarded combat-listener iterator.
3. Records `CardPlayStarted`.
4. Runs `CardModel.OnPlay`, then the played card's enchantment and affliction effects, with an owner-death early return
   after each stage.
5. If the owner is still alive, records `CardPlayFinished`.
6. If combat is still in progress, calls `Hook.AfterCardPlayed`.
7. The facade directly iterates all combat listeners once for `AfterCardPlayed`, then starts a fresh direct
   iteration for `AfterCardPlayedLate`.

The before hook is suppressed when combat is already over or ending at dispatch start. The after facade deliberately
bypasses that guard so listeners can finish resolving the card that caused a kill. It pushes each listener into the
`PlayerChoiceContext`; the before hook does not use a choice context.

The simulator preserves one before/ordinary-after/late-after cycle per play index, including replayed cards, and runs
the ordinary phase for every listener before beginning the fresh late pass. The guarded before phase also suppresses
dispatch after the shadow combat has ended; both after passes use direct listener iteration.

## Feasibility labels

- **Local feasible**: implementable with current simulator state, cloned RNG, card helpers, and optionally
  `PredictionStateStore`.
- **Cross-hook feasible**: also requires a prediction-aware value or predicate hook; mutating only local card-play
  state would not change the original hook's live-state read.
- **Partial / blocked**: part of the effect is modelable, but exact behavior needs an unsupported state domain or an
  independently missing model mirror.
- **Ignorable**: no state relevant to the current-player-turn prediction surface; use an explicit no-op registration.

All rows below are currently unmirrored. The disposition is an implementation recommendation, not current coverage.

## BeforeCardPlayed listeners

| Model | 中文名 | Original effect | Research disposition |
| --- | --- | --- | --- |
| `SkillSilent1Achievement` | 成就模型 | Remembers the first local card on the current play stack for achievement bookkeeping. | **Ignorable.** Achievement state has no prediction effect. |
| `Stomp` | 踩踏 | Whenever owner plays an Attack, reduces this card's this-turn cost by 1. | **Local feasible.** Find the predicted listener card and mutate only its preview cost. Live plus shadow card-play history is already an established pattern. |
| `AfterimagePower` | 余像 | Snapshots the power amount for each owner card so the matching after hook grants that much block. | **Local feasible.** Store the amount by `CardPlay`/predicted card identity in `StateStore`. |
| `CalamityPower` | 劫难 | Snapshots the amount when owner plays an Attack; the matching after hook generates that many random Attacks. | **Local feasible.** Pair state locally; existing combat card-generation helpers and cloned `CombatCardGeneration` cover the result. |
| `ChainsOfBindingPower` | 魂缚锁链 | Marks that owner has played a non-dupe Bound card; `ShouldPlay` then prevents another Bound card this turn. | **Cross-hook feasible.** `AutoPlay` currently calls original `Hook.ShouldPlay`, so the predicate must read the same shadow flag. |
| `DanseMacabrePower` | 死亡之舞 | If owner spends at least the configured energy, grants block before the card effect. | **Local feasible.** `CardPlay.Resources` and simulator `GainBlock` are sufficient. |
| `FreeAttackPower` | 免费攻击 | Consumes one stack when owner plays an Attack from hand/play. | **Cross-hook feasible.** Track shadow amount and make prediction cost resolution read it; general power removal is unnecessary for this targeted mirror. |
| `FreePowerPower` | 免费能力 | Consumes one stack when owner plays a Power from hand/play. | **Cross-hook feasible.** Same targeted shadow-cost requirement as `FreeAttackPower`. |
| `FreeSkillPower` | 免费技能 | Consumes one stack when owner plays a Skill from hand/play. | **Cross-hook feasible.** Same targeted shadow-cost requirement as `FreeAttackPower`. |
| `GravityPower` | 引力 | Snapshots the power amount for each owner card so the matching after hook damages all hittable enemies. | **Local feasible.** Pair state plus simulator `Damage`. |
| `ImitationLearningPower` | 模仿学习 | On the first play in a series of the selected ally's Power, creates an owner-swapped clone for later auto-play. | **Partial.** Detached clone creation and generic `AutoPlay` are available, but the clone's Power `OnPlay` may still be unsupported and Apply Power remains outside simulator state. |
| `JugglingPower` | 杂耍 | Counts owner Attacks; on the third, adds `Amount` clones of that Attack to hand. | **Local feasible.** Use `StateStore`, `PredictedCard.CreateClone`, `Fixed` generation classification, and normal generation hooks. |
| `MonologuePower` | 独白 | Snapshots the configured Strength amount for each owner card; the after hook applies it to the monster. | **Blocked.** Apply Power is unsupported. Register risk on the matching trigger. |
| `OblivionPower` | 湮灭 | Snapshots amount for each applier card; the after hook applies Doom to the owner. | **Blocked.** Apply Power/death consequences are unsupported. |
| `RupturePower` | 撕裂 | Opens a per-card accumulator so HP loss caused during that card is converted to Strength after the play. | **Blocked.** Pairing with the existing damage listener is possible, but the resulting Strength application is unsupported. Preserve the current explicit risk. |
| `SerpentFormPower` | 群蛇形态 | Snapshots amount for each owner card; the after hook damages one random hittable enemy. | **Local feasible.** Pair state, cloned `CombatTargets`, and simulator `Damage` cover it. |
| `SlothPower` | 懒惰 | Increments the owner's cards-played counter before the card effect; `ShouldPlay` enforces the cap. | **Cross-hook feasible.** Required for nested auto-plays because original `Hook.ShouldPlay` sees only the live counter. |
| `SpiritOfAshPower` | 灰烬之灵 | Owner gains block before playing an Ethereal card. | **Local feasible.** Use predicted keywords and simulator `GainBlock`. |
| `StormPower` | 雷暴 | Snapshots amount when owner plays a Power; the after hook channels that many Lightning orbs. | **Local feasible.** Pair state plus simulator `OrbChannel`. |
| `StranglePower` | 紧勒 | Snapshots amount for each applier card; the after hook deals unblockable damage to the power owner. | **Local feasible.** Pair state plus simulator `Damage`. |
| `SubroutinePower` | 子程序 | Snapshots amount when owner plays a Power; the after hook grants that much energy. | **Local feasible.** Pair state plus simulator `GainEnergy`. |
| `SurroundedPower` | 遭到包围 | Before owner's targeted card resolves, turns the power owner and pets toward that target, changing subsequent back-attack damage against the owner. | **Cross-hook feasible.** Store shadow facing so any retaliation/reaction inside the predicted chain uses it in `ModifyDamageMultiplicative`; visual flipping/music are ignored. |
| `TheSealedThronePower` | 封印王座 | Grants owner stars before every owner card effect. | **Local feasible.** Simulator player state already owns stars. |
| `VeilpiercerPower` | 刺破帷幕 | Consumes one stack when owner plays an Ethereal card from hand/play. | **Cross-hook feasible.** Shadow amount must also feed its zero-cost modifier. |
| `ChemicalX` | 化学物X | Flashes when owner plays an energy-X or star-X card. | **Ignorable.** The actual X increase is the separate read-only `ModifyXValue` hook. |
| `IntimidatingHelmet` | 骇人头盔 | If owner spends at least the configured energy, grants block before the card effect. | **Local feasible.** `CardPlay.Resources` and simulator `GainBlock` are sufficient. |
| `MusicBox` | 音乐盒 | Remembers the first owner Attack this turn so the matching after hook can clone it with Ethereal. | **Local feasible.** Pair state, preview cloning, keyword mutation, and `Fixed` generated-card flow are available. |
| `PaelsEye` | 佩尔之眼 | Changes relic display status when owner manually plays a card before the relic has triggered. | **Ignorable.** Gameplay uses combat history/extra-turn hooks, not this status mutation. |
| `PenNib` | 钢笔尖 | Advances the Attack counter and marks every tenth owner Attack as the card whose damage is doubled. | **Cross-hook feasible and high priority.** The current original damage modifier reads live fields, so it needs shadow counter/card identity and a prediction-aware multiplier path. |

## AfterCardPlayed listeners: relics

| Model | 中文名 | Original effect | Research disposition |
| --- | --- | --- | --- |
| `ArtOfWar` | 孙子兵法 | Records that owner played an Attack, affecting next-turn energy. | **Ignorable.** Only a later turn is affected. |
| `BrilliantScarf` | 艳丽围巾 | Counts manual owner cards so the fifth card's energy and star costs are zero. | **Cross-hook feasible.** Track the counter and make prediction cost helpers read it for a later nested card. |
| `DaughterOfTheWind` | 风的女儿 | Grants block after every owner Attack. | **Local feasible.** Use simulator `GainBlock`. |
| `GamePiece` | 棋子 | Draws cards after owner plays a Power. | **Local feasible.** Use simulator `Draw`. |
| `HelicalDart` | 螺线飞镖 | Playing an owner Shiv applies Dexterity. | **Blocked.** Apply Power is unsupported and can affect later block in the same turn. |
| `IronClub` | 铁棒 | Counts owner cards and draws one every configured interval. | **Local feasible.** Initialize a `StateStore` counter from live state and use simulator `Draw`. |
| `IvoryTile` | 象牙麻将牌 | If the card spent enough energy, grants energy. | **Local feasible.** `CardPlay.Resources` and simulator `GainEnergy` are sufficient. |
| `Kunai` | 苦无 | Every configured number of owner Attacks applies Dexterity. | **Blocked.** Counter tracking is easy, but Apply Power is unsupported. |
| `Kusarigama` | 锁镰 | Every configured number of owner Attacks damages one random hittable enemy. | **Local feasible.** Use shadow counter, cloned `CombatTargets`, and simulator `Damage`. |
| `LetterOpener` | 开信刀 | Every configured number of owner Skills damages all hittable enemies. | **Local feasible.** Use shadow counter and simulator `Damage`. |
| `LostWisp` | 迷失鬼火 | Owner Power cards damage all hittable enemies. | **Local feasible.** Use simulator `Damage`. |
| `MummifiedHand` | 干瘪之手 | After owner plays a Power, selects a random card in hand through four cost-based fallback pools and makes it free this turn. | **Local feasible.** Use shadow hand, cloned `CombatCardSelection`, and prediction cost helpers; never call live card cost/state APIs. |
| `MusicBox` | 音乐盒 | Clones the remembered Attack into hand, adds Ethereal, and consumes the once-per-turn trigger. | **Local feasible.** Complete the paired `StateStore` transaction and use normal generated-card flow. |
| `Nunchaku` | 双截棍 | Every configured number of owner Attacks grants energy. | **Local feasible.** Shadow counter plus simulator `GainEnergy`. |
| `OrnamentalFan` | 精致折扇 | Every configured number of owner Attacks grants block. | **Local feasible.** Shadow counter plus simulator `GainBlock`. |
| `PaelsLegion` | 佩尔的士兵 | After the card whose block was doubled finishes, starts the relic cooldown and consumes the doubling trigger. | **Cross-hook feasible.** Shadow `ModifyBlock`/`AfterModifyingBlockAmount` state must share the same `CardPlay` identity with this hook. Pet animation is ignored. |
| `PenNib` | 钢笔尖 | Clears the current doubled-Attack marker after that card resolves. | **Cross-hook feasible.** Must share the shadow state used by the damage multiplier. |
| `Permafrost` | 永冻冰晶 | The first owner Power each combat grants block and consumes the trigger. | **Local feasible.** State-store once/combat flag plus simulator `GainBlock`. |
| `Pocketwatch` | 怀表 | Counts owner cards for next-turn hand draw. | **Ignorable.** Only a later turn is affected. |
| `RainbowRing` | 彩虹戒指 | After owner has played Attack, Skill, and Power this turn, applies Strength and Dexterity once. | **Blocked.** Counters are easy; both Apply Power results are unsupported. |
| `RazorTooth` | 剃刀牙 | Upgrades an upgradable owner Skill or Power after it is played. | **Local feasible.** Upgrade only the detached predicted card; never mutate the live/deck card. |
| `RippleBasin` | 波纹水盆 | Changes display status after an owner Attack. | **Ignorable.** End-turn gameplay checks card-play history directly; the status is cosmetic. |
| `Shuriken` | 手里剑 | Every configured number of owner Attacks applies Strength. | **Blocked.** Counter tracking is easy, but Apply Power is unsupported. |
| `TuningFork` | 音叉 | Counts owner Skills persistently and grants block at each threshold. | **Local feasible.** Initialize the shadow counter from `SkillsPlayed`; use simulator `GainBlock`. |
| `UnsettlingLamp` | 不安油灯 | Marks its one-combat debuff-doubling trigger finished after the triggering card. | **Partial / defer.** The state commit is feasible, but it only matters with power-application hooks, which are unsupported. |
| `Vambrace` | 臂甲 | After the card whose block was doubled finishes, consumes the once-per-combat doubling. | **Cross-hook feasible.** Shadow block multiplier/commit state must replace the current live-state read for chained block gains. |
| `VelvetChoker` | 天鹅绒颈圈 | Increments the owner's cards-played counter used by `ShouldPlay`. | **Cross-hook feasible.** Nested auto-play gating must read the shadow counter. |

## AfterCardPlayed listeners: powers

| Model | 中文名 | Original effect | Research disposition |
| --- | --- | --- | --- |
| `AfterimagePower` | 余像 | Consumes the paired snapshot and grants owner block. | **Local feasible.** Complete the before/after pair with simulator `GainBlock`. |
| `BlackHolePower` | 黑洞 | On the last play in a series, a card that spent stars damages all enemies. | **Local feasible.** `CardPlay.Resources`, series metadata, and simulator `Damage` are available. |
| `CalamityPower` | 劫难 | Consumes the paired snapshot and generates random Attack cards into hand. | **Local feasible.** Existing safe `GetForCombat` prediction helpers reproduce the pool and cloned RNG without adding cards to live state. |
| `CurlUpPower` | 蜷身 | After the damaging card finishes, grants block, marks the louse curled, and removes this power. | **Partial.** Block is directly simulatable; monster state and Remove Power are unsupported, so record risk for the remainder. |
| `DevourLifePower` | 吞噬生命 | Playing an owner Soul summons Osty. | **Blocked.** Summon/pet state is unsupported. |
| `EchoFormPower` | 回响形态 | Updates VFX after card-play-started history reaches the replay limit. | **Ignorable.** Replay count is handled by separate play-count hooks; this body is visual only. |
| `EnragePower` | 激怒 | Whenever a Skill is played, applies Strength to the power owner. | **Blocked.** Apply Power is unsupported. |
| `GalvanicPower` | 流电 | A Galvanized card deals move damage to its owner after play. | **Local feasible.** Read predicted affliction and use simulator `Damage`. |
| `GravityPower` | 引力 | Consumes the paired snapshot and damages all hittable enemies. | **Local feasible.** Use simulator `Damage`. |
| `HauntPower` | 纠缠 | An owner Soul damages one random hittable enemy. | **Local feasible.** Cloned `CombatTargets` plus simulator `Damage`. |
| `ImitationLearningPower` | 模仿学习 | Consumes the paired clone, decrements the power, and auto-plays the cloned ally Power. | **Partial.** Generic auto-play is available, but power decrement and the cloned Power's `OnPlay`/Apply Power may remain unsupported. |
| `MasterPlannerPower` | 谋划专家 | Adds Sly to every owner Skill after it resolves. | **Local feasible.** Mutate only predicted keywords so later shadow plays see it. |
| `MonologuePower` | 独白 | Applies the paired Strength amount to the monster and updates its accumulator. | **Blocked.** Apply Power is unsupported. |
| `OblivionPower` | 湮灭 | Applies the paired Doom amount to the owner. | **Blocked.** Apply Power/death state is unsupported. |
| `PaleBlueDotPower` | 暗淡蓝点 | On the fifth owner card this turn, applies next-turn draw once. | **Ignorable for current scope.** The result cannot affect the current player turn. |
| `PanachePower` | 神气制胜 | Counts owner cards and damages all enemies every five cards. | **Local feasible.** State-store counter plus simulator `Damage`. |
| `RagePower` | 狂怒 | Grants block after every owner Attack. | **Local feasible.** Use simulator `GainBlock`. |
| `RupturePower` | 撕裂 | Converts the paired during-card HP-loss accumulator into Strength. | **Blocked.** Apply Power is unsupported; retain explicit risk from the damage mirror. |
| `SerpentFormPower` | 群蛇形态 | Consumes the paired snapshot and damages one random hittable enemy. | **Local feasible.** Cloned `CombatTargets` plus simulator `Damage`. |
| `SlowPower` | 缓慢 | Increments the damage multiplier by 10 percentage points after every card. | **Cross-hook feasible and high priority for chains.** `ModifyDamageMultiplicative` must read shadow amount instead of the live dynamic var. |
| `SmoggyPower` | 烟雾弥漫 | After owner plays a Skill, afflicts every unafflicted owner Skill with Smog. | **Local feasible.** Iterate shadow piles and use simulator `Afflict`; do not call live card APIs. |
| `SneakyPower` | 鬼祟 | Whenever another creature's Attack is played, grants block to the power owner. | **Local feasible.** Use simulator `GainBlock`. |
| `StormPower` | 雷暴 | Consumes the paired snapshot and channels Lightning orbs. | **Local feasible.** Use simulator `OrbChannel`. |
| `StranglePower` | 紧勒 | Consumes the paired snapshot and deals unblockable damage to the power owner. | **Local feasible.** Use simulator `Damage`. |
| `SubroutinePower` | 子程序 | Consumes the paired snapshot and grants owner energy. | **Local feasible.** Use simulator `GainEnergy`. |
| `TenderPower` | 柔嫩 | After every owner card, applies negative Strength and Dexterity to owner. | **Blocked.** Apply Power and shadow power amounts are unsupported. |
| `VitalSparkPower` | 活力火花 | A Tainted card applies TaintedPower to its owner after play. | **Blocked.** Apply Power is unsupported. |
| `VoidFormPower` | 虚空形态 | Counts non-auto owner cards on the last play in a series; later cards stop being free at the power amount. | **Cross-hook feasible.** Shadow count must feed both energy- and star-cost prediction helpers. |
| `WitheringPresencePower` | 凋萎存在 | Counts target-player cards and adds a Wither to hand every six. | **Local feasible.** State-store counter plus fixed generated-card flow. |

## AfterCardPlayed listeners: cards, enchantments, and bookkeeping

| Model | 中文名 | Original effect | Research disposition |
| --- | --- | --- | --- |
| `BansheesCry` | 女妖之嚎 | Whenever owner plays an Ethereal card, reduces this listener card's this-combat cost. | **Local feasible.** Find the predicted listener card and mutate only its preview cost. |
| `Pinpoint` | 精密瞄准 | Whenever owner plays a Skill, reduces this listener card's this-turn cost. | **Local feasible.** Find the predicted listener card and mutate only its preview cost. |
| `Glam` | 华彩 | Disables Replay on its card after the first play this combat. | **Partial / defer.** Shadow enchantment status is feasible, but enchantment `OnPlay`/play-count simulation is independently missing. |
| `Goopy` | 黏糊 | Increments its enchantment amount, permanently increasing the card's block bonus. | **Local feasible for prediction.** Mutate only the detached enchantment/card preview; never touch `DeckVersion`. |
| `Vigorous` | 活力 | Disables its damage bonus after its card is played. | **Local feasible.** Store disabled status on the detached predicted enchantment for later shadow plays. |
| `Play20CardsSingleTurnAchievement` | 成就模型 | Counts local cards and unlocks an achievement at 20. | **Ignorable.** |
| `SkillSilent1Achievement` | 成就模型 | Clears local stack/achievement counters after the remembered root card. | **Ignorable.** |
| `CccComboModel` | 徽章模型 | Counts local cards and unlocks the 20-card badge. | **Ignorable.** |

## AfterCardPlayedLate listeners

| Model | 中文名 | Original effect | Research disposition |
| --- | --- | --- | --- |
| `MakeItSo` | 如此甚好 | If outside hand, returns itself to hand after every configured owner Skill, using finished-card history that already includes the current play. | **Local feasible.** Combine live and shadow finished history, then move the predicted listener card from its shadow pile. |
| `RightHandHand` | 得力助手 | If in discard, returns itself to hand after owner plays a card that spent at least the configured energy. | **Local feasible.** Use `CardPlay.Resources` and shadow pile movement. |

## Recommended implementation slices

1. Add hook contexts/registries and exact vanilla dispatch order. Add shadow `CardPlayStarted` before `OnPlay`; keep
   `CardPlayFinished` before both after phases. Preserve the before guarded / after unguarded distinction against
   simulated combat-ending state.
2. First cover local effects that can change the current card or immediate projection: `PenNib`, pre-play block/stars,
   post-play block/damage/draw/energy/orbs, and paired RNG effects.
3. Add prediction-aware adapters for the four cross-hook families: damage modifiers (`PenNib`, `SlowPower`,
   `SurroundedPower`), block modifiers (`PaelsLegion`, `Vambrace`), cost modifiers (free-card powers,
   `BrilliantScarf`, `VeilpiercerPower`, `VoidFormPower`), and `ShouldPlay` (`ChainsOfBindingPower`, `SlothPower`,
   `VelvetChoker`). Calling the original hooks after only updating `StateStore` would still read stale live fields.
4. Add shadow counters/history consumers and card-listener mutation (`Stomp`, `BansheesCry`, `Pinpoint`,
   `MakeItSo`, `RightHandHand`). Centralize live-plus-shadow card-play counts instead of duplicating the helper already
   present in `CardDrawCardMirrors`.
5. Register explicit no-ops for ignorable listeners and explicit risk handlers for Apply/Remove Power, summon, and
   monster-state effects. Do not silently no-op a trigger that can alter a later nested action.

## Parity and risk notes

- The two TODOs are lifecycle gaps, not merely missing visible bonus effects. Until they are filled, chained
  simulations also retain stale counters for cost, `ShouldPlay`, block, and damage value hooks.
- Direct original value/predicate hook calls read live models. `PredictionStateStore` fixes pair/counter mutation only
  when the corresponding value hook is also routed through a prediction-aware adapter.
- Hook listeners are currently enumerated from the live `CombatState`. Prediction-generated cards are not added as
  listeners, so generated copies of `Stomp`, `BansheesCry`, `Pinpoint`, `MakeItSo`, or `RightHandHand` would remain a
  parity gap until hook iteration can include shadow generated cards in vanilla order.
- Multi-play cards run both hook phases once per play index. Pair state should use `CardPlay` occurrence identity or
  an equivalent stack-safe key, not only card identity; nested play can interleave another card before the outer after
  phase.
- `AfterCardPlayedLate` must remain a second full listener pass. Running each listener's ordinary and late methods
  back-to-back would change `MakeItSo`/`RightHandHand` pile observations and other listener interactions.
- `RazorTooth` and `Goopy` have persistent live/deck side effects in vanilla. Prediction may mirror their detached
  current-combat card state when relevant, but must never mutate the live card or its `DeckVersion`.

## Coverage summary

StS2 v0.110.1 has 29 `BeforeCardPlayed`, 65 `AfterCardPlayed`, and 2 `AfterCardPlayedLate` overrides. Excluding the
single Mock listener gives 95 override occurrences:

- 50 are locally feasible with current simulator primitives;
- 15 are feasible after a targeted value/predicate hook also reads shadow state;
- 11 are ignorable under the current-player-turn scope;
- 19 are partial or blocked by Apply/Remove Power, summon/monster state, or an independently missing
  enchantment/nested-`OnPlay` mirror.

These counts classify override occurrences, so a paired model such as `AfterimagePower` appears once in each relevant
phase.

## Mock model list

- `MockCloneCardsOnPlayPower`: after any owner card, adds a clone to hand. Once shadow generated-card listeners are
  supported, it is useful as a recursion/ordering test but should not be registered as vanilla gameplay coverage.
