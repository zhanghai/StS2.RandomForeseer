# Card OnPlay mirror

## Vanilla entry

StS2 v0.109.0 runs each concrete `CardModel.OnPlay(PlayerChoiceContext, CardPlay)` from the card-play lifecycle. The lifecycle owns resource spending, play/result piles, wrapper hooks, enchantments, afflictions and history; the model-specific `OnPlay` body issues the card's commands.

Combat prediction never invokes the real virtual method. `CombatPredictionSimulator.ManualPlay` creates a shadow `CardPlay`, then `CardOnPlayMirrors` dispatches the exact or inferred mirror against the mutable predicted card without mutating the real card, piles, creatures or RNG.

## Dispatch priority

`CardOnPlayMirrors` uses one exact-runtime-type `ModelMethodMirrorRegistry<CardModel, CardOnPlayMirrorContext>` with this fixed priority:

1. An explicitly registered exact handler is `Handled`.
2. A reviewed non-gameplay mod override is `Ignored`.
3. `CardOnPlayInferer` may classify an unregistered gameplay override as `Inferred`.
4. Every other gameplay override is `Unsupported`.

Exact handlers always win and are never combined with inferred behavior. `CanMirror` accepts `Handled` and `Inferred`; unsupported cards do not open a combat-card prediction session. Every inferred invocation records `MethodMirrorIncomplete` before executing its first candidate so projections retain the best-effort warning even when the simulated part succeeds.

## General inference

The inferer inspects original IL through RitsuLib's `GetOriginalIl()`. Async `OnPlay` methods resolve to their generated `MoveNext` body, and the returned ordered direct call targets form a Type-level classification that the registry caches with its handler. Instance values such as upgrade state, dynamic vars and selected target are resolved only when the cached handler runs.

Stage 1 recognizes two direct templates:

| Candidate | Required direct calls | Runtime guard | Mirrored behavior |
| --- | --- | --- | --- |
| Attack | `DamageCmd.Attack` and `AttackCommand.Execute` | `card.Type == CardType.Attack` | Builds an attack from `CalculatedDamage`, `Damage` or `OstyDamage`, applies optional `Repeat`, and targets a single, all or random enemy according to the card. |
| Block | `CreatureCmd.GainBlock` | `card.GainsBlock` | Uses the standard `Block` var. Self-target cards and enemy-targeting attack cards gain block on the owner; `AnyAlly` uses the selected ally; `AllAllies` uses all living player teammates. |

Candidates are deduplicated and executed in their first direct-call order. Requiring both attack construction and execution avoids treating an unused builder as a played attack. Missing standard vars, unsupported targets and an unavailable Osty skip that candidate and retain incomplete risk.

## Deliberate limits and risks

- Only calls directly present in the original `OnPlay`/`MoveNext` body are considered. Same-card helpers, virtual/interface dispatch, delegates, reflection and arbitrary transitive calls are not followed.
- Original IL does not include behavior introduced by Harmony prefixes, postfixes or transpilers.
- Direct calls may still be conditional. Stage 1 does not reconstruct control flow, so a structurally inferred candidate may execute in a state where vanilla would skip it.
- General attack/block parameter resolution is intentionally limited to standard dynamic-var and target templates. Calculated special values, dependent command results and nonstandard targeting require exact mirrors.
- Inference does not imply the complete `OnPlay` was mirrored. Power application, HP loss, draw, energy, card movement/generation and other commands remain omitted unless an exact handler covers the card.
- A dynamic card whose runtime `Type` or `GainsBlock` does not satisfy the candidate guard safely skips that candidate; its structural Type-level classification remains cached.

These limits are intentional. Expanding inference should add narrowly named, offline-verifiable templates rather than evolve into a general IL interpreter.

## Maintenance

Keep exact registrations in `CardOnPlayMirrors.CreateRegistry` and register the single general inferer after them. When adding an inferred template, match an unambiguous original command, define conservative instance-time parameter and target resolution, preserve call order where RitsuLib exposes it, add positive and negative offline samples, and document omitted control flow. New templates must continue to clone RNG and mutate only prediction-owned state.
