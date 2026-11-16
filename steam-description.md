[h1]⚠ Read first: only UI Rethemed mods built on Core change look[/h1]

[b]This mod works through UI Rethemed - Core, so the era looks only show in the UI Rethemed mods that use Core.[/b]

[list]
[*][b]Today:[/b] the Main Bar and Factions get the full look. Ideology gets the palette.
[*][b]Not yet:[/b] the other UI Rethemed mods, such as Colonist Card, Quests, Notifications, Animals, Wildlife and History, keep their own look for now.
[*][b]Later:[/b] Lazy Friday Studio is moving the rest of the suite onto Core. Each one picks up the era looks as it moves, with no update to this mod needed.
[/list]

[h1]Description[/h1]

Your tribal colony's interface can look like split wood and fieldstone instead of spacer hexagons.

This add-on for Lazy Friday Studio's [b]UI Rethemed[/b] gives each of RimWorld's seven tech levels its own palette and pattern. Each pattern is a plain material texture that suits the level, and it changes as your colony advances. Your own UI Rethemed colours stay saved and come back whenever you turn this off.

[b]Mod version:[/b] 0.2.0

[h1]Features[/h1]

[list]
[*]Seven era looks, one per tech level, each a palette and a pattern for UI Rethemed - Core.
[*]The look follows your colony's tech level and changes the moment research or a mod such as VFE Tribals raises it.
[*]Your own pick in UI Rethemed - Core is never overwritten. It shows at the main menu and whenever Follow the era is off.
[*]Optionally, in the UI Rethemed - Factions window, each faction shows the pattern for its own tech level, so a tribe shows split wood and a pirate gang shows hull panels whatever your colony's era.
[*]Items you can't use still show Core's line hatching while an era pattern is on.
[*]Choose the palette and pattern for each era yourself, from every one UI Rethemed - Core has, or keep your own pick for an era. The settings show a preview and the palette colours for each tech level.
[*]Four stone wall patterns to choose from as well: castle ashlar, coursed drystone, random rubble and levelled rubble.
[/list]

[h2]Animal[/h2]
Fieldstone on charcoal and bone.

[h2]Neolithic[/h2]
Split wood on dark rock, with red ochre and yellow ochre.

[h2]Medieval[/h2]
Cut stone on lapis blue and gold.

[h2]Industrial[/h2]
Bolted plates of diamond tread plate on iron grey and steel.

[h2]Spacer[/h2]
Hull panels on deep blue and cyan.

[h2]Ultra[/h2]
Hex tiles on deep plum and magenta.

[h2]Archotech[/h2]
Angled plates with lit circuit traces, on olive grey and lime.

[h2]Drawn to stay readable[/h2]
Each texture is flat, based on the game's own floors and buildings, and drawn in the palette's colours behind text and icons. They drift slowly along title bars when UI Rethemed's animations are on. The older rock art, tapestry and engraving figure patterns are still in the pattern lists if you prefer them.

[h2]Each faction in its own era (optional)[/h2]
Turn on [b]Show each faction's era[/b] in this mod's settings, and the UI Rethemed - Factions window shows every faction in the pattern for its own tech level. A tribe's row shows split wood and a pirate gang's shows hull panels, whatever era your colony is in. Select a faction and its header card, graph pages and leader card use its pattern too. Your colony's own look doesn't change.

It's off by default, because a faction in another era's pattern is easy to mistake for your own colony changing era. It needs UI Rethemed - Factions.

[h1]How to Use[/h1]

[list]
[*]Load a save or start a game. The look matches your colony's current tech level straight away.
[*]Research or an era advance from a mod such as VFE Tribals or Node Research switches it on the spot.
[*]To pick a look yourself, turn off Follow the era, then choose a palette and pattern in [b]Options > Mod options > UI Rethemed - Core[/b]. The era looks sit at the end of each list.
[/list]

[h1]Settings[/h1]

[b]Options > Mod options > UI Rethemed - Era Looks[/b]:

