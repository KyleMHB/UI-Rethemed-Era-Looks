using System;
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

// Expected behaviour: GLOSSARY.md "Era look" and Defs/EraLooks.xml. Each era look's palette and pattern loads as an LFS Core def
// with its texture, and the batch saves screenshots of the default look and every era look for the art review.
// The screenshots are not a case of their own: the developer judges what they show. For each look the batch picks its palette and
// pattern through Core's own setters (ThemeLook.Apply and ThemeLook.Pick, internal, so by reflection), the same calls Core's
// settings page makes, then captures the colony with a colonist selected and the Architect tab open, and Core's settings page.
// The runner's own save-data folder holds these settings, so the player's Core settings are never touched.
public class LooksBatch : Batch
{
    private static readonly (string name, string palette, string pattern)[] Looks =
    {
        ("default", "UIRethemedCore_Slate", "UIRethemedCore_Honeycomb"),
        ("animal", "EraLooks_Bone", "EraLooks_Scree"),
        ("neolithic", "EraLooks_RockArt", "EraLooks_SplitWood"),
        ("medieval", "EraLooks_Tapestry", "EraLooks_CutStone"),
        ("industrial", "EraLooks_Engraving", "EraLooks_TreadPlate"),
        ("spacer", "EraLooks_Spacer", "EraLooks_HullPanels"),
        ("ultra", "EraLooks_Ultratech", "EraLooks_Honeycomb"),
        ("archotech", "EraLooks_Archotech", "EraLooks_EtchedCircuits"),
    };

    private static readonly (string shot, string tab)[] Tabs =
    {
        ("menu", "Menu"), ("factions", "Factions"), ("ideology", "Ideos"), ("relationships", "UIRethemedRelationships_Relationships"),
        ("architect", "Architect"), ("animals", "Animals"), ("wildlife", "Wildlife"), ("quests", "Quests"), ("history", "History"),
        ("calendar", "UIRethemedNotifications_Calendar"), ("work", "Work"),
    };

    // The selected colonist's inspector tabs, drawn by UI Rethemed - Inspector Tabs when it is loaded.
    private static readonly (string shot, Type tab)[] InspectTabs =
    {
        ("needs", typeof(ITab_Pawn_Needs)), ("health", typeof(ITab_Pawn_Health)), ("bio", typeof(ITab_Pawn_Character)),
        ("gear", typeof(ITab_Pawn_Gear)), ("social", typeof(ITab_Pawn_Social)),
    };

    // Need levels low, middling and high in turn, so the shots show each status colour (UI Rethemed colours a need red under
    // 20%, yellow under 45% and green above).
    private static readonly float[] NeedLevels = { 0.12f, 0.35f, 0.85f };

    public override string Id => "eralooks-screens";
    public override int SegmentCount => 1;
    public override IReadOnlyList<PlannedCase> Plan { get; } = new[]
    {
        new PlannedCase("eralooks.defs-load", Levels.Mechanical),
        new PlannedCase("eralooks.screenshots-saved", Levels.Mechanical),
    };

