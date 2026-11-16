using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace EraLooks;

// Several UI Rethemed modules still carry their own copy of the theme: a fixed palette and their own honeycomb, so their
// windows ignore the era look. This sends each copy's pattern draws to Core and copies Core's colours into each copy's
// palette, so they show the look in effect now. Drop a module from Modules once it draws through Core.
internal static class ThemeCopies
{
    private static readonly string[] Modules = { "Architect", "Animals", "ColonistCard", "History", "Notifications", "Quests", "Wildlife" };

    private static readonly List<(string module, Type theme)> loaded = new();

    // One palette object per loaded copy, with each field paired to the Core colour of the same name. Empty until startup
    // has finished, and then replaced whole, as CoreBridge's Update can run while mods are still loading.
    private static (object palette, AccessTools.FieldRef<object, Color> field, Func<Color> core)[] colours = Array.Empty<(object, AccessTools.FieldRef<object, Color>, Func<Color>)>();

    // From the mod constructor. Only looks the types up: reading a copy's palette runs its static constructor, which loads
    // its textures and would get nulls this early.
    internal static void Patch(Harmony harmony)
    {
        var prefix = new HarmonyMethod(typeof(ThemeCopies), nameof(Pattern));
        foreach (var module in Modules)
        {
            var theme = AccessTools.TypeByName($"UIRethemed.{module}.Theme");
            if (theme == null) continue; // the module isn't loaded
            loaded.Add((module, theme));
            var pattern = AccessTools.Method(theme, "Pattern", new[] { typeof(Rect), typeof(Vector2) });
            if (pattern != null) harmony.Patch(pattern, prefix: prefix);
            else Changed(module);
        }
    }

    // From ThemeCopiesStartup, once textures and defs have loaded. The colour getters are too small to patch reliably, as
    // the JIT may inline them, so their backing palette objects are written instead.
    internal static void FindColours()
    {
        var found = new List<(object, AccessTools.FieldRef<object, Color>, Func<Color>)>();
        foreach (var (module, theme) in loaded)
        {
            var palette = AccessTools.Field(theme, "Colors")?.GetValue(null);
            if (palette == null)
            {
                Changed(module);
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
    private static void Changed(string module) =>
        Log.Warning($"[EraLooks] UI Rethemed - {module} changed how it draws its theme, so it may keep its own colours or honeycomb.");

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
