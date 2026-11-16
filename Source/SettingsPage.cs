using System;
using System.Collections.Generic;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace EraLooks;

// The mod's settings page, built from UI Rethemed - Core's own public settings widgets so it looks and works like Core's
// page: fold-out cards, Core's palette menu and its pattern picker with previews. Core's live preview pane is internal, so
// each era card draws its own small preview of its palette's colours and its pattern.
[StaticConstructorOnStartup]
internal static class SettingsPage
{
    private const string SwitchingKey = "Switching";
    private const string FactionsKey = "Factions";
    private const float OptionsShare = 0.32f;
    private const float ColumnGap = 20f;
    private const float PreviewHeight = 34f;
    private const float SwatchGap = 3f;

    private static readonly Texture2D SwitchingTex = CoreWidgets.Icon("sliders");
    private static readonly Texture2D FactionsTex = CoreWidgets.Icon("flag-banner");
    private static readonly Texture2D OwnTex = CoreWidgets.Icon("user");
    private static readonly Texture2D PaletteTex = CoreWidgets.Icon("palette");

    private static readonly Dictionary<EraGroup, Texture2D> EraTex = new()
    {
        [EraGroup.Animal] = CoreWidgets.Icon("paw-print"),
        [EraGroup.Neolithic] = CoreWidgets.Icon("campfire"),
        [EraGroup.Medieval] = CoreWidgets.Icon("castle-turret"),
        [EraGroup.Industrial] = CoreWidgets.Icon("factory"),
        [EraGroup.Spacer] = CoreWidgets.Icon("rocket"),
        [EraGroup.Ultra] = CoreWidgets.Icon("planet"),
        [EraGroup.Archotech] = CoreWidgets.Icon("atom"),
    };

    // A palette's colours in Core's order, keyed as Core saves the player's own colours, with Core's built-in default for a
    // colour the palette leaves out (its slate palette leaves out all of them). A palette without its own title bar colours
    // uses its darkest colour and its theme colour instead, as Core does.
    private static readonly (string key, Func<ThemePaletteDef, string> hex, string fallback)[] Slots =
    {
        ("TitleBar", d => d.titleBar, "Well"),
        ("TitleAccent", d => d.titleAccent, "Green"),
        ("Window", d => d.window, "15191D"),
        ("Panel", d => d.panel, "2F3337"),
        ("Card", d => d.card, "15191D"),
        ("Well", d => d.well, "0E1013"),
        ("Green", d => d.green, "2CAC41"),
        ("Blue", d => d.blue, "2C7AAC"),
        ("Yellow", d => d.yellow, "AC972C"),
        ("Red", d => d.red, "AC3A2C"),
        ("Gray", d => d.gray, "8A8A8A"),
    };

    // Open cards, for this session only. Every card starts open so the era previews show straight away.
    private static readonly List<string> Open = StartOpen();
    private static readonly SettingsCards Cards = new("EraLooks", () => Open);

    private static Vector2 optionsScroll;
    private static float optionsHeight;
    private static Vector2 erasScroll;
    private static float erasHeight;

    private static EraLooksSettings Settings => EraLooksMod.Settings;

    private static List<string> StartOpen()
    {
        var open = new List<string> { SwitchingKey, FactionsKey };
        foreach (EraGroup group in Enum.GetValues(typeof(EraGroup))) open.Add(group.ToString());
        return open;
    }

    internal static void Draw(Rect inRect)
    {
        var options = new Rect(inRect.x, inRect.y, Mathf.Floor((inRect.width - ColumnGap) * OptionsShare), inRect.height);
        var eras = new Rect(options.xMax + ColumnGap, inRect.y, inRect.xMax - options.xMax - ColumnGap, inRect.height);
        optionsHeight = Cards.Column(options, ref optionsScroll, optionsHeight, Options);
        erasHeight = Cards.Column(eras, ref erasScroll, erasHeight, Eras);
        // Core's dropdowns, pattern picker and confirmations only queue their popup; this opens it.
        Cards.Finish();
    }

