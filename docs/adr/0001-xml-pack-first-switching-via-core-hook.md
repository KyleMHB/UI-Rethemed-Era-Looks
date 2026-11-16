---
status: superseded by ADR-0002
---

# Ship era looks as XML first; switch through a Core hook

The era looks ship first as plain `ThemePaletteDef` and `PatternDef` defs that players pick by hand in LFS Core's settings. Automatic switching by era waits for a public hook from the LFS author, The XML pack ships without waiting for that hook. A Harmony read-time override of Core's internals gets built only if the hook stalls, not alongside the pack. Core's palette and pattern setters are internal, save into the player's own settings, and belong to a mod its author calls work in progress. Patching them would break on Core updates, and a careless write would overwrite the player's own choice. The art and defs are the same work on every route, so building them first wastes nothing.

## Considered Options

- Harmony override only, now: switching right away, but it depends on Core internals that may change at any time.
- XML only, with no switching: safe, but it misses the main point of the mod.
