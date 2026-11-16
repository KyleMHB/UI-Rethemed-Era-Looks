using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace EraLooks;

// Shows each faction's own tech level as its pattern in UI Rethemed - Factions (docs/adr/0005). Factions' public pattern
// resolver covers everything Factions draws in a faction's pattern: the icon boxes, the selected faction's header card and the
// relations list. Factions draws no pattern behind its list rows, so one more patch on its private FactionRail.Row adds one; if
// a Factions update changes Row, only that row pattern goes and the mod writes one warning to the log.
internal static class FactionEras
{
    private const string ApiType = "UIRethemed.Factions.FactionsApi";
    private const string RailType = "UIRethemed.Factions.FactionRail";
    private const string InfoType = "UIRethemed.Factions.FactionInfo";
    private const string CentreType = "UIRethemed.Factions.FactionCentre";
    private const string DetailsType = "UIRethemed.Factions.DetailsPanel";

    // How strongly the faction's colour tints the row pattern, the same as Factions' own header card.
    private const float RowTint = 0.12f;

    private static AccessTools.FieldRef<object, Faction> factionOf;

    // The faction pattern for the next LitFace inside a list row; null outside a row.
    private static PatternDef rowPattern;

    internal static void Patch(Harmony harmony)
    {
        if (AccessTools.TypeByName(RailType) == null) return; // Factions isn't loaded
        if (AccessTools.TypeByName(ApiType) == null)
        {
            // not translated
            Log.Warning("[EraLooks] This version of UI Rethemed - Factions is too old for faction patterns. Update it to show each faction's own era.");
            return;
        }
        Register();
        PatchRows(harmony);
        PatchScopes(harmony);
    }

    // Kept out of Patch so the game resolves Factions' types only after Patch has found them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register() => FactionResolver.Register();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PatternDef ResolvedPattern(Faction faction) => FactionResolver.PatternFor(faction);

    // Factions opens the faction's pattern scope only around its icon boxes, header card and list rows. The selected
    // faction's graph pages and leader card are drawn outside it, so they would show the colony's pattern.
    private static void PatchScopes(Harmony harmony)
    {
        var centre = AccessTools.TypeByName(CentreType);
        var details = AccessTools.TypeByName(DetailsType);
        var targets = new[]
        {
            centre == null ? null : AccessTools.Method(centre, "Settlements"),
            centre == null ? null : AccessTools.Method(centre, "Overview"),
            details == null ? null : AccessTools.Method(details, "Leader"),
        };
        var prefix = new HarmonyMethod(typeof(FactionEras), nameof(ScopePrefix));
        var finalizer = new HarmonyMethod(typeof(FactionEras), nameof(ScopeFinalizer));
        var missed = false;
        foreach (var target in targets)
        {
            if (factionOf == null || target == null || Array.FindIndex(target.GetParameters(), p => p.Name == "info") < 0)
            {
                missed = true;
                continue;
            }
            harmony.Patch(target, prefix: prefix, finalizer: finalizer);
        }
        if (missed)
            // not translated
            Log.Warning("[EraLooks] This version of UI Rethemed - Factions draws its faction pages differently, so some of them show the colony's pattern instead of the faction's.");
    }

    private static void ScopePrefix(object info, out PatternScopeToken __state) =>
        __state = Theme.PatternScope(info == null ? null : ResolvedPattern(factionOf(info)));

    private static Exception ScopeFinalizer(Exception __exception, PatternScopeToken __state)
    {
        __state.Dispose();
        return __exception;
    }

    private static void PatchRows(Harmony harmony)
    {
        var row = AccessTools.Method(AccessTools.TypeByName(RailType), "Row");
        var info = AccessTools.TypeByName(InfoType);
        var faction = info == null ? null : AccessTools.Field(info, "faction");
        var litFace = AccessTools.Method(typeof(CoreWidgets), nameof(CoreWidgets.LitFace));
        if (row == null || litFace == null || faction == null || faction.FieldType != typeof(Faction))
        {
            // not translated
            Log.Warning("[EraLooks] This version of UI Rethemed - Factions draws its faction list differently, so the list rows show no faction pattern behind them.");
            return;
        }
        factionOf = AccessTools.FieldRefAccess<object, Faction>(faction);
        harmony.Patch(row, prefix: new HarmonyMethod(typeof(FactionEras), nameof(RowPrefix)), finalizer: new HarmonyMethod(typeof(FactionEras), nameof(RowFinalizer)));
        harmony.Patch(litFace, postfix: new HarmonyMethod(typeof(FactionEras), nameof(LitFacePostfix)));
    }

    // The pattern set for the faction's tech level, or null to leave it to Factions: with the setting off, or when the
    // player keeps their own pick for that level.
    internal static PatternDef PatternOf(Faction faction)
    {
        if (EraLooksMod.Settings is not { factionEras: true }) return null;
        var def = faction?.def;
        if (def == null) return null;
        var name = CoreBridge.LookFor(def.techLevel).pattern;
        return name == null ? null : DefDatabase<PatternDef>.GetNamedSilentFail(name);
    }

    // The row's pattern goes on right after the row's face, under its icon and labels.
    private static void RowPrefix(object info) => rowPattern = PatternOf(factionOf(info));

    private static void LitFacePostfix(Rect rect, Color accent)
    {
        if (rowPattern == null) return;
        var pattern = rowPattern;
        rowPattern = null;
        using (Theme.PatternScope(pattern))
            Theme.Pattern(rect.ContractedBy(3f), Faces.Fade(accent, RowTint), false);
    }

    private static Exception RowFinalizer(Exception __exception)
    {
        rowPattern = null;
        return __exception;
    }
}
