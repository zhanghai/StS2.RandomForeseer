# Card play result-location hooks

Mirror files: `InCombat/Mirrors/HookMirrors.cs`,
`InCombat/Mirrors/Hooks/Card/ModifyCardPlayResultLocationMirrors.cs`, and
`InCombat/Simulation/CombatPredictionSimulator.Card.cs`.

## Hook specs

- `AbstractModel.ModifyCardPlayResultLocation(CardModel, bool, ResourceInfo, CardLocation)`
- `AbstractModel.AfterModifyingCardPlayResultLocation(CardModel, CardLocation)`

## Vanilla order

`CardModel.OnPlayWrapper` first resolves the card's base result location, then chains every
`ModifyCardPlayResultLocation` listener in hook order. A listener is added to the modifier list only when its returned
`CardLocation` differs from the value it received. `Hook` has no after facade for this operation. Instead,
`CardModel.OnPlayWrapper` directly iterates the returned modifier list in order and calls
`AfterModifyingCardPlayResultLocation` before generating the card's play count.

The simulator preserves the value pass and then directly dispatches that modifier list without re-enumerating hook
listeners. Unregistered value listeners continue through their original read-only method; unregistered selected after
listeners record unsupported risk rather than mutating live model state.

## Vanilla listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `CorruptionPower` | 腐化 | Owner Skills move to exhaust. | Value method uses the original read-only path; selected after hook is visual-only and registered ignored. |
| `FeralPower` | 野性 | The first configured number of non-dupe, zero-energy owner Attacks each turn return to hand. | Implemented with prediction-local used count initialized from the live power and committed only when this listener changes the result location. |
| `NostalgiaPower` | 怀旧 | The first configured number of owner Attacks or Skills each turn move to the top of the draw pile. | Implemented by combining live and shadow card-play-started history. Its selected after hook is visual-only. |
| `ReboundPower` | 弹回 | The next configured number of owner cards that would discard instead move to the top of the draw pile. | Implemented with shared shadow power amount; each selected result-location change consumes one stack. |

## Parity notes

- The selected-modifier check compares the complete `CardLocation`, including player, pile, and position, like vanilla.
- `FeralPower` does not advance when an earlier modifier already produced the same hand location, because vanilla only
  selects a modifier whose own return value changes the chained result.
- `ReboundPower` shadows only prediction-relevant stack consumption and does not run the full vanilla power
  amount/removal lifecycle.

## Mock model list

- None.