    public override IEnumerable<Wait> Segment(int index, BatchContext ctx)
    {
        var look = AccessTools.TypeByName("UIRethemed.Core.ThemeLook");
        var apply = AccessTools.Method(look, "Apply", new[] { typeof(ThemePaletteDef), typeof(string) });
        var pick = AccessTools.Method(look, "Pick", new[] { typeof(PatternDef) });

        var loading = ctx.Begin("eralooks.defs-load");
        var missing = new List<string>();
        foreach (var (_, palette, pattern) in Looks.Skip(1))
        {
            if (DefDatabase<ThemePaletteDef>.GetNamedSilentFail(palette) == null) missing.Add("palette " + palette);
            var def = DefDatabase<PatternDef>.GetNamedSilentFail(pattern);
            if (def == null) missing.Add("pattern " + pattern);
            else if (def.Tex == null) missing.Add($"texture {def.texPath}");
            else ctx.Setup($"{pattern}: texture {def.Tex.width}x{def.Tex.height}, tile {def.TileSize}, weight {def.weight}");
        }
        loading.Check(missing.Count == 0, "missing: " + string.Join(", ", missing));

        var shots = ctx.Begin("eralooks.screenshots-saved");
        if (apply == null || pick == null)
        {
            shots.SetupFailed("LFS Core's ThemeLook.Apply or ThemeLook.Pick was not found; Core's internals changed");
            yield break;
        }
        var core = LoadedModManager.ModHandles.FirstOrDefault(m => m.GetType().FullName == "UIRethemed.Core.UIRethemedCoreMod");
        var colonist = ctx.Map.mapPawns.FreeColonists.FirstOrDefault();
        var toggleTab = AccessTools.Method(typeof(InspectPaneUtility), "ToggleTab");
        if (toggleTab == null) ctx.Setup("InspectPaneUtility.ToggleTab was not found; skipped the inspector tab shots");
        string dir = RunDir();
        var paths = new List<string>();
        // These shots set each look by hand, so the colony's own era must not replace it.
        EraLooksMod.Settings.followEra = false;
        foreach (var (name, palette, pattern) in Looks)
        {
            apply.Invoke(null, new object[] { DefDatabase<ThemePaletteDef>.GetNamed(palette), null });
            pick.Invoke(null, new object[] { DefDatabase<PatternDef>.GetNamed(pattern) });
            Find.Selector.ClearSelection();
            if (colonist != null) Find.Selector.Select(colonist, playSound: false, forceDesignatorDeselect: false);
            // The main bar's own menu and the tabs of the LFS modules built on Core (Factions, Ideology, Relationships), then those
            // that still carry their own copy of the theme, which Era Looks keeps in step with Core. A tab whose mod or DLC is missing is skipped.
            foreach (var (shot, tab) in Tabs)
            {
                var def = DefDatabase<MainButtonDef>.GetNamedSilentFail(tab);
                if (def == null) continue;
                Find.MainTabsRoot.SetCurrentTab(def, playSound: false);
                SetLevels(colonist);
                yield return Wait.Frames(30);
                paths.Add(Capture(dir, $"{name}-{shot}"));
                yield return Wait.Frames(10);
                Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
            }

            if (colonist != null && toggleTab != null)
            {
                var pane = (MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
                foreach (var (shot, type) in InspectTabs)
                {
                    var tab = colonist.GetInspectTabs()?.FirstOrDefault(t => t.GetType() == type);
                    if (tab == null) continue;
                    if (pane.OpenTabType != type) toggleTab.Invoke(null, new object[] { tab, pane });
                    SetLevels(colonist);
                    yield return Wait.Frames(30);
                    paths.Add(Capture(dir, $"{name}-{shot}"));
                    yield return Wait.Frames(10);
                }
                pane.OpenTabType = null;
            }

            if (core != null) Find.WindowStack.Add(new Dialog_ModSettings(core));
            yield return Wait.Frames(30);
            paths.Add(Capture(dir, name + "-settings"));
            yield return Wait.Frames(10);
            Find.WindowStack.TryRemove(typeof(Dialog_ModSettings), doCloseSound: false);
        }
        // This mod's own settings page, for the Workshop gallery, in the medieval look.
        var own = LoadedModManager.ModHandles.FirstOrDefault(m => m is EraLooksMod);
        var medieval = Looks.First(l => l.name == "medieval");
        apply.Invoke(null, new object[] { DefDatabase<ThemePaletteDef>.GetNamed(medieval.palette), null });
        pick.Invoke(null, new object[] { DefDatabase<PatternDef>.GetNamed(medieval.pattern) });
        if (own != null)
        {
            // Shown with Follow the era ticked, as a player sees it by default.
            EraLooksMod.Settings.followEra = true;
            Find.WindowStack.Add(new Dialog_ModSettings(own));
            yield return Wait.Frames(30);
            paths.Add(Capture(dir, "eralooks-settings"));
            yield return Wait.Frames(10);
            Find.WindowStack.TryRemove(typeof(Dialog_ModSettings), doCloseSound: false);
            EraLooksMod.Settings.followEra = false;
        }

        // The faction window with Show each faction's era on, which is off by default, so each row shows its faction's era.
        bool factionEras = EraLooksMod.Settings.factionEras;
        EraLooksMod.Settings.factionEras = true;
        var factionsDef = DefDatabase<MainButtonDef>.GetNamedSilentFail("Factions");
        if (factionsDef != null)
        {
            Find.MainTabsRoot.SetCurrentTab(factionsDef, playSound: false);
            yield return Wait.Frames(30);
            paths.Add(Capture(dir, "faction-eras"));
            yield return Wait.Frames(10);
            Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
        }
        apply.Invoke(null, new object[] { DefDatabase<ThemePaletteDef>.GetNamed(Looks[0].palette), null });
        pick.Invoke(null, new object[] { DefDatabase<PatternDef>.GetNamed(Looks[0].pattern) });

        // The relations graph draws the selected faction's era as a large background, so select one faction per era.
        // Run eralooks-sandbox first in the same session to have a faction for every era.
        var select = AccessTools.Method(AccessTools.TypeByName("UIRethemed.Factions.FactionsTab"), "Select", new[] { typeof(Faction) });
        var factionsTab = DefDatabase<MainButtonDef>.GetNamedSilentFail("Factions");
        if (select != null && factionsTab != null)
        {
            var byEra = Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer).GroupBy(f => f.def.techLevel).OrderBy(g => g.Key);
            foreach (var era in byEra)
            {
                Find.MainTabsRoot.SetCurrentTab(factionsTab, playSound: false);
                yield return Wait.Frames(10);
                select.Invoke(null, new object[] { era.First() });
                yield return Wait.Frames(30);
                paths.Add(Capture(dir, "selected-" + era.Key.ToString().ToLowerInvariant()));
                yield return Wait.Frames(10);
                Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
            }
        }
        else ctx.Setup("UI Rethemed - Factions' FactionsTab.Select was not found; skipped the selected-faction shots");
        EraLooksMod.Settings.factionEras = factionEras;
        EraLooksMod.Settings.followEra = true;
        yield return Wait.Frames(30);

        ctx.Setup("Screenshots: " + string.Join(", ", paths));
        var unsaved = paths.Where(p => !File.Exists(p) || new FileInfo(p).Length == 0).ToList();
        shots.Check(core != null && unsaved.Count == 0,
            core == null ? "LFS Core's Mod class was not found, so its settings page could not open" : "not written: " + string.Join(", ", unsaved));
    }

    private static void SetLevels(Pawn colonist)
    {
        var needs = colonist?.needs?.AllNeeds;
        if (needs == null) return;
        int i = 0;
        foreach (var need in needs)
        {
            if (need is Need_Mood) continue;
            need.CurLevelPercentage = NeedLevels[i++ % NeedLevels.Length];
        }
    }

    internal static string Capture(string dir, string name)
    {
        string path = Path.Combine(dir, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        return path;
    }

    // The active run's evidence folder (the runner writes it to active.txt), or the evidence root.
    internal static string RunDir()
    {
        string pointer = Path.Combine(TestRunner.EvidenceRoot, "active.txt");
        string dir = File.Exists(pointer) ? File.ReadAllText(pointer).Trim() : null;
        return !string.IsNullOrEmpty(dir) && Directory.Exists(dir) ? dir : TestRunner.EvidenceRoot;
    }
}
