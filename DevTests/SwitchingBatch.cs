using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorldDevTests;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace EraLooks.DevTests;

// Expected behaviour: GLOSSARY.md "Follow the era" and docs/adr/0002. While a game is loaded and Follow the era is on, LFS Core
// shows the look for the colony's tech level (one palette and texture per level, and ultra a
// relief of its honeycomb); the player's own pick is never written over. With Follow the era off, the own pick
// shows in every era. A palette or pattern the player chose for an era replaces that era's look, and 'Your own pick' shows
// the player's own palette or pattern. An era pattern hatches what can't be used with Core's fine lines.
// Mechanical setup: the developer sets a distinctive own pick (Core's midnight palette and grid pattern) through Core's setters,
// then sets the player faction def's tech level directly, as VFE Tribals and Node Research do, and restores it at the end.
public class SwitchingBatch : Batch
{
    private const string OwnPalette = "UIRethemedCore_Midnight", OwnPattern = "UIRethemedCore_Grid";

    private static readonly (TechLevel era, string look, string pattern)[] Eras =
    {
        (TechLevel.Animal, "EraLooks_Bone", "EraLooks_Scree"),
        (TechLevel.Neolithic, "EraLooks_RockArt", "EraLooks_SplitWood"),
        (TechLevel.Medieval, "EraLooks_Tapestry", "EraLooks_CutStone"),
        (TechLevel.Industrial, "EraLooks_Engraving", "EraLooks_TreadPlate"),
        (TechLevel.Spacer, "EraLooks_Spacer", "EraLooks_HullPanels"),
        (TechLevel.Ultra, "EraLooks_Ultratech", "EraLooks_Honeycomb"),
        (TechLevel.Archotech, "EraLooks_Archotech", "EraLooks_EtchedCircuits"),
    };

    public override string Id => "eralooks-switching";
    public override int SegmentCount => 1;
    public override IReadOnlyList<PlannedCase> Plan { get; } = new[]
    {
        new PlannedCase("eralooks.follows-era", Levels.Mechanical),
        new PlannedCase("eralooks.own-pick-kept", Levels.Mechanical),
        new PlannedCase("eralooks.per-era-choice", Levels.Mechanical),
        new PlannedCase("eralooks.follow-off", Levels.Mechanical),
        new PlannedCase("eralooks.era-hatch-lines", Levels.Mechanical),
    };

