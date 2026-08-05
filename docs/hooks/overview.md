# Hook mirror overview

## Guidelines

- Prefer original read-only value hooks when vanilla already uses them for previews. Current examples include `Hook.ModifyHpLost`, card reward creation options, and card reward upgrade odds. When a simulated hook commits shadow state consumed by a later value hook, preserve original listener order but replace the affected listener through an exact hook-mirror registration, as the card-play cost, predicate, damage, and block mirrors do.
- Mirror only side effects that can change prediction output: draw order, hand/discard/exhaust piles, preview card cost/dynamic vars, block, damage, death/liveness, orb counts, current-turn energy, and RNG consumption.
- Combat predictions are scoped to outcomes that can still affect the current player turn. Do not mark risk only because vanilla would mutate state for an enemy turn, a later player turn, room-end rewards, or future reward screens, unless that state can feed back into a prediction surfaced before the current player turn finishes.
- Do not simulate VFX, SFX, waits, achievement unlocks, or effects that cannot occur during the current player-turn prediction surface.
- Treat Apply Power, Remove Power, summon, revive, monster move/state changes, combat removal, player death, and max HP mutation as unsupported until the simulator owns those state domains.
- Use `PredictionStateStore` for model-local counters/flags instead of mutating live model fields.
- Consumable live powers whose later value hooks depend on amount/presence use the shared
  `PowerAmountPredictionState`; only those exact listeners are replaced by prediction-aware value-hook adapters.
  Current damage and attack consumers include `SlipperyPower`, `BufferPower`, `FlutterPower`, `VigorPower`, and
  `GigantificationPower`.
- If a listener has any unmodeled prediction-relevant side effect, append an explicit `CombatPredictionRiskReason` to prediction history instead of silently ignoring it.
- Keep Mock models out of implementation/ignore registries; list them only in docs.

## Mirror registry architecture

- `Common/Mirrors/ModelMethodMirrorRegistry.cs` centralizes exact-type registration and dispatch, override detection, lookup caching, trace scoping, and unsupported-risk recording. Action registries can check for an explicit handler and optionally infer unregistered gameplay overrides; inferred dispatch records incomplete risk. Result registries require a registered handler that supplies the return value and do not support inference.
- Selective read-only value and predicate hook mirrors use `TryInvokeRegistered` to dispatch only exact
  prediction-state overrides. A miss does not resolve or cache an unsupported lookup and records no risk; the hook
  facade instead calls that listener's original method in the same vanilla pass. Side-effect hooks continue through
  the full action registry: reviewed visual/no-op overrides are registered ignored, while an unknown override records
  unsupported risk because it may mutate prediction-relevant state and cannot safely run against live models.
- `IPredictionMirrorContext<TBase>` is a dispatcher-only contract. Combat contexts explicitly map ordinary listeners to the listener, orb receivers to the shadow orb, and `CardModel.OnPlay` / `CardModel.OnTurnEndInHand` receivers to the original card rather than an optional detached preview. Typed handlers use the context's `History` alias for explicit risk reasons.
- `HookMirrors` facades own hook-level control flow, including context construction, listener enumeration, phase refresh, short-circuiting, result chaining, and only-modifier dispatch. The registry only dispatches one listener at a time.
- Hook-level listener enumeration follows each vanilla facade rather than assuming every hook uses
  the guarded iterator. In particular, `AfterBlockBroken` and the two passes inside `AfterCardPlayed`
  directly iterate the combat state so a killing hit/card can still finish its listeners. The paired
  `BeforeCardPlayed` hook uses the guarded iterator.
- Hook mirrors are grouped first by domain and then by hook name under `Mirrors/Hooks/`. Each hook-name file owns its method specification, registry, context, handlers, and hook-local state; state or behavior shared by multiple hooks may use a separate model-centric file.
- Combat and out-of-combat code have independent `HookMirrors` facades but share the registry infrastructure. Mirrored model behavior that is not a hook, such as orb virtual methods, `CardModel.OnPlay`, `CardModel.OnTurnEndInHand` and `PotionModel.OnUse`, lives in its model domain under `Mirrors/` and follows the same facade/registry split.
- `CombatPredictionHistory` stores semantic events, resolved events, and explicit risk events in one ordered timeline. Entries recorded within a prediction source scope capture its current immutable trace frame; source-less operations may record entries with no trace. Deferred card draws and individual generated cards append separate original and resolved entries; consumers use original order, resolved snapshots, and the maximum resolved timeline position. A reference-identity completion index rejects unresolved, duplicate, and cross-history completion. History also maintains exact entry-type counts so simulator safety limits can be checked without repeatedly scanning the full history.
- Card play simulation records a started entry before `CardModel.OnPlay` and a finished entry before the two after-hook passes. The paired before/ordinary-after/late-after dispatch runs once per play index and keeps ordinary and late listeners in separate full passes.
- `CombatPredictionProjector.Project` consumes one completed combat-action history together with the exact root action frame returned by its simulator entry. The frame's source is the original card or potion identity, its action is `CardPlay` or `PotionUse`, and its parent chain identifies nested actions; callers must pair it only with history from the same simulator. The projector owns root-action feature gates, ordered HoverTip projection, damage/highlight payloads, causal explanations and the shared risk boundary.

## Related docs

- `after-card-changed-piles.md`
- `after-card-discarded.md`
- `after-card-drawn.md`
- `after-card-entered-combat.md`
- `after-card-exhausted.md`
- `after-card-generated-for-combat.md`
- `attack-hooks.md`
- `should-draw.md`
- `block-hooks.md`
- `damage-modifier-hooks.md`
- `damage-hooks.md`
- `death-hooks.md`
- `end-turn-hooks.md`
- `energy-hooks.md`
- `orb-hooks.md`
- `shuffle-hooks.md`
- `card-reward-hooks.md`
- `card-play-hooks.md`
