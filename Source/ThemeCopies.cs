using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace EraLooks;

// Several UI Rethemed modules still carry their own copy of the theme: a fixed palette and their own honeycomb, so their
// windows ignore the era look. This sends each copy's pattern draws to Core and copies Core's colours into each copy's
// palette, so they show the look in effect now. Drop a module from Modules once it draws through Core.
internal static class ThemeCopies
{
    // Each module's namespace under UIRethemed, and its name as players see it.
    private static readonly (string module, string name)[] Modules =
    {
        ("Architect", "Architect"), ("Animals", "Animals"), ("ColonistCard", "Colonist Card"), ("History", "History"),
        ("InspectorTabs", "Inspector Tabs"), ("Notifications", "Notifications"), ("Quests", "Quests"), ("Wildlife", "Wildlife"),
    };

    private static readonly List<(string name, Type theme)> loaded = new();

    // The colour fields of copies that keep them as static readonly fields on Theme itself (Inspector Tabs), each with the
    // Core getter of the same name that its reads are sent to.
    private static readonly Dictionary<FieldInfo, MethodInfo> reads = new();

    // One palette object per loaded copy, with each field paired to the Core colour of the same name. Empty until startup
    // has finished, and then replaced whole, as CoreBridge's Update can run while mods are still loading.
    private static (object palette, AccessTools.FieldRef<object, Color> field, Func<Color> core)[] colours = Array.Empty<(object, AccessTools.FieldRef<object, Color>, Func<Color>)>();

    // From the mod constructor. Only looks the types up: reading a copy's palette runs its static constructor, which loads
    // its textures and would get nulls this early.
    internal static void Patch(Harmony harmony)
    {
        var prefix = new HarmonyMethod(typeof(ThemeCopies), nameof(Pattern));
        foreach (var (module, name) in Modules)
        {
            var theme = AccessTools.TypeByName($"UIRethemed.{module}.Theme");
            if (theme == null) continue; // the module isn't loaded
            var pattern = AccessTools.Method(theme, "Pattern", new[] { typeof(Rect), typeof(Vector2) });
            if (pattern != null) harmony.Patch(pattern, prefix: prefix);
            else Changed(name);
            if (AccessTools.Field(theme, "Colors") != null) loaded.Add((name, theme));
            else Redirect(harmony, name, theme);
        }
    }

    // A copy whose colours are static readonly fields can't be written: the JIT folds such fields into the code that reads
    // them once it optimises that code, so a write would stop showing. Instead every method of the module that reads one is
    // patched to call Core's getter of the same name.
    private static void Redirect(Harmony harmony, string name, Type theme)
    {
        foreach (var field in theme.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            if (field.FieldType != typeof(Color)) continue;
            var core = AccessTools.PropertyGetter(typeof(UIRethemed.Core.Theme), field.Name);
            if (core != null && core.IsStatic && core.ReturnType == typeof(Color)) reads[field] = core;
        }
        if (reads.Count == 0)
        {
            Changed(name);
            return;
        }
        var transpiler = new HarmonyMethod(typeof(ThemeCopies), nameof(ReadCore));
        foreach (var type in AccessTools.GetTypesFromAssembly(theme.Assembly))
        {
            var methods = AccessTools.GetDeclaredMethods(type).Cast<MethodBase>()
                .Concat(AccessTools.GetDeclaredConstructors(type).Where(c => !c.IsStatic));
            foreach (var method in methods)
            {
                if (method.IsAbstract || method.ContainsGenericParameters || !ReadsColour(method)) continue;
                harmony.Patch(method, transpiler: transpiler);
            }
        }
    }

    private static bool ReadsColour(MethodBase method)
    {
        try
        {
            return PatchProcessor.ReadMethodBody(method).Any(code => code.Value is FieldInfo field && reads.ContainsKey(field));
        }
        catch (Exception)
        {
            return false; // extern or bodiless, nothing to redirect
        }
    }

    private static IEnumerable<CodeInstruction> ReadCore(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        foreach (var code in instructions)
        {
            if (code.operand is FieldInfo field && reads.TryGetValue(field, out var core))
            {
                if (code.opcode == OpCodes.Ldsfld)
                {
                    code.opcode = OpCodes.Call;
                    code.operand = core;
                }
                else if (code.opcode == OpCodes.Ldsflda)
                {
                    // The field's address was wanted, so the value goes through a local of its own.
                    var local = il.DeclareLocal(typeof(Color));
                    yield return new CodeInstruction(OpCodes.Call, core).MoveLabelsFrom(code).MoveBlocksFrom(code);
                    yield return new CodeInstruction(OpCodes.Stloc, local);
                    yield return new CodeInstruction(OpCodes.Ldloca, local);
                    continue;
                }
            }
            yield return code;
        }
    }

    // From ThemeCopiesStartup, once textures and defs have loaded. The colour getters are too small to patch reliably, as
    // the JIT may inline them, so their backing palette objects are written instead.
    internal static void FindColours()
    {
        var found = new List<(object, AccessTools.FieldRef<object, Color>, Func<Color>)>();
        foreach (var (name, theme) in loaded)
        {
            var palette = AccessTools.Field(theme, "Colors")?.GetValue(null);
            if (palette == null)
            {
                Changed(name);
                continue;
            }
            foreach (var field in AccessTools.GetDeclaredFields(palette.GetType()))
            {
                if (field.FieldType != typeof(Color)) continue;
                var core = AccessTools.PropertyGetter(typeof(UIRethemed.Core.Theme), field.Name);
                if (core == null || !core.IsStatic || core.ReturnType != typeof(Color)) continue;
                found.Add((palette, AccessTools.FieldRefAccess<Color>(palette.GetType(), field.Name),
                    (Func<Color>)Delegate.CreateDelegate(typeof(Func<Color>), core)));
            }
        }
        colours = found.ToArray();
    }

    // not translated
    private static void Changed(string name) =>
        Log.Warning($"[EraLooks] UI Rethemed - {name} changed how it draws its theme, so it may keep its own colours or honeycomb.");

    // Once a frame, from CoreBridge. Core's look can change without a version bump (it sets the era look aside while its
    // own settings page is open), and copying the colours costs less than tracking that.
    internal static void Sync()
    {
        foreach (var (palette, field, core) in colours) field(palette) = core();
    }

    // The other Pattern overload sets GUI.color first and Core keeps it, so the colour and fade carry over.
    private static bool Pattern(Rect rect, Vector2 offset)
    {
        UIRethemed.Core.Theme.Pattern(rect, offset);
        return false;
    }
}

// Its own class, so that ThemeCopies.Patch in the mod constructor doesn't run this early.
[StaticConstructorOnStartup]
internal static class ThemeCopiesStartup
{
    static ThemeCopiesStartup() => ThemeCopies.FindColours();
}
