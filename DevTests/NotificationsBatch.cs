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

// Visual tier: what UI Rethemed - Notifications draws, in each era look. Notifications draws through LFS Core, so the era
// look reaches it with no patch from Era Looks; these shots are how a developer sees that it does, and how its letter cards,
// alerts, messages and game conditions sit in each palette. The quickstart map has none of them, so the batch makes them:
// - a letter of every standard letter def (positive, neutral, negative, small and big threat and the rest);
// - real incidents through IncidentWorker.TryExecute (game conditions such as an eclipse and solar flare, a resource pod),
//   which Notifications also watches for its Calendar;
// - a message of every message type, sent again before each look as messages fade in real time;
// - alerts of each priority, from a downed colonist (critical) and a malnourished one, besides those the quickstart raises.
// Shots per look: the map with all of them, a letter's and an alert's hover card, the first letter opened (clicking a card
// calls Letter.OpenLetter, so this is the game's own window), and the Calendar tab with the conditions on it.
// The hover cards are Notifications' own view of a letter or alert; HoverMouse makes them draw without a real mouse.
// eralooks-notifications shoots every look; eralooks-notifications-<look> shoots the default look and that one.
public class NotificationsBatch : Batch
{
    // Incidents that leave something for Notifications to show and harm nothing while the game stays paused.
    private static readonly string[] Incidents = { "Eclipse", "SolarFlare", "ColdSnap", "ToxicFallout", "ResourcePodCrash", "ShipChunkDrop" };

    // Vanilla's AlertsReadout checks nothing before this tick.
    private const int AlertsFrom = 700;

    private readonly string only;

    public NotificationsBatch(string only = null) => this.only = only;

    public override string Id => only == null ? "eralooks-notifications" : "eralooks-notifications-" + only;
    public override int SegmentCount => 1;
    public override IReadOnlyList<PlannedCase> Plan { get; } = new[]
    {
        new PlannedCase("eralooks.notifications-shots-saved", Levels.Mechanical),
    };

    public override IEnumerable<Wait> Segment(int index, BatchContext ctx)
    {
        var shots = ctx.Begin("eralooks.notifications-shots-saved");
        if (!CoreLooks.Ready)
        {
            shots.SetupFailed("LFS Core's ThemeLook.Apply or ThemeLook.Pick was not found; Core's internals changed");
            yield break;
        }
        if (AccessTools.TypeByName("UIRethemed.Notifications.CalendarSourceDef") == null)
        {
            shots.SetupFailed("UI Rethemed - Notifications isn't loaded; the default mod set loads it");
            yield break;
        }
        TestState.Save();
        // These shots set each look by hand, so the colony's own era must not replace it.
        EraLooksMod.Settings.followEra = false;
        var map = ctx.Map;

        RunIncidents(ctx, map);
        SendLetters(ctx);
        RaiseAlerts(ctx, map);
        Find.Selector.ClearSelection();
        // Some incidents open their own letter on arrival (the eclipse does); it would cover every shot.
        CloseDialogs();
        // Alerts are checked a few types a frame, so give the readout time to find them all.
        yield return Wait.Frames(120);
        ActiveAlerts(ctx);

        string dir = LooksBatch.RunDir();
        var paths = new List<string>();
        var calendar = DefDatabase<MainButtonDef>.GetNamedSilentFail("UIRethemedNotifications_Calendar");
        foreach (var (name, palette, pattern) in LooksBatch.Looks.Where(l => only == null || l.name == "default" || l.name == only))
        {
            CoreLooks.Apply(palette, pattern);
            SendMessages();
            yield return Wait.Frames(30);
            paths.Add(LooksBatch.Capture(dir, $"{name}-notifications"));
            yield return Wait.Frames(10);

            // Messages draw over every window, so they go before the hover and window shots.
            Messages.Clear();
            foreach (var w in HoverShots(ctx, dir, name, paths)) yield return w;

            var letter = Find.LetterStack.LettersListForReading.FirstOrDefault(l => l.Label.ToString().StartsWith("Test "));
            if (letter != null)
            {
                letter.OpenLetter();
                yield return Wait.Frames(30);
                paths.Add(LooksBatch.Capture(dir, $"{name}-letter-open"));
                yield return Wait.Frames(10);
                CloseDialogs();
            }

            if (calendar != null)
            {
                Find.MainTabsRoot.SetCurrentTab(calendar, playSound: false);
                yield return Wait.Frames(30);
                paths.Add(LooksBatch.Capture(dir, $"{name}-calendar-events"));
                yield return Wait.Frames(10);
                Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
            }
        }
        HoverMouse.Remove();
        TestState.Restore();
        yield return Wait.Frames(30);

        ctx.Setup("Screenshots: " + string.Join(", ", paths));
        var unsaved = paths.Where(p => !File.Exists(p) || new FileInfo(p).Length == 0).ToList();
        shots.Check(unsaved.Count == 0, "not written: " + string.Join(", ", unsaved));
    }