[list]
[*][b]Follow the era[/b] (on by default): while a game is loaded, shows the look for your colony's tech level in place of your own pick.
[*][b]Show each faction's era[/b] (off by default): in the UI Rethemed - Factions window, each faction's list row, header card and icon boxes show the pattern for that faction's tech level. A selected faction's graph pages and leader card show its pattern too.
[*][b]Look for each era[/b]: a palette and a pattern for each tech level, animal through archotech. Each opens a list of every palette or pattern UI Rethemed - Core has, plus [b]Your own pick[/b] to keep yours for that era. [b]Reset to defaults[/b] puts the era looks back.
[/list]

While UI Rethemed - Core's settings page is open, your own pick shows, so changes you make there appear as you make them. Core's settings say Era Looks is overriding the look.

[h2]Auto Mod Config[/h2]
Supports [url=https://steamcommunity.com/sharedfiles/filedetails/?id=3586136174]Auto Mod Config (Continued)[/url], so a mod list can ship its own look for each era. The settings file is [b]Mod_3840498215_EraLooksMod.xml[/b] (or [b]Mod_<folder>_EraLooksMod.xml[/b] for a local copy), with the class [b]EraLooks.EraLooksSettings[/b]. Its keys are [b]followEra[/b] (True or False) and a palette and a pattern per era: [b]animalPalette[/b] and [b]animalPattern[/b], and the same pair for [b]neolithic[/b], [b]medieval[/b], [b]industrial[/b], [b]spacer[/b], [b]ultra[/b] and [b]archotech[/b]. Each takes a palette or pattern defName, such as UIRethemedCore_Slate or EraLooks_Tapestry, or OwnPick for the player's own. A key that is left out keeps the era look made for that era.

[h1]Requirements[/h1]

[list]
[*]RimWorld 1.7.
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077]Harmony[/url].
[*][url=https://steamcommunity.com/sharedfiles/filedetails/?id=3832843406]UI Rethemed - Core[/url] by Lazy Friday Studio, version 1.10.0 or later.
[/list]

UI Rethemed - Factions is optional. The faction patterns only show when it's loaded.

No DLC is needed.

[h1]Compatibility and Save Safety[/h1]

[list]
[*]Only the UI Rethemed mods built on Core change look. See the note at the top.
[*]Load after UI Rethemed - Core. RimWorld sorts this for you.
[*]Safe to add to or remove from an existing save. Nothing is stored in your save; your settings live in the mod settings.
[*]If you remove the mod while an era pattern is picked by hand, UI Rethemed - Core goes back to its honeycomb. Colours you applied from an era palette stay as your own colours.
[*]The faction patterns need the UI Rethemed - Factions update from 16 November 2026 or later. With an older Factions, the mod writes one warning to the log and factions show your colony's pattern. The pattern behind each row of the faction list still hooks into how Factions draws that list. If a Factions update changes the list, the rows lose that pattern and the mod writes one warning to the log.
[*]Not related to combat, so Combat Extended makes no difference.
[/list]

[h1]Credits[/h1]

UI Rethemed and its Core palette and pattern system are by Lazy Friday Studio. This mod adds to their work and isn't an official part of it.

[h1]License[/h1]

GNU General Public License v3. Commercial use is allowed. Distributed modified versions must remain under GPLv3, with corresponding source available as the license requires.

[h2]AI?[/h2]

Do I use AI to build my mods? [b]Yes.[/b]
Am I obsessive about how I use it and the quality of the results? [b]Also yes.[/b]

I understand the scepticism.
I've seen the flood of AI-generated mods that look like someone typed a prompt, uploaded whatever came out, and called it done.

That bothers me too.

My workflow involves a lot of iteration, review, and testing. The same care I'd put into building everything by hand. I've spent hundreds of hours building mods. Using AI doesn't lower the standard I expect from my work.

[h1]Links[/h1]

Source code: [url=https://github.com/KyleMHB/UI-Rethemed-Era-Looks]GitHub[/url]

Support me on Ko-fi.

[url=https://ko-fi.com/I7L525WMJ6][img]https://img.shields.io/badge/Support_me_on_Ko--fi-72a4f2?style=for-the-badge&logo=kofi&logoColor=white[/img][/url]
