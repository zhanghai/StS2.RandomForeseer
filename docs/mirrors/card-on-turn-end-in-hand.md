# CardModel.OnTurnEndInHand mirror

Mirror files: `InCombat/Mirrors/CardOnTurnEndInHand/` and
`InCombat/Simulation/CombatPredictionSimulator.EndTurn.cs`.

Combat prediction never invokes the real protected virtual method. After moving a predicted card from the shadow hand
to the shadow play pile, `CardOnTurnEndInHandMirrors` dispatches an exact-runtime-type handler against its current
preview. Read-only handlers can therefore keep using the original model; `Regret` explicitly creates and mutates a
detached mutable preview for its private counter. The registry records `MethodNotMirrored` risk for unregistered
gameplay overrides. The wrapper then applies vanilla's Ethereal-or-discard result using shadow piles.

## Method spec

- `CardModel.OnTurnEndInHand(PlayerChoiceContext)`

## Vanilla card coverage

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `BadLuck` | 霉运 | Deals unblockable, unpowered HP loss to its owner. | Implemented through simulator card-source damage. |
| `Beckon` | 呼唤 | Deals unblockable, unpowered HP loss to its owner. | Implemented through simulator card-source damage. |
| `Burn` | 灼伤 | Deals its `Damage` dynamic var to its owner. | Implemented through the shared damage handler. VFX/SFX omitted. |
| `Decay` | 腐朽 | Deals its `Damage` dynamic var to its owner. | Implemented through the shared damage handler. |
| `Infection` | 感染 | Deals its `Damage` dynamic var to its owner. | Implemented through the shared damage handler. VFX omitted. |
| `Toxic` | 毒素 | Deals its `Damage` dynamic var to its owner. | Implemented through the shared damage handler. |
| `Wither` | 凋萎 | Deals its current `Damage` dynamic var to its owner. | Implemented through the shared damage handler, including preview-state value changes. |
| `Regret` | 悔恨 | Deals unblockable, unpowered damage equal to hand size captured before turn-end card processing. | Implemented with `CardsInHand` on the detached mutable preview, populated by the `BeforeSideTurnEnd` mirror and reset after damage. |
| `Debt` | 债务 | Removes owner gold. | Ignored because run gold is not consumed by combat prediction. |
| `Doubt` | 疑虑 | Applies Weak; a newly created instance skips its next duration tick so it remains for the next player turn. | Ignored because Weak cannot affect the remaining unpowered phase-one damage. |
| `Shame` | 羞耻 | Applies Frail; a newly created instance skips its next duration tick so it remains for the next player turn. | Ignored because Frail cannot affect the remaining phase-one damage. |

## Parity notes

- Card damage passes both the owner creature as dealer and the predicted card as `cardSource`, matching vanilla's
  card-source `CreatureCmd.Damage` overload while leaving `CardPlay` null.
- Unsupported modded overrides are risk-marked rather than invoking live card behavior.
- Waits, VFX, SFX, real pile mutation, and real card-local state mutation are intentionally omitted. `Regret`'s
  `CardsInHand` mutation is reproduced only on its detached mutable preview.

## Mock model list

- `MockTurnEndInHandRecorderCard`
