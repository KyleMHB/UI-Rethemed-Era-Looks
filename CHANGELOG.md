# Changelog

<!-- Agent read route: read only the newest relevant release; locate older versions by searching `^## ` headings. -->

## 2026-11-16 (late night)

### Added

- Added four stone wall patterns: castle ashlar, coursed drystone, random rubble and levelled rubble. Pick any of them for an era in the settings.

### Changed

- Rebuilt the settings page with UI Rethemed - Core's own cards, palette menu and pattern picker. Each tech level now shows a preview of its title bar and pattern and a swatch of every palette colour.
- Moved Show each faction's era into its own Factions window card, with a note on how it can be confused with a change to your colony's look.
- Drew the era patterns at half size on icons, tabs, buttons and cards so more of each pattern shows in a small box. Split wood, hull panels and etched circuits are drawn at about a third of their size, since their shapes are spread wider. Hull panels and etched circuits also use a version with thicker seams and traces on these small surfaces, so the lines don't fade out. Large backgrounds such as the faction relations graph keep the full size. The figure patterns (rock art, tapestry, engraving) are unchanged.
- Redrew the Archotech etched circuits to look like the Ideology archotech structures. The pattern is now large plates whose corners are all cut at 45 degrees, each holding its own lit circuit traces. Every trace starts at a plate edge and ends at a dot or ring, or at another edge. Some leave an edge in groups of two or three.
- Redrew the Industrial tread plate as rectangular deck plates in staggered rows, with a bolt in each corner, so it reads as a steel floor rather than a repeating pattern.
- Redrew the Ultratech hex tiles with clean, even joints. The old ones had ragged edges and a few gaps. The hexes keep the same size and position.
- Widened and darkened the Spacer hull panel seams on large backgrounds so the plates read clearly.
- Redrew the Spacer hull panels with smaller insets and more staggered seams.
- Fixed the Spacer hull panels repeating with bent seams. They now have a new layout of interlocking plates with a few cut corners, and repeat with no visible border between copies.
- Set split wood as the Animal default, hull panels as the Spacer default, and etched circuits as the Archotech default. Saved pattern choices still take precedence.
- Set cut stone as the medieval default. Castle ashlar, brick and the other stone patterns remain available.
- Changed the medieval pattern from brick to castle ashlar, large dressed stone blocks like a keep wall. Brick is still available. If you picked a pattern for medieval yourself, your pick stays.

## 2026-11-16 (night)

### Changed

- Changed every era pattern to a flat material texture, mostly taken from the game's own floors and buildings: flagstone for animal, wooden floorboards for neolithic, brick for medieval, diamond tread plate for industrial, the Odyssey orbital platform for spacer, the Ideology hex tiles for ultra and the plates of the Ideology grand archotech structure for archotech. They are drawn flat, in UI Rethemed's own style, with no bevel or shading. If you picked a pattern for an era yourself, your pick stays.
- Changed the Ultra look to its own hex tiles instead of the theme's honeycomb.
- Changed every era to use one texture for tabs, buttons and large backgrounds alike.

### Fixed

- Fixed the selected faction's Settlements and Overview graphs and its leader card in UI Rethemed - Factions showing your colony's era instead of the faction's.

## 2026-11-16 (evening)

### Changed

- Changed the sparse backgrounds, the line hatching and the faction patterns to use UI Rethemed - Core's new public pattern settings instead of patching Core's drawing. Needs UI Rethemed - Core 1.10.0 or later.
- Changed the faction patterns to use UI Rethemed - Factions' new public pattern hook. The icon boxes in the faction relations list now show each faction's era too. Needs the Factions update from 16 November 2026 or later.

## 2026-11-16 (later)

### Added

- Added each faction's own era pattern to the UI Rethemed - Factions window. Each row in the faction list, the selected faction's header card and the box behind each faction's icon show the pattern for that faction's tech level, so a tribe shows scree and a pirate gang shows hull panel lines whatever your colony's era. The relations graph keeps your colony's pattern. Turn it off with the new Show each faction's era setting.

### Fixed

- Fixed the medieval, animal, spacer and archotech patterns looking blank on small tabs and buttons. Quatrefoil is now a close diaper of quatrefoils, grass has more tufts in a smaller tile, the spacer hull seams are closer together, and the archotech circuit has about twice as many traces, so each one shows on a tab of any size.

### Changed

- Changed the medieval, animal, spacer and archotech patterns to use a sparser version on large backgrounds, such as the faction window. Tabs and buttons keep the dense version.
- Changed the era switching to use UI Rethemed - Core's own override, so Core's settings now say Era Looks is overriding the look. Your own pick now shows while Core's settings page is open, instead of while any mod settings window is open. Needs the current Core.

## 2026-11-16

### Added

- Added a look for each of the seven tech levels, each a palette with a plain material texture: grass on charcoal and bone for animal, scree on rock art for neolithic, quatrefoil tracery on tapestry for medieval, tread plate on engraving for industrial, hull panel lines on the theme's slate for spacer, the theme's honeycomb on deep teal and cyan for ultra, and circuit traces on olive and lime for archotech.
- Added the Bone, Ultratech and Archotech palettes and the Grass, Scree, Quatrefoil, Tread plate, Panel lines and Archotech circuit patterns to Core's lists.

### Changed

- Changed the mod settings to one row per tech level. Animal no longer shares the neolithic look, and ultra and archotech no longer share the spacer look. Choices you saved for neolithic, medieval, industrial and spacer carry over.
- Changed the default patterns from figures to textures. The rock art, tapestry and engraving figure patterns stay in the pattern lists.

## 2026-11-15

### Added

- Added three era looks for LFS UI Rethemed - Core, each a palette and a pattern you pick in Core's mod settings.
- Added Rock art for animal and neolithic colonies, with eland, hunters, giraffes and cattle in red ochre on dark rock.
- Added Tapestry for medieval colonies, with knights, longships, towers, trees, hounds and anvils in gold on lapis blue.
- Added Engraving for industrial colonies, with gears, locomotives, works chimneys, paddle steamers, street lamps and pocket watches in brass on bottle green.
- Added automatic switching on RimWorld 1.7: while a game is loaded, Core shows the look for your colony's tech level, with the theme's own slate and honeycomb from the spacer era on. Your own saved pick shows at the main menu and with the new Follow the era setting turned off.
- Added a palette and a pattern choice for each era in the mod settings, listing every one UI Rethemed - Core has plus your own pick.
- Added support for Auto Mod Config (Continued): Era Looks loads after it, so a preset can set the look for each era.
- Made unusable items hatch with Core's fine lines while an era pattern is picked, so they still read as unusable.

### Changed

- Changed the mod to require Harmony.

### Internal

- Added SVG sources for the patterns and a script that exports them to the textures Core loads.
- Added developer tests that load the looks in a separate game, save screenshots of each one, and check switching by era without changing the player's saved pick.
