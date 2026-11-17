using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace EraLooks;

// UI Rethemed uses its green both as an accent and to tell the player a state: a need's level, good or bad, done or not.
// The era palettes put their own colour in green for the accent, which would hide those states (a gold "full" beside a
// yellow "half"). So each status read of green, wherever the suite picks it against red or yellow, is sent here, and shows
// Core's standard green while an era palette's green is in effect. Any other green, such as a player's own, passes through.
internal static class StatusColours
{
    private static readonly string[] Greens = { "HighlightGreen", "TextGreen", "BGGreen" };
    private static readonly string[] Contrasts = { "HighlightRed", "HighlightYellow", "TextRed", "TextYellow", "BGRed", "BGYellow" };

    private static Dictionary<string, MethodInfo> redirects;
    private static HashSet<Color> eraGreens = new();
    private static Color standardGreen, standardText;
    private static bool eraGreen;

    public static Color HighlightGreen => eraGreen ? standardGreen : Theme.HighlightGreen;
    public static Color TextGreen => eraGreen ? standardText : Theme.TextGreen;
    public static Color BGGreen => eraGreen ? Color.Lerp(Theme.BG, standardGreen, 0.25f) : Theme.BGGreen;

    // From the mod constructor. Scans every UI Rethemed assembly for status reads of green and patches the methods that hold one.
    internal static void Patch(Harmony harmony)
    {
        redirects = Greens.ToDictionary(name => name, name => AccessTools.PropertyGetter(typeof(StatusColours), name));
        var transpiler = new HarmonyMethod(typeof(StatusColours), nameof(Redirect)) { priority = Priority.High };
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("UIRethemed")))
            foreach (var method in ThemeCopies.MethodsOf(assembly))
                if (StatusReads(ThemeCopies.Body(method)).Count > 0) harmony.Patch(method, transpiler: transpiler);
    }

    // From StatusColoursStartup, once defs have loaded: the greens of this mod's palettes, and Core's standard green.
    internal static void FindColours()
    {
        var defaults = AccessTools.Field(AccessTools.TypeByName("UIRethemed.Core.ThemeLook"), "Defaults")?.GetValue(null);
        if (defaults == null)
        {
            // not translated
            Log.Warning("[EraLooks] UI Rethemed - Core changed how it keeps its standard colours, so status colours may show the era's green.");
            return;
        }
        standardGreen = Traverse.Create(defaults).Field<Color>("HighlightGreen").Value;
        standardText = Traverse.Create(defaults).Field<Color>("TextGreen").Value;
        var mine = LoadedModManager.GetMod<EraLooksMod>().Content;
        eraGreens = DefDatabase<ThemePaletteDef>.AllDefs
            .Where(def => def.modContentPack == mine && !def.green.NullOrEmpty())
            .Select(def => def.Accent).ToHashSet();
    }

    // Once a frame, from CoreBridge.
    internal static void Refresh() => eraGreen = eraGreens.Contains(Theme.HighlightGreen);

    // A green read is a status read when a red or yellow read sits in the same expression: a choice between them, a blend, or
    // one of the returns of a function that picks a colour by level. Only colour maths and comparisons may come between.
    private static List<int> StatusReads(IReadOnlyList<(OpCode opcode, object operand)> body)
    {
        var found = new List<int>();
        for (int i = 0; i < body.Count; i++)
        {
            if (!Greens.Contains(ColourRead(body[i]))) continue;
            // A method that compares a colour with green maps an accent to its text colour; its greens follow that accent.
            if (Compared(body, i)) return new List<int>();
            if (Near(body, i, -1) || Near(body, i, 1)) found.Add(i);
        }
        return found;
    }

    private const int Reach = 16;

    private static bool Near(IReadOnlyList<(OpCode opcode, object operand)> body, int from, int step)
    {
        for (int i = from + step, n = 0; i >= 0 && i < body.Count && n < Reach; i += step, n++)
        {
            if (Contrasts.Contains(ColourRead(body[i]))) return true;
            if (Breaks(body[i])) return false;
        }
        return false;
    }

    private static bool Compared(IReadOnlyList<(OpCode opcode, object operand)> body, int from)
    {
        for (int i = from + 1; i < body.Count && i <= from + 2; i++)
            if (body[i].operand is MethodInfo { Name: "op_Equality" or "op_Inequality" or "Equals" }) return true;
        return false;
    }

    // A store, or a call that does something other than read or work out a colour, ends the expression.
    private static bool Breaks((OpCode opcode, object operand) code) =>
        code.opcode == OpCodes.Stfld || code.opcode == OpCodes.Stsfld || code.opcode.Name.StartsWith("stelem")
        || (code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt || code.opcode == OpCodes.Newobj)
        && code.operand is MethodBase method && !method.Name.StartsWith("get_")
        && !(method is MethodInfo info && info.ReturnType == typeof(Color));

    // The name of the theme colour a code reads (a Theme getter, or a field of a theme that keeps its colours as fields), or null.
    private static string ColourRead((OpCode opcode, object operand) code)
    {
        if (code.opcode == OpCodes.Ldsfld && code.operand is FieldInfo field && IsTheme(field.DeclaringType)) return field.Name;
        if (code.opcode == OpCodes.Call && code.operand is MethodInfo getter && getter.Name.StartsWith("get_") && IsTheme(getter.DeclaringType))
            return getter.Name.Substring(4);
        return null;
    }

    private static bool IsTheme(Type type) => type?.Name == "Theme" && type.Namespace != null && type.Namespace.StartsWith("UIRethemed");

    private static IEnumerable<CodeInstruction> Redirect(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        foreach (int i in StatusReads(codes.Select(c => (c.opcode, c.operand)).ToList()))
        {
            codes[i].operand = redirects[ColourRead((codes[i].opcode, codes[i].operand))];
            codes[i].opcode = OpCodes.Call;
        }
        return codes;
    }
}

// Its own class, so that StatusColours.Patch in the mod constructor doesn't run this early.
[StaticConstructorOnStartup]
internal static class StatusColoursStartup
{
    static StatusColoursStartup() => StatusColours.FindColours();
}