    private static float Options(float y, float width)
    {
        y = Cards.Card(y, width, SwitchingKey, SwitchingTex, "EraLooksMod_Switching".Translate(),
            (Settings.followEra ? "EraLooksMod_On" : "EraLooksMod_Off").Translate(), SwitchingCard);
        y = Cards.Card(y, width, FactionsKey, FactionsTex, "EraLooksMod_FactionsCard".Translate(),
            (Settings.factionEras ? "EraLooksMod_On" : "EraLooksMod_Off").Translate(), FactionsCard);
        return y;
    }

    private static void SwitchingCard(Listing_Standard listing)
    {
        SettingsCards.Tick(listing, "EraLooksMod_FollowEra".Translate(), ref Settings.followEra, "EraLooksMod_FollowEraTip".Translate());
        listing.Gap(4f);
        if (CoreBridge.Ready) SettingsCards.Hint(listing, "EraLooksMod_FollowEraNote".Translate());
        else SettingsCards.Warning(listing, "EraLooksMod_SwitchingUnavailable".Translate());
    }

    private static void FactionsCard(Listing_Standard listing)
    {
        SettingsCards.Tick(listing, "EraLooksMod_FactionEras".Translate(), ref Settings.factionEras, "EraLooksMod_FactionErasTip".Translate());
        listing.Gap(4f);
        SettingsCards.Hint(listing, "EraLooksMod_FactionErasNote".Translate());
    }

    private static float Eras(float y, float width)
    {
        var hint = "EraLooksMod_LooksHint".Translate().ToString();
        var hintRect = new Rect(4f, y + 2f, width - 8f, Text.CalcHeight(hint, width - 8f));
        Theme.Label(hintRect, hint, TextAnchor.UpperLeft, Theme.TextDim);
        y = hintRect.yMax + 8f;
        foreach (EraGroup group in Enum.GetValues(typeof(EraGroup)))
        {
            var era = group;
            string palette = PaletteOf(era), pattern = PatternOf(era);
            y = Cards.Card(y, width, era.ToString(), EraTex[era], ("EraLooksMod_Era_" + era).Translate(),
                "EraLooksMod_LookSummary".Translate(PaletteLabel(palette), PatternLabel(pattern)), listing => EraCard(listing, era));
        }
        var reset = "EraLooksMod_ResetLooks".Translate().ToString();
        var resetRect = new Rect(0f, y + 2f, Mathf.Min(width, CoreWidgets.ButtonWidth(reset, true)), 28f);
        if (CoreWidgets.Button(resetRect, reset, CoreWidgets.ResetTex, null, Settings.AnyChosen, "EraLooksMod_ResetLooksTip".Translate()))
            Cards.Confirm("EraLooksMod_ResetLooksConfirm".Translate(), Settings.ResetLooks);
        return resetRect.yMax;
    }

    private static void EraCard(Listing_Standard listing, EraGroup era)
    {
        string palette = PaletteOf(era), pattern = PatternOf(era);
        Preview(listing.GetRect(PreviewHeight), ColorsOf(palette), PatternDefOf(pattern));
        listing.Gap(6f);
        Cards.Dropdown(listing.GetRect(SettingsCards.LineHeight), "EraLooksMod_Palette".Translate(), PaletteLabel(palette),
            palette == EraLooksSettings.OwnPick ? OwnTex : DefDatabase<ThemePaletteDef>.GetNamedSilentFail(palette)?.Icon ?? PaletteTex,
            "EraLooksMod_PaletteTip".Translate(), () => PaletteChoices(era));
        listing.Gap(3f);
        // Core's pattern picker offers the follow label as its first card and hands back null for it: here, the own pick.
        Cards.PatternRow(listing.GetRect(SettingsCards.LineHeight), "EraLooksMod_Pattern".Translate(), "EraLooksMod_PatternTip".Translate(),
            pattern == EraLooksSettings.OwnPick ? null : DefDatabase<PatternDef>.GetNamedSilentFail(pattern), "EraLooksMod_OwnPick".Translate(),
            Surface.Small, def => Settings.SetPattern(era, def?.defName ?? EraLooksSettings.OwnPick));
        listing.Gap(4f);
    }

