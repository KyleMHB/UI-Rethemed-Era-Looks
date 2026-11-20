using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorldDevTests;
using UnityEngine;
using Verse;

namespace EraLooks.DevTests;

// What Era Looks costs on the paths the shared perf batch does not see, as it measures ticks only. Era Looks does its work
// once a frame (CoreBridge.Update, which also copies Core's colours into the theme copies of the other UI Rethemed modules)
// and on every pattern those modules draw (the ThemeCopies prefix that hands the draw to Core). Each case states how many
// calls one realistic situation makes and the budget for that scale.
public class PerfBatch : Batch
{
    // A busy window such as Quests draws its title banner, group headers and lit faces: about 50 pattern draws a frame.
    private const int DrawsPerFrame = 50;
    private const double DrawBudgetMs = 0.5;
    // One second of frames at 60 fps.
    private const int FramesPerSecond = 60;
    private const double FrameBudgetMs = 1;

    public override string Id => "eralooks-perf";
    public override int SegmentCount => 1;
    public override TimeSpan WallLimit => TimeSpan.FromMinutes(5);
    public override IReadOnlyList<PlannedCase> Plan { get; } = new[]
    {
        new PlannedCase("perf.frame-update", Levels.Mechanical),
        new PlannedCase("perf.pattern-draw", Levels.Mechanical),
    };

    // A timing job run inside the next Repaint, where GUI drawing is valid; the harness has no GUI hook of its own.
    private static Action onRepaint;

    private static void RunOnRepaint()
    {
        if (onRepaint == null || Event.current?.type != EventType.Repaint) return;
        var job = onRepaint;
        onRepaint = null;
        job();
    }

    public override IEnumerable<Wait> Segment(int index, BatchContext ctx)
    {
        var bridge = AccessTools.TypeByName("EraLooks.CoreBridge");
        var update = bridge == null ? null : AccessTools.Method(bridge, "Update");
        var c = ctx.Begin("perf.frame-update").Expect("msAtScale", "< " + FrameBudgetMs);
        if (update == null) c.Error("EraLooks.CoreBridge.Update not found");
        else
        {
            var call = (Action)Delegate.CreateDelegate(typeof(Action), update);
            var ms = PerfTiming.MsPerCall(call, calls: 2000);
            c.Observe("msPerCall", ms).Observe("scale", FramesPerSecond).Observe("msAtScale", ms * FramesPerSecond)
                .Evidence($"CoreBridge.Update: {CaseScope.Format(ms)} ms a frame, median of rounds of 2000 calls")
                .Evidence($"At {FramesPerSecond} frames (one second at 60 fps): {CaseScope.Format(ms * FramesPerSecond)} ms")
                .Check(ms * FramesPerSecond < FrameBudgetMs, $"the frame update takes {CaseScope.Format(ms * FramesPerSecond)} ms a second, over the {FrameBudgetMs} ms budget");
        }

        var d = ctx.Begin("perf.pattern-draw").Expect("addedMsAtScale", "< " + DrawBudgetMs);
        var theme = AccessTools.TypeByName("UIRethemed.Quests.Theme");
        var pattern = theme == null ? null : AccessTools.Method(theme, "Pattern", new[] { typeof(Rect), typeof(Vector2) });
        if (pattern == null)
        {
            d.Error("UI Rethemed - Quests' Theme.Pattern(Rect, Vector2) not found; the default mod set loads Quests");
            yield break;
        }

        var harmony = new Harmony("kylemhb.uirethemederalooks.devtests.perf");
        var onGui = AccessTools.Method(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI));
        harmony.Patch(onGui, postfix: new HarmonyMethod(typeof(PerfBatch), nameof(RunOnRepaint)));
        PatchCost cost = null;
        string failure = null;
        // The size of a wide title banner.
        var banner = new Rect(10f, 10f, 1200f, 40f);
        var draw = (Action<Rect, Vector2>)Delegate.CreateDelegate(typeof(Action<Rect, Vector2>), pattern);
        onRepaint = () =>
        {
            try { cost = PerfTiming.PatchCost(pattern, () => draw(banner, Vector2.zero), calls: 500); }
            catch (Exception e) { failure = e.Message; }
        };
        yield return Wait.Frames(5);
        onRepaint = null;
        harmony.Unpatch(onGui, HarmonyPatchType.Postfix, harmony.Id);

        if (failure != null) d.Error(failure);
        else if (cost == null) d.Error("no Repaint ran the timing within 5 frames");
        else
        {
            cost.Report(d, DrawsPerFrame, "pattern draws in one frame of a busy Quests window, at the title banner's size");
            d.Check(cost.AddedMsPerCall * DrawsPerFrame < DrawBudgetMs, $"the pattern prefix adds {CaseScope.Format(cost.AddedMsPerCall * DrawsPerFrame)} ms a frame at {DrawsPerFrame} draws, over the {DrawBudgetMs} ms budget");
        }
    }
}
