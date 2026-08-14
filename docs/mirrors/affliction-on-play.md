# Affliction OnPlay mirror

## Vanilla entry

StS2 v0.111.0 calls `AfflictionModel.OnPlay(PlayerChoiceContext, Creature?)` after the card and enchantment `OnPlay` bodies. `CardModel.OnPlayWrapper` captures the current affliction, invokes it with the resolved card target, and stops the remaining lifecycle if the owner dies.

Combat prediction invokes `AfflictionOnPlayMirrors` at the same point and preserves the post-affliction shadow owner death check.

## Registry status

All current production afflictions inherit the empty base `OnPlay` implementation, so the registry intentionally has no registrations. It remains as the lifecycle dispatch point and ensures a future vanilla or gameplay-Mod override is detected and records `MethodNotMirrored` until reviewed. Test-only overrides are subject to the same policy.

The mirror context carries the predicted card and nullable resolved creature target. An unchanged preview affliction maps back to the original affliction for trace ownership; a preview-only affliction remains its own source.
