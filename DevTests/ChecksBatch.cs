using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorldDevTests;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace EraLooks.DevTests;

// The quick checks, for every code change: no screenshots, one short batch. Most of what Era Looks does is Harmony patches
// on other mods' internals, found by name or by scanning their IL, so an LFS update can quietly turn one off; the mod then
// logs a warning and carries on. These cases fail on that, and on the behaviour each patch exists for.
// Expected behaviour: GLOSSARY.md, CHANGELOG.md (status colours, theme copies, plain animal icons) and docs/adr/0003, 0005.
public class ChecksBatch : Batch
{
    private const string Owner = "kylemhb.uirethemederalooks";

    public override string Id => "eralooks-checks";
    public override int SegmentCount => 2;
    public override IReadOnlyList<PlannedCase> Plan { get; } = new[]
    {
        new PlannedCase("eralooks.defs-load", Levels.Mechanical),
        new PlannedCase("eralooks.patches-applied", Levels.Mechanical),
        new PlannedCase("eralooks.status-green", Levels.Mechanical),
        new PlannedCase("eralooks.theme-copies-sync", Levels.Mechanical),
        new PlannedCase("eralooks.faction-pattern", Levels.Mechanical),
        new PlannedCase("eralooks.reload-keeps-look", Levels.Mechanical),
    };

    public override IEnumerable<Wait> Segment(int index, BatchContext ctx)
    {
        if (index == 1)
        {
            foreach (var w in AfterReload(ctx)) yield return w;
            yield break;
        }
        TestState.Save();
        EraLooksMod.Settings.followEra = true;
        DefsLoad(ctx);
        PatchesApplied(ctx);
        foreach (var w in StatusGreen(ctx)) yield return w;
        foreach (var w in ThemeCopiesSync(ctx)) yield return w;
        FactionPattern(ctx);

        // The load path: a save and reload with Follow the era on. A NullReferenceException in CoreBridge while a game loaded
        // went unseen until 2026-11-17 (UpdateErrors now logs it, and the run fails on it).
        TestState.Restore();
        TestState.Save();
        EraLooksMod.Settings.followEra = true;
        ctx.Remember("modErrors", CaseErrors.ModMark.ToString());
        ctx.Setup($"Reloading in {Faction.OfPlayer.def.techLevel} with Follow the era on");
        yield return Wait.Reload();
    }

    // Each era look's palette and pattern loads as an LFS Core def with its texture.
    private static void DefsLoad(BatchContext ctx)
    {
        var c = ctx.Begin("eralooks.defs-load");
        var missing = new List<string>();
        foreach (var (_, palette, pattern) in SwitchingBatch.Eras)
        {
            if (DefDatabase<ThemePaletteDef>.GetNamedSilentFail(palette) == null) missing.Add("palette " + palette);
            var def = DefDatabase<PatternDef>.GetNamedSilentFail(pattern);
            if (def == null) missing.Add("pattern " + pattern);
            else if (def.Tex == null) missing.Add($"texture {def.texPath}");
            else ctx.Setup($"{pattern}: texture {def.Tex.width}x{def.Tex.height}, tile {def.TileSize}, weight {def.weight}");
        }
        c.Check(missing.Count == 0, "missing: " + string.Join(", ", missing));
    }

