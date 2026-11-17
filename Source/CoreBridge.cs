using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace EraLooks;

// Shows the colony's era look in place of the player's own LFS Core pick while a game is loaded, through Core's public
// Theme.Override, so the player's saved pick is never written (docs/adr/0003). Harmony is only needed to run once a frame.
// The sparse background textures and the fine-line hatching are set on the era PatternDefs (largeTexPath, hatchWith).
internal static class CoreBridge
{
    internal static bool Ready { get; private set; }

    // The era look shown now: its palette's def name and its pattern's, or nulls for the player's own pick.
    internal static string Shown { get; private set; }
    private static string shownPatternName;

    internal static void Patch(Harmony harmony)
    {
        if (AccessTools.Method(typeof(Theme), "Override") == null)
        {
            // not translated
            Log.Warning("[EraLooks] This version of UI Rethemed - Core has no Theme.Override, so the era looks won't switch on their own; update Core, or pick them in Core's settings.");
            return;
        }
        harmony.Patch(AccessTools.Method(typeof(Root_Play), nameof(Root_Play.Update)), postfix: new HarmonyMethod(typeof(CoreBridge), nameof(Update)));
        harmony.Patch(AccessTools.Method(typeof(Root_Entry), nameof(Root_Entry.Update)), postfix: new HarmonyMethod(typeof(CoreBridge), nameof(Update)));
        FactionEras.Patch(harmony);
        ThemeCopies.Patch(harmony);
        StatusColours.Patch(harmony);
        PatternScroll.Patch(harmony);
        Ready = true;
    }

    // The look the colony's era asks for (palette, pattern), or nulls for the player's own pick: at the main menu and with
    // Follow the era off. Core sets the override aside itself while its own settings page is open.
    internal static (string palette, string pattern) Wanted()
    {
        if (EraLooksMod.Settings == null || !EraLooksMod.Settings.followEra || Current.ProgramState != ProgramState.Playing) return default;
        var player = Find.FactionManager?.OfPlayer;
        return player?.def == null ? default : LookFor(player.def.techLevel);
    }

    // The player's choice for the era's group, or the era look made for it. The own pick (EraLooksSettings.OwnPick) is a null, so
    // that half shows the player's own palette or pattern.
    internal static (string palette, string pattern) LookFor(TechLevel era)
    {
        var group = EraLooksSettings.GroupOf(era);
        if (group == null) return default;
        var settings = EraLooksMod.Settings;
        string palette = settings?.PaletteFor(group.Value) ?? EraLooksSettings.DefaultPalette(group.Value);
        string pattern = settings?.PatternFor(group.Value) ?? EraLooksSettings.DefaultPattern(group.Value);
        return (palette == EraLooksSettings.OwnPick ? null : palette, pattern == EraLooksSettings.OwnPick ? null : pattern);
    }

    // Once a frame, in the menus and in play. Core's override stays set across a return to the main menu, so this clears it there.
    // Core is only called when the look changes, as each call looks up the calling mod's name.
    private static void Update()
    {
        var wanted = Wanted();
        if (wanted.palette != Shown || wanted.pattern != shownPatternName)
        {
            Shown = wanted.palette;
            shownPatternName = wanted.pattern;
            Apply(wanted.palette == null ? null : DefDatabase<ThemePaletteDef>.GetNamedSilentFail(wanted.palette),
                wanted.pattern == null ? null : DefDatabase<PatternDef>.GetNamedSilentFail(wanted.pattern));
        }
        ThemeCopies.Sync();
        StatusColours.Refresh();
    }

    // Not inlined: Core names the overriding mod from the calling assembly, which must be this one, not Harmony's patched Update.
    // Two nulls clear the override, so the player's own pick shows.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Apply(ThemePaletteDef palette, PatternDef pattern)
    {
        if (palette == null && pattern == null) Theme.ClearOverride();
        else Theme.Override(palette, pattern);
    }
}
