# Switch looks by era through Core's Theme.Override

Supersedes [0002](0002-harmony-fallback-before-core-hook.md): LFS Core now has a public `Theme.Override(palette, pattern)` and `Theme.ClearOverride()`, so the mod no longer patches Core's internal `ThemeLook`. It calls `Override` only when the wanted look changes, because each call looks up the calling mod's name, and clears it at the main menu and with Follow the era off, because Core keeps an override across a return to the menu. On a Core without `Theme.Override` the mod logs a warning and the looks stay manual picks.

## Consequences

- Core's settings page names Era Looks as overriding the look, and Core sets the override aside itself while that page is open. The mod no longer clears its look whenever any mod settings window is open.
- The call into Core sits in a method that is never inlined, so Core sees this mod's assembly as the caller rather than Harmony's patched `Update`.
- Harmony stays a requirement: the mod still runs once a frame from `Root_Play.Update` and `Root_Entry.Update`, and still hatches an era pattern with Core's line pattern through the `PatternDef.HatchFor` postfix.
