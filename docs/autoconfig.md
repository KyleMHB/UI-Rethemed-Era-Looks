# Auto Mod Config presets

Era Looks loads after Auto Mod Config (Continued) (`Garethp.ModlistConfigurator`), so a preset's settings file is in place before Era Looks reads it.

- File: `Mod_3840498215_EraLooksMod.xml` for the Workshop copy, or `Mod_<folder>_EraLooksMod.xml` for a local copy (for example `Mod_UI-Rethemed-Era-Looks_EraLooksMod.xml`). A preset that covers both ships both files.
- Class: `EraLooks.EraLooksSettings`.
- `followEra`: `True` or `False`. Left out, it is `True`.
- `<era>Palette` and `<era>Pattern` for `neolithic` (animal and neolithic), `medieval`, `industrial` and `spacer` (spacer and later). Each holds a `ThemePaletteDef` or `PatternDef` defName, or `OwnPick` for the player's own pick in UI Rethemed - Core. Left out, the era keeps the look made for it. A defName that no loaded mod defines shows the player's own pick.

```xml
<?xml version="1.0" encoding="utf-8"?>
<SettingsBlock>
  <ModSettings Class="EraLooks.EraLooksSettings">
    <followEra>True</followEra>
    <medievalPalette>OwnPick</medievalPalette>
    <medievalPattern>EraLooks_Tapestry</medievalPattern>
    <spacerPalette>UIRethemedCore_Midnight</spacerPalette>
    <spacerPattern>UIRethemedCore_Grid</spacerPattern>
  </ModSettings>
</SettingsBlock>
```

UI Rethemed - Core's own pick (the palette, pattern and colours shown with Follow the era off) lives in Core's settings file, not this one.