    public override IEnumerable<Wait> Segment(int index, BatchContext ctx)
    {
        var faction = Faction.OfPlayer.def;
        var startEra = faction.techLevel;
        var core = UIRethemedCoreMod.Settings;
        CoreLooks.Apply(OwnPalette, OwnPattern);
        EraLooksMod.Settings.followEra = true;
        string ownGreen = core.colors.TryGetValue("Green", out var g) ? g : null;
        ctx.Setup($"Own pick {OwnPalette} + {OwnPattern} (green {ownGreen}); player faction {faction.defName} starts {startEra}");

        var follows = ctx.Begin("eralooks.follows-era");
        var wrong = new List<string>();
        var kept = new List<string>();
        string dir = LooksBatch.RunDir();
        foreach (var (era, look, expectedPattern) in Eras)
        {
            faction.techLevel = era;
            yield return Wait.Frames(5);
            string pattern = Theme.CurrentPatternDef?.defName;
            string green = Hex(Theme.HighlightGreen);
            string expectedGreen = CoreLooks.Green(look) ?? "2CAC41"; // slate leaves green to the theme: Core's 2CAC41
            ctx.Setup($"{era}: pattern {pattern}, green {green}, shown {EraLooksMod.ShownLook ?? "own pick"}");
            if (pattern != expectedPattern) wrong.Add($"{era} pattern {pattern}, expected {expectedPattern}");
            if (green != expectedGreen) wrong.Add($"{era} green {green}, expected {expectedGreen}");
            string saved = core.colors.TryGetValue("Green", out var s) ? s : null;
            if (core.patternDef != OwnPattern || saved != ownGreen)
                kept.Add($"{era}: saved pattern {core.patternDef}, saved green {saved}");
            var factions = DefDatabase<MainButtonDef>.GetNamedSilentFail("Factions");
            if (factions != null)
            {
                Find.MainTabsRoot.SetCurrentTab(factions, playSound: false);
                yield return Wait.Frames(20);
                LooksBatch.Capture(dir, "switch-" + era.ToString().ToLowerInvariant());
                yield return Wait.Frames(10);
                Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
            }
        }
        follows.Check(wrong.Count == 0, string.Join("; ", wrong));
        ctx.Begin("eralooks.own-pick-kept").Check(kept.Count == 0, "the player's saved pick changed: " + string.Join("; ", kept));

        // The player's choices: medieval keeps their own palette under the honeycomb; industrial gets slate under rock art.
        var choice = ctx.Begin("eralooks.per-era-choice");
        var settings = EraLooksMod.Settings;
        settings.SetPalette(EraGroup.Medieval, EraLooksSettings.OwnPick);
        settings.SetPattern(EraGroup.Medieval, "UIRethemedCore_Honeycomb");
        settings.SetPalette(EraGroup.Industrial, "UIRethemedCore_Slate");
        settings.SetPattern(EraGroup.Industrial, "EraLooks_RockArt");
        var chosen = new List<string>();
        foreach (var (era, pattern, green) in new[] { (TechLevel.Medieval, "UIRethemedCore_Honeycomb", ownGreen), (TechLevel.Industrial, "EraLooks_RockArt", CoreLooks.Green("UIRethemedCore_Slate") ?? "2CAC41") })
        {
            faction.techLevel = era;
            yield return Wait.Frames(5);
            string shownPattern = Theme.CurrentPatternDef?.defName, shownGreen = Hex(Theme.HighlightGreen);
            ctx.Setup($"Chosen look, {era}: pattern {shownPattern}, green {shownGreen}");
            if (shownPattern != pattern || shownGreen != green) chosen.Add($"{era} shows {shownPattern} / {shownGreen}, expected {pattern} / {green}");
        }
        settings.ResetLooks();
        choice.Check(chosen.Count == 0, string.Join("; ", chosen));

        var off = ctx.Begin("eralooks.follow-off");
        faction.techLevel = TechLevel.Medieval;
        EraLooksMod.Settings.followEra = false;
        yield return Wait.Frames(5);
        string offPattern = Theme.CurrentPatternDef?.defName, offGreen = Hex(Theme.HighlightGreen);
        ctx.Setup($"Follow the era off, Medieval: pattern {offPattern}, green {offGreen}");
        off.Check(offPattern == OwnPattern && offGreen == ownGreen, $"shows {offPattern} / {offGreen}, expected the own pick {OwnPattern} / {ownGreen}");

        var hatch = ctx.Begin("eralooks.era-hatch-lines");
        var hatches = Eras.Where(e => e.pattern.StartsWith("EraLooks_")).Select(e => (look: e.pattern, PatternDef.HatchFor(DefDatabase<PatternDef>.GetNamed(e.pattern))?.defName)).ToList();
        ctx.Setup("Hatching: " + string.Join(", ", hatches.Select(h => $"{h.look} -> {h.defName}")));
        hatch.Check(hatches.All(h => h.defName == "UIRethemedCore_Lines"), "an era pattern doesn't hatch with UIRethemedCore_Lines");

        // A screenshot of this mod's settings page with its per-era rows, for review.
        EraLooksMod.Settings.followEra = true;
        Find.WindowStack.Add(new Dialog_ModSettings(LoadedModManager.GetMod<EraLooksMod>()));
        yield return Wait.Frames(30);
        LooksBatch.Capture(dir, "eralooks-settings");
        yield return Wait.Frames(10);
        Find.WindowStack.TryRemove(typeof(Dialog_ModSettings), doCloseSound: false);

        faction.techLevel = startEra;
        CoreLooks.Apply("UIRethemedCore_Slate", "UIRethemedCore_Honeycomb");
        yield return Wait.Frames(5);
    }

    private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
}

// LFS Core's own setters for the player's pick (internal, so by reflection), as its settings page calls them.
internal static class CoreLooks
{
    private static readonly System.Type Look = AccessTools.TypeByName("UIRethemed.Core.ThemeLook");
    private static readonly MethodInfo ApplyPalette = AccessTools.Method(Look, "Apply", new[] { typeof(ThemePaletteDef), typeof(string) });
    private static readonly MethodInfo PickPattern = AccessTools.Method(Look, "Pick", new[] { typeof(PatternDef) });
    private static readonly PropertyInfo Colors = AccessTools.Property(typeof(ThemePaletteDef), "Colors");

    internal static bool Ready => ApplyPalette != null && PickPattern != null;

    internal static void Apply(string palette, string pattern)
    {
        ApplyPalette.Invoke(null, new object[] { DefDatabase<ThemePaletteDef>.GetNamed(palette), null });
        PickPattern.Invoke(null, new object[] { DefDatabase<PatternDef>.GetNamed(pattern) });
    }

    // A palette's theme colour as Core stores it (RRGGBB), or null when it leaves the colour to the theme.
    internal static string Green(string palette)
    {
        var raw = Colors.GetValue(DefDatabase<ThemePaletteDef>.GetNamed(palette)) as System.Collections.IDictionary;
        if (raw == null) return null;
        foreach (System.Collections.DictionaryEntry e in raw)
            if (e.Key.ToString() == "Green") return ((string)e.Value).ToUpperInvariant();
        return null;
    }
}
