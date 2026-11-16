# Switch looks by era with a Harmony patch until Core has a hook

Superseded by [0003](0003-switch-through-core-override.md).

Supersedes the timing in [0001](0001-xml-pack-first-switching-via-core-hook.md): the player wanted automatic switching before LFS Core offers a public way to do it, so the mod patches Core's internal `ThemeLook` now (its `Def` getter, `Build`, `Changed` and `Previewing`, and `UIRethemedCoreSettings.colors` during a build). The patch only changes what Core shows while a game is loaded and never writes the player's saved pick. Every internal is looked up once at startup; if any is missing after a Core update, nothing is patched and the looks stay manual picks. When Core gains a public override hook, the patch is replaced by it.

## Consequences

- The mod supports RimWorld 1.7 only; 1.6 was dropped rather than shipped untested.
- The mod now requires Harmony.
- An era pattern hatches with Core's line pattern through a postfix on the public `PatternDef.HatchFor`, because scattered figures don't read as hatching.
