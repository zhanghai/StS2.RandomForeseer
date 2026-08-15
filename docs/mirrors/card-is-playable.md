# CardModel.IsPlayable mirror

Mirror files: `InCombat/Mirrors/Cards/CardIsPlayableMirrors.cs` and
`InCombat/Simulation/CombatPredictionSimulator.Card.cs`.

Fair-mode card prediction evaluates `CardModel.IsPlayable` after prediction-aware keyword, resource, and
`Hook.ShouldPlay` checks. The protected virtual getter can read live piles or creature state through a detached preview,
so reviewed vanilla overrides use exact-runtime-type handlers backed by `CombatPredictionState`.

## Method spec

- `CardModel.IsPlayable.get`

## Dispatch policy

`CardIsPlayableMirrors` uses `MethodMirrorRegistry<CardModel, CardIsPlayableMirrorContext, bool>` and
`TryInvokeRegistered`:

1. An exact registered type returns its prediction-aware result.
2. Every other type calls the original virtual getter on the current preview card.

The fallback deliberately records no unsupported mirror risk. Cards inheriting the base implementation return true,
and unregistered Mod overrides retain their original behavior. A Mod override that derives its result from live piles,
creatures, combat history, or other state changed only in the prediction may still disagree with shadow state and
should receive an exact handler when support is added.

## Vanilla card coverage

| Model | 中文名 | Original condition | Current mirror status |
| --- | --- | --- | --- |
| `Clash` | 交锋 | Every card in the owner's hand is an Attack. | Implemented from the shadow hand and predicted card types. |
| `GrandFinale` | 华丽收场 | The owner's draw pile is empty. | Implemented from the shadow draw pile. |
| `HighFive` | 击掌 | The owner's Osty is present and alive. | Implemented from the live Osty identity and shadow creature liveness. |

## Parity notes

- An empty hand satisfies `Clash`, matching vanilla `Enumerable.All` behavior.
- `HighFive` returns false when the owner has no Osty or shadow damage has killed Osty.
- Exact handlers read only prediction-owned pile and creature state and do not invoke the original override.
- The original fallback is intentional for compatibility, but it is only as prediction-aware as the unregistered
  override itself.
