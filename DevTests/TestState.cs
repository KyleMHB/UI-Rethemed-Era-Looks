using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace EraLooks.DevTests;

// What the batches change outside their disposable colony: the player faction def's tech level (a def, so it outlives the
// colony and carries into the next batch of the session), this mod's settings and Core's own pick. Batches call Save before
// changing any of them; ModHarness.OnBatchEnd restores them, so a batch that stops early leaves nothing behind.
internal static class TestState
{
    private static FactionDef faction;
    private static TechLevel era;
    private static bool follow, factionEras, saved;

    internal static void Save()
    {
        if (saved) return;
        faction = Faction.OfPlayer.def;
        era = faction.techLevel;
        follow = EraLooksMod.Settings.followEra;
        factionEras = EraLooksMod.Settings.factionEras;
        saved = true;
    }

    internal static void Restore()
    {
        if (!saved) return;
        faction.techLevel = era;
        EraLooksMod.Settings.followEra = follow;
        EraLooksMod.Settings.factionEras = factionEras;
        EraLooksMod.Settings.ResetLooks();
        if (CoreLooks.Ready) CoreLooks.Apply("UIRethemedCore_Slate", "UIRethemedCore_Honeycomb");
        saved = false;
    }
}

// The once-a-frame update runs as a Harmony postfix on Root_Play.Update, so an exception in it reaches the engine's own
// "Update threw" log, which bypasses Verse.Log.Error and so the harness's error count (a NullReferenceException there while
// each new game loaded passed a run on 2026-11-17). This logs each distinct one through Log.Error with the mod's marker,
// so ModHarness.FailOnModErrors fails the run, and rethrows it unchanged.
[StaticConstructorOnStartup]
internal static class UpdateErrors
{
    private static readonly HashSet<string> seen = new();

    static UpdateErrors()
    {
        var update = AccessTools.Method(AccessTools.TypeByName("EraLooks.CoreBridge"), "Update");
        if (update == null) Log.Error("[EraLooks tests] EraLooks.CoreBridge.Update not found; its exceptions won't fail a run");
        else new Harmony("kylemhb.uirethemederalooks.devtests.errors").Patch(update, finalizer: new HarmonyMethod(typeof(UpdateErrors), nameof(Finalizer)));
    }

    // Once per distinct exception per batch, as it repeats every frame.
    internal static void Reset() => seen.Clear();

    private static Exception Finalizer(Exception __exception)
    {
        if (__exception != null && seen.Add(__exception.GetType().Name + __exception.StackTrace))
            Log.Error("[EraLooks] CoreBridge.Update threw: " + __exception);
        return __exception;
    }
}
