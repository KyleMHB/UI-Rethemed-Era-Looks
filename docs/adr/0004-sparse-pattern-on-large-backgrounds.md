# Draw a sparser pattern on large backgrounds by patching Core's Theme.Pattern

A pattern dense enough to show on a small tab looks busy across a whole window, and a sparse one shows almost nothing on a tab. Core's `PatternDef` has one texture. So each Era Looks pattern ships two textures: the dense `<Name>` and a sparse `<Name>Sparse`. A Harmony prefix on `Theme.Pattern(Rect, Vector2)` draws the sparse one when the rect is at least 100 px tall. It reuses Core's private `Tiled` and the same alpha as Core (`a * PatternStrength * weight`). The prefix only acts on tiled `EraLooks_` patterns that have a sparse texture. Everything else goes through Core unchanged.

This patch is temporary. We asked Core's author for an optional large-surface texture on `PatternDef` (see `artifacts/core-request-small-pattern.md`). If Core adds one, move the sparse textures to that field and remove the patch.

## Consequences

- If Core renames `Theme.Pattern` or `Tiled`, the mod logs one warning, skips the patch and draws the dense pattern everywhere.
- `Faces.GradientPattern` reads the pattern texture directly, so gradient fills stay dense.
- The 100 px threshold is a guess from the screens test: tabs and header bands are under it, and window bodies such as the faction graph are over it.

## Update 2026-11-16

Superseded in mechanism: Core 1.10.0 added `PatternDef.largeTexPath` and `Surface.Background`, so the sparse textures are set on the PatternDefs and the Harmony prefix on `Theme.Pattern` is gone. Core now chooses the large texture only where the caller passes `Surface.Background` (its Ideology graph, previews and picker), not by rect height. The Factions relations graph shows the dense pattern until a Factions build passes `Surface.Background`.

## Update 2026-11-16 (night)

No era pattern sets `largeTexPath` any more. The era textures are now flat adaptations of the game's own floor and building textures, which read evenly at every size, so each era uses one texture everywhere. The bridge for `largeTexPath` in `Source/CoreBridge.cs` stays, so a sparse texture can come back without code changes.

## Update 2026-11-16 (late night)

The era material patterns set Core's `scale` to 0.5 and leave `largeScale` at 1. `Surface.Small` (icons, tabs, buttons, cards) draws the tile at half size so a small box shows a full repeat, and `Surface.Background` (the faction relations graph) keeps the full tile. Core has no rule based on box size, so the row cards also become denser. The figure patterns keep scale 1, because their figures would be too small to read.

Hull panels and etched circuits use `largeTexPath` again, the other way round from the old sparse textures: the original 1024 px texture is the large one, and `texPath` points at a `…Small` version made by `Art/tools/make-small-variant.py`, with its lines widened and the tile shrunk to 512 px. Their 4 px lines dropped below a screen pixel at scale 0.35 and averaged away, and raising `weight` barely helped because the lines are already nearly clear.