    // Every patch the loaded mods allow is in place, and no "[EraLooks] ... changed" warning was logged at startup.
    private static void PatchesApplied(BatchContext ctx)
    {
        var c = ctx.Begin("eralooks.patches-applied");
        var counts = new Dictionary<string, int>();
        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            var info = Harmony.GetPatchInfo(method);
            foreach (var patch in info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
            {
                if (patch.owner != Owner) continue;
                string key = patch.PatchMethod.DeclaringType.Name + "." + patch.PatchMethod.Name;
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }
        }
        ctx.Setup("Patches: " + string.Join(", ", counts.OrderBy(p => p.Key).Select(p => $"{p.Key} x{p.Value}")));

        // Each check: the patch method, the least number of methods it should patch, and why.
        var wanted = new List<(string patch, int least, string what)>
        {
            ("CoreBridge.Update", 2, "the once-a-frame update on Root_Play and Root_Entry"),
            ("PatternScroll.Tiled", 1, "Core's Theme.Tiled"),
            ("StatusColours.Redirect", 1, "status reads of green in the UI Rethemed modules"),
        };
        var modules = (ValueTuple<string, string>[])AccessTools.Field(AccessTools.TypeByName("EraLooks.ThemeCopies"), "Modules").GetValue(null);
        var loaded = modules.Where(m => AccessTools.TypeByName($"UIRethemed.{m.Item1}.Theme") != null).Select(m => m.Item1).ToList();
        ctx.Setup("Theme-copy modules loaded: " + string.Join(", ", loaded));
        wanted.Add(("ThemeCopies.Pattern", loaded.Count, "each loaded theme copy's Theme.Pattern"));
        if (loaded.Contains("Animals")) wanted.Add(("ThemeCopies.PlainStart", 1, "the animal icon and header draws"));
        if (AccessTools.TypeByName("UIRethemed.Factions.FactionsApi") != null)
        {
            wanted.Add(("FactionEras.RowPrefix", 1, "Factions' FactionRail.Row"));
            wanted.Add(("FactionEras.LitFacePostfix", 1, "Core's CoreWidgets.LitFace"));
            wanted.Add(("FactionEras.ScopePrefix", 3, "Factions' Settlements, Overview and Leader pages"));
        }
        var short_ = wanted.Where(w => (counts.TryGetValue(w.patch, out int n) ? n : 0) < w.least)
            .Select(w => $"{w.patch} patched {(counts.TryGetValue(w.patch, out int n) ? n : 0)}, expected at least {w.least} ({w.what})").ToList();

        var warnings = Log.Messages.Where(m => m.type == LogMessageType.Warning && m.text != null && m.text.StartsWith("[EraLooks]"))
            .Select(m => m.text).Distinct().ToList();
        foreach (var w in warnings) c.Evidence("warning: " + w);
        c.Check(short_.Count == 0 && warnings.Count == 0, string.Join("; ", short_.Concat(warnings)));
    }

    // Under an era palette with its own green, a status read shows Core's standard green while the accent keeps the era's.
    // Under the player's own pick, both show the theme's green.
    private static IEnumerable<Wait> StatusGreen(BatchContext ctx)
    {
        var c = ctx.Begin("eralooks.status-green");
        var status = AccessTools.PropertyGetter(AccessTools.TypeByName("EraLooks.StatusColours"), "HighlightGreen");
        var defaults = AccessTools.Field(AccessTools.TypeByName("UIRethemed.Core.ThemeLook"), "Defaults")?.GetValue(null);
        if (status == null || defaults == null || !CoreLooks.Ready)
        {
            c.Error("StatusColours.HighlightGreen, Core's ThemeLook.Defaults or its setters not found");
            yield break;
        }
        string standard = Hex(Traverse.Create(defaults).Field<Color>("HighlightGreen").Value);
        var era = SwitchingBatch.Eras.FirstOrDefault(e => CoreLooks.Green(e.look) is string g && g != standard);
        if (era.look == null)
        {
            c.SetupFailed("no era palette has a green of its own");
            yield break;
        }
        Faction.OfPlayer.def.techLevel = era.era;
        yield return Wait.Frames(5);
        string accent = Hex(Theme.HighlightGreen), shown = Hex((Color)status.Invoke(null, null));
        ctx.Setup($"{era.era} ({era.look}): accent {accent}, status green {shown}, standard {standard}");
        var wrong = new List<string>();
        if (accent == standard) wrong.Add($"{era.era}'s accent is the standard green, so the era palette didn't apply");
        if (shown != standard) wrong.Add($"{era.era} status green {shown}, expected the standard {standard}");

        EraLooksMod.Settings.followEra = false;
        CoreLooks.Apply("UIRethemedCore_Midnight", "UIRethemedCore_Grid");
        yield return Wait.Frames(5);
        string ownAccent = Hex(Theme.HighlightGreen), ownShown = Hex((Color)status.Invoke(null, null));
        ctx.Setup($"Own pick (midnight): accent {ownAccent}, status green {ownShown}");
        if (ownShown != ownAccent) wrong.Add($"own pick status green {ownShown}, expected it to pass through as {ownAccent}");
        EraLooksMod.Settings.followEra = true;
        c.Check(wrong.Count == 0, string.Join("; ", wrong));
    }