    // A big threat letter's hover card, then the critical rescue alert's (or the first alert's).
    private static IEnumerable<Wait> HoverShots(BatchContext ctx, string dir, string look, List<string> paths)
    {
        if (!HoverMouse.Install(ctx)) yield break;
        var letters = Find.LetterStack.LettersListForReading.Where(l => l.Label.ToString().StartsWith("Test ")).ToList();
        HoverMouse.TargetLetter = letters.FirstOrDefault(l => l.def == LetterDefOf.ThreatBig) ?? letters.FirstOrDefault();
        yield return Wait.Frames(3); // the letter's card rect is recorded as Notifications draws it
        if (HoverMouse.LetterRect is Rect card)
        {
            HoverMouse.Fake = card;
            yield return Wait.Frames(20);
            paths.Add(LooksBatch.Capture(dir, $"{look}-letter-hover"));
            yield return Wait.Frames(10);
        }
        else ctx.Setup($"{look}: no card rect recorded for {HoverMouse.TargetLetter?.Label}; skipped the letter hover shot");
        HoverMouse.Fake = null;
        HoverMouse.TargetLetter = null;

        if (HoverMouse.AlertRow() is (Alert alert, Rect row))
        {
            HoverMouse.Fake = row;
            yield return Wait.Frames(20);
            paths.Add(LooksBatch.Capture(dir, $"{look}-alert-hover"));
            ctx.Setup($"{look}: alert hover on {alert.GetType().Name}");
            yield return Wait.Frames(10);
        }
        else ctx.Setup($"{look}: no alert row recorded; skipped the alert hover shot");
        HoverMouse.Fake = null;
        yield return Wait.Frames(5);
    }

    // The alerts the readout shows, so a run says which ones the shots can hold.
    private static void ActiveAlerts(BatchContext ctx)
    {
        var readout = Traverse.Create(Find.UIRoot).Field("alerts").GetValue();
        var active = readout == null ? null : Traverse.Create(readout).Field("activeAlerts").GetValue() as System.Collections.IEnumerable;
        var names = active?.Cast<Alert>().Select(a => $"{a.GetType().Name} ({a.Priority})").ToList();
        ctx.Setup("Active alerts: " + (names == null ? "readout not found" : string.Join(", ", names)));
        foreach (var pawn in ctx.Map.mapPawns.FreeColonists)
            ctx.Setup($"{pawn.LabelShort}: downed {pawn.Downed}, in bed {pawn.InBed()}, food {pawn.needs?.food?.CurLevelPercentage:0.00}");
    }

    private static void CloseDialogs()
    {
        foreach (var window in Find.WindowStack.Windows.Where(w => w.layer == WindowLayer.Dialog).ToList())
            Find.WindowStack.TryRemove(window, doCloseSound: false);
    }

    private static void RunIncidents(BatchContext ctx, Map map)
    {
        var ran = new List<string>();
        foreach (var name in Incidents)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(name);
            if (def == null) { ran.Add(name + " (not loaded)"); continue; }
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            parms.forced = true;
            bool done;
            try { done = def.Worker.TryExecute(parms); }
            catch (Exception e) { done = false; ctx.Setup($"{name} threw: {e.Message}"); }
            ran.Add(name + (done ? "" : " (did not fire)"));
        }
        ctx.Setup("Incidents: " + string.Join(", ", ran));
    }

    // One letter of every letter def that a plain StandardLetter can carry; the others need a quest or a choice to draw.
    private static void SendLetters(BatchContext ctx)
    {
        var defs = DefDatabase<LetterDef>.AllDefs.Where(d => d.letterClass == typeof(StandardLetter)).OrderBy(d => d.defName).ToList();
        foreach (var def in defs)
            Find.LetterStack.ReceiveLetter($"Test {def.label ?? def.defName}", $"A test letter of the {def.defName} kind, from the Era Looks dev tests.", def);
        ctx.Setup("Letters: " + string.Join(", ", defs.Select(d => d.defName)));
    }

    // One message of every message type, so each type's colour shows.
    private static void SendMessages()
    {
        foreach (var def in DefDatabase<MessageTypeDef>.AllDefs.OrderBy(d => d.defName))
            Messages.Message($"Test {def.defName} message", def, historical: false);
    }

    // A downed colonist raises the critical "needs rescue" alert; a malnourished one the starvation alert.
    private static void RaiseAlerts(BatchContext ctx, Map map)
    {
        // The game checks no alerts in a colony's first 600 ticks, and the quickstart map has barely begun. Moving the clock
        // on, rather than running those ticks, keeps the colonists from rescuing or feeding anyone before the shots.
        var ticks = AccessTools.Field(typeof(TickManager), "ticksGameInt");
        if (ticks != null && Find.TickManager.TicksGame < AlertsFrom)
        {
            ticks.SetValue(Find.TickManager, AlertsFrom);
            ctx.Setup($"Moved the clock to tick {AlertsFrom}, past the start-of-game pause on alerts");
        }
        else if (ticks == null) ctx.Setup("TickManager.ticksGameInt not found; alerts may not show in the first 600 ticks");

        var colonists = map.mapPawns.FreeColonists.ToList();
        if (colonists.Count >= 2)
        {
            var downed = colonists[colonists.Count - 1];
            HealthUtility.DamageUntilDowned(downed, allowBleedingWounds: false);
            ctx.Setup($"Downed {downed.LabelShort} for the rescue alert");
        }
        if (colonists.Count >= 1 && colonists[0].needs?.food != null)
        {
            colonists[0].needs.food.CurLevel = 0f;
            var malnutrition = HediffMaker.MakeHediff(HediffDefOf.Malnutrition, colonists[0]);
            malnutrition.Severity = 0.3f;
            colonists[0].health.AddHediff(malnutrition);
            ctx.Setup($"Starved {colonists[0].LabelShort} for the starvation alert");
        }
    }
}


