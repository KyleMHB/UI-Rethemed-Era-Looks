# Show each faction's era pattern by patching UI Rethemed - Factions

Factions draws its list rows with `CoreWidgets.LitFace` and no pattern, and its details header card with one `Theme.Pattern` call. Core has no way to draw a pattern other than the current one, so the mod sets a one-shot `CoreBridge.Forced` pattern that the existing `Theme.Pattern(Rect, Vector2)` prefix (ADR 0004) draws in place of the current one and then clears. A prefix on the private `DetailsPanel.Header` forces the faction's pattern for the header's first pattern call, so the tags drawn later in the header keep the colony's pattern. A prefix on the private `FactionRail.Row` marks the row, and a postfix on `CoreWidgets.LitFace` draws the faction's pattern over the row's face, under its icon and labels. Both prefixes also mark the faction's icon box, and a prefix on `Faces.SlotBack` forces the faction's pattern again for that box, which is drawn after the row or card has used up its forced pattern. Finalizers on both methods clear any pattern left unused.

The faction's pattern is the one set for its tech level in this mod's settings. A level left on the player's own pick shows the colony's pattern. The relations graph is one background for all factions, so it keeps the colony's pattern.

## Consequences

- Factions is optional and not referenced at build time; its types are found by name. Without Factions nothing is patched. If Factions renames `Row`, `Header`, Core's `Faces.SlotBack` or `FactionInfo.faction`, the mod logs one warning and the window shows the colony's pattern.
- The faction patterns need the ADR 0004 prefix, so they are skipped whenever that patch is.
- If Factions ever draws another `Theme.Pattern` before the header card's own, that call takes the faction's pattern instead.

## Update 2026-11-16

The Row and Header prefixes now open Core's public `Theme.PatternScope` (Core 1.10.0) instead of forcing a pattern through Era Looks' own `Theme.Pattern` prefix, and the `Faces.SlotBack` patch is gone because the scope covers it. Row and Header are still private Factions methods; the planned replacement is the Factions pattern resolver hook once that build is installed.

Later the same day, Factions added the public `FactionsApi.AddPatternResolver`. Era Looks now registers a resolver at priority -100, below Factions' own resolver for faction defs that name a pattern, and Factions opens the scope itself around the icon boxes, the header card and the relations list. The Header patch is gone. The Row prefix and `CoreWidgets.LitFace` postfix remain only to draw the pattern behind each list row, which Factions doesn't draw. Only `FactionResolver` names Factions' types, and it runs only after `FactionsApi` is found, so Factions stays optional.

## Update 2026-11-16 (night)

Factions now draws the selected faction's Settlements and Overview graphs and its leader card, all outside the scopes it opens. Era Looks patches the private `FactionCentre.Settlements`, `FactionCentre.Overview` and `DetailsPanel.Leader` with a prefix that opens `Theme.PatternScope` for `FactionsApi.PatternFor(info.faction)` and a finalizer that closes it, so those match the faction's header card. The earlier note that the relations graph keeps the colony's pattern no longer holds: the graph belongs to the selected faction. If Factions renames those methods or their `info` parameter, the mod logs one warning and those parts show the colony's pattern.