    private static List<ChoiceSection> PaletteChoices(EraGroup era)
    {
        string current = PaletteOf(era);
        var section = new ChoiceSection { title = "EraLooksMod_Palette".Translate() };
        section.items.Add(new ChoiceItem
        {
            label = "EraLooksMod_OwnPick".Translate(),
            note = "EraLooksMod_OwnPaletteNote".Translate(),
            icon = OwnTex,
            accent = ColorsOf(EraLooksSettings.OwnPick)[6],
            on = current == EraLooksSettings.OwnPick,
            action = () => Settings.SetPalette(era, EraLooksSettings.OwnPick),
        });
        foreach (var def in ThemePaletteDef.Ordered)
        {
            var picked = def;
            section.items.Add(new ChoiceItem
            {
                label = def.LabelCap,
                note = def.description,
                icon = def.Icon,
                accent = def.Accent,
                on = current == def.defName,
                action = () => Settings.SetPalette(era, picked.defName),
            });
        }
        return new List<ChoiceSection> { section };
    }

    // A title bar in the era's palette with its pattern drifting along it, then a swatch of each of the palette's colours.
    private static void Preview(Rect rect, Color[] colors, PatternDef pattern)
    {
        var bar = new Rect(rect.x, rect.y, Mathf.Floor(rect.width * 0.45f), rect.height);
        Rounded.Fill(bar, colors[0]);
        using (Theme.PatternScope(pattern))
            Faces.FadingHex(bar, colors[1], colors[0], 1f, 0.85f);
        Rounded.Border(bar, colors[3]);

        float x = bar.xMax + 8f;
        float size = Mathf.Min(rect.height, Mathf.Floor((rect.xMax - x - SwatchGap * (colors.Length - 1)) / colors.Length));
        for (int i = 0; i < colors.Length; i++)
        {
            var swatch = new Rect(x + i * (size + SwatchGap), rect.y + Mathf.Round((rect.height - size) / 2f), size, size);
            Rounded.Fill(swatch, colors[i]);
            Rounded.Border(swatch, Theme.BGLight);
        }
    }

    private static string PaletteOf(EraGroup era) => Settings.PaletteFor(era) ?? EraLooksSettings.DefaultPalette(era);
    private static string PatternOf(EraGroup era) => Settings.PatternFor(era) ?? EraLooksSettings.DefaultPattern(era);

    // The own pick names the player's own pattern explicitly: a null scope would show the era override instead.
    private static PatternDef PatternDefOf(string pattern) =>
        pattern == EraLooksSettings.OwnPick
            ? PatternDef.Named(UIRethemedCoreMod.Settings?.patternDef)
            : DefDatabase<PatternDef>.GetNamedSilentFail(pattern) ?? PatternDef.Default;

    // The palette's colours. The own pick is the player's colours from Core's settings; read from there, not from Theme,
    // which shows the era override while a game is loaded.
    private static Color[] ColorsOf(string palette)
    {
        var own = palette == EraLooksSettings.OwnPick ? UIRethemedCoreMod.Settings?.colors : null;
        var def = palette == EraLooksSettings.OwnPick ? null : DefDatabase<ThemePaletteDef>.GetNamedSilentFail(palette);
        var colors = new Color[Slots.Length];
        for (int i = 0; i < Slots.Length; i++)
            colors[i] = ColorPicker.Parse(HexOf(i, own, def), out var color) ? color : Color.gray;
        return colors;
    }

    private static string HexOf(int slot, Dictionary<string, string> own, ThemePaletteDef def)
    {
        var (key, hex, fallback) = Slots[slot];
        if (own != null && own.TryGetValue(key, out var picked) && !picked.NullOrEmpty()) return picked;
        var value = def == null ? null : hex(def);
        if (!value.NullOrEmpty()) return value;
        int derived = Array.FindIndex(Slots, s => s.key == fallback);
        return derived >= 0 ? HexOf(derived, own, def) : fallback;
    }

    private static string PaletteLabel(string defName) => LabelOf<ThemePaletteDef>(defName);
    private static string PatternLabel(string defName) => LabelOf<PatternDef>(defName);

    private static string LabelOf<T>(string defName) where T : Def
    {
        if (defName == EraLooksSettings.OwnPick) return "EraLooksMod_OwnPick".Translate();
        var def = DefDatabase<T>.GetNamedSilentFail(defName);
        return def == null ? defName : def.LabelCap.ToString();
    }
}