    // Each loaded module that keeps its own palette object shows Core's colours after a frame in an era look.
    private static IEnumerable<Wait> ThemeCopiesSync(BatchContext ctx)
    {
        var c = ctx.Begin("eralooks.theme-copies-sync");
        Faction.OfPlayer.def.techLevel = TechLevel.Medieval;
        yield return Wait.Frames(5);
        var modules = (ValueTuple<string, string>[])AccessTools.Field(AccessTools.TypeByName("EraLooks.ThemeCopies"), "Modules").GetValue(null);
        var wrong = new List<string>();
        int compared = 0;
        foreach (var (module, _) in modules)
        {
            var palette = AccessTools.Field(AccessTools.TypeByName($"UIRethemed.{module}.Theme"), "Colors")?.GetValue(null);
            if (palette == null) continue; // not loaded, or keeps its colours as fields (redirected, not copied)
            foreach (var field in AccessTools.GetDeclaredFields(palette.GetType()).Where(f => f.FieldType == typeof(Color)))
            {
                var core = AccessTools.PropertyGetter(typeof(Theme), field.Name);
                if (core == null || !core.IsStatic || core.ReturnType != typeof(Color)) continue;
                compared++;
                string copy = Hex((Color)field.GetValue(palette)), want = Hex((Color)core.Invoke(null, null));
                if (copy != want) wrong.Add($"{module}.{field.Name} {copy}, Core {want}");
            }
        }
        ctx.Setup($"Compared {compared} copied colours in the medieval look");
        if (compared == 0) c.SetupFailed("no loaded module keeps a palette object to compare");
        else c.Check(wrong.Count == 0, string.Join("; ", wrong));
    }

    // With Show each faction's era on, a faction's pattern is its tech level's; off, or with the player's own pick for that
    // level, it is left to Factions (null).
    private static void FactionPattern(BatchContext ctx)
    {
        var c = ctx.Begin("eralooks.faction-pattern");
        var patternOf = AccessTools.Method(AccessTools.TypeByName("EraLooks.FactionEras"), "PatternOf");
        var faction = Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => !f.IsPlayer && EraLooksSettings.GroupOf(f.def.techLevel) != null);
        if (patternOf == null || faction == null)
        {
            c.SetupFailed(patternOf == null ? "FactionEras.PatternOf not found" : "no visible faction with an era");
            return;
        }
        var group = EraLooksSettings.GroupOf(faction.def.techLevel).Value;
        string Of() => (patternOf.Invoke(null, new object[] { faction }) as PatternDef)?.defName ?? "null";
        var settings = EraLooksMod.Settings;

        settings.factionEras = true;
        string on = Of();
        settings.SetPattern(group, EraLooksSettings.OwnPick);
        string own = Of();
        settings.ResetLooks();
        settings.factionEras = false;
        string off = Of();
        string want = EraLooksSettings.DefaultPattern(group);
        ctx.Setup($"{faction.Name} ({group}): on {on}, own pick {own}, off {off}");
        c.Check(on == want && own == "null" && off == "null", $"expected on {want}, own pick null, off null");
    }

    private static IEnumerable<Wait> AfterReload(BatchContext ctx)
    {
        var c = ctx.Begin("eralooks.reload-keeps-look");
        yield return Wait.Frames(10);
        var era = Faction.OfPlayer.def.techLevel;
        var group = EraLooksSettings.GroupOf(era);
        string want = group == null ? null : EraLooksSettings.DefaultPattern(group.Value);
        string shown = Theme.CurrentPatternDef?.defName;
        long before = long.Parse(ctx.Recall("modErrors") ?? "0"), errors = CaseErrors.ModMark - before;
        ctx.Setup($"After reload in {era}: pattern {shown}, palette {EraLooksMod.ShownLook ?? "own pick"}, {errors} Era Looks errors while loading");
        c.Check(shown == want && errors == 0, $"expected pattern {want} and no Era Looks errors");
        TestState.Restore();
    }

    private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
}