// Makes Notifications draw one letter's or alert's hover card for the shots. The runner can't move the real mouse, and setting
// Event.current.mousePosition did not reach Notifications' checks (they still saw the real cursor, 2026-11-17). So the
// Mouse.IsOver calls in the methods that decide hovering are swapped for IsOver below, which also answers yes over the target's
// rect: Notifications' LetterCards.CheckForMouseOverTextAt and AlertRows.DrawInfoPane, and the game's
// AlertsReadout.AlertsReadoutOnGUI, which picks the alert whose info pane is drawn. Notifications' internals are reached by name,
// and a missing one skips the hover shots with a setup line rather than failing the batch. Removed at the batch's end (and by
// OnBatchEnd), so it never runs outside these shots.
internal static class HoverMouse
{
    private const string Id = "kylemhb.uirethemederalooks.devtests.hover";
    private static Harmony harmony;

    // The rect to treat as under the mouse, or null for the real mouse only.
    internal static Rect? Fake;
    internal static Letter TargetLetter;
    internal static Rect? LetterRect;

    private static readonly Type Cards = AccessTools.TypeByName("UIRethemed.Notifications.LetterCards");
    private static readonly Type Rows = AccessTools.TypeByName("UIRethemed.Notifications.AlertRows");
    private static readonly MethodInfo CardRect = AccessTools.Method(Cards, "CardRect");
    private static readonly MethodInfo TimeOf = AccessTools.Method(Cards, "TimeOf");
    private static readonly MethodInfo LetterY = AccessTools.Method(AccessTools.TypeByName("UIRethemed.Notifications.Placement"), "LetterY");
    private static readonly MethodInfo Label = AccessTools.Method(typeof(Letter), "PostProcessedLabel");
    private static readonly MethodInfo RealIsOver = AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver), new[] { typeof(Rect) });

    internal static bool Install(BatchContext ctx)
    {
        if (harmony != null) return true;
        var check = AccessTools.Method(Cards, "CheckForMouseOverTextAt");
        var pane = AccessTools.Method(Rows, "DrawInfoPane");
        var readout = AccessTools.Method(typeof(AlertsReadout), nameof(AlertsReadout.AlertsReadoutOnGUI));
        if (check == null || pane == null || readout == null || CardRect == null || TimeOf == null || LetterY == null || Label == null || RealIsOver == null)
        {
            ctx.Setup("Notifications' LetterCards, AlertRows or Placement changed; skipped the hover shots");
            return false;
        }
        harmony = new Harmony(Id);
        var swap = new HarmonyMethod(typeof(HoverMouse), nameof(SwapIsOver));
        foreach (var method in new[] { check, pane, readout }) harmony.Patch(method, transpiler: swap);
        harmony.Patch(check, prefix: new HarmonyMethod(typeof(HoverMouse), nameof(Record)));
        return true;
    }

    internal static void Remove()
    {
        harmony?.UnpatchAll(Id);
        harmony = null;
        Fake = null;
        TargetLetter = null;
        LetterRect = null;
    }

    public static bool IsOver(Rect rect) => (Fake is Rect fake && rect.Contains(fake.center)) || Mouse.IsOver(rect);

    private static IEnumerable<CodeInstruction> SwapIsOver(IEnumerable<CodeInstruction> instructions)
    {
        var mine = AccessTools.Method(typeof(HoverMouse), nameof(IsOver));
        foreach (var code in instructions)
        {
            if (code.Calls(RealIsOver)) code.operand = mine;
            yield return code;
        }
    }

    // Notifications' own card rect for the target letter at the height it is drawn at, as its hover check works it out.
    private static void Record(Letter __0, float __1)
    {
        if (__0 == null || __0 != TargetLetter) return;
        string label = (string)Label.Invoke(__0, null);
        float slot = (float)LetterY.Invoke(null, new object[] { __1 });
        LetterRect = (Rect)CardRect.Invoke(null, new object[] { slot, label, (string)TimeOf.Invoke(null, new object[] { __0 }) });
    }

    // The critical rescue alert's row as Notifications last drew it, or the first row.
    internal static (Alert alert, Rect row)? AlertRow()
    {
        if (AccessTools.Field(Rows, "rows")?.GetValue(null) is not Dictionary<Alert, Rect> rows || rows.Count == 0) return null;
        var pick = rows.FirstOrDefault(r => r.Key is Alert_ColonistNeedsRescuing);
        if (pick.Key == null) pick = rows.First();
        return (pick.Key, pick.Value);
    }
}
