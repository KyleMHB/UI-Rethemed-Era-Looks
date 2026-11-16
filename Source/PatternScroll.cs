using HarmonyLib;
using UnityEngine;
using Verse;

namespace EraLooks;

// Core scrolls animated patterns by rounding each frame's offset to a whole UI pixel. With a UI scale such as 1.5 a UI
// pixel is 1.5 screen pixels, so the pattern jumps 1 then 2 screen pixels in turn and the scroll looks like it hitches;
// the long straight edges of the era patterns show it far more than the honeycomb. This draws the same tiles, but
// places each on a whole screen pixel, so every step is one screen pixel and the tiles stay sharp. Remove it once
// Core's own Tiled snaps to screen pixels.
internal static class PatternScroll
{
    internal static void Patch(Harmony harmony)
    {
        var tiled = AccessTools.Method(typeof(UIRethemed.Core.Theme), "Tiled", new[] { typeof(Rect), typeof(Vector2), typeof(Texture2D), typeof(Vector2) });
        if (tiled == null)
        {
            // not translated
            Log.Warning("[EraLooks] UI Rethemed - Core changed how it tiles patterns, so the pattern scroll keeps Core's own steps.");
            return;
        }
        harmony.Patch(tiled, prefix: new HarmonyMethod(typeof(PatternScroll), nameof(Tiled)));
    }

    // Core's Tiled with the first tile's position snapped in screen pixels instead of UI pixels.
    private static bool Tiled(Rect rect, Vector2 offset, Texture2D tile, Vector2 size)
    {
        float scale = Prefs.UIScale;
        float startX = Snap(rect.x, Mathf.Repeat(rect.width / 2f + offset.x, size.x), scale) - size.x;
        float startY = Snap(rect.y, Mathf.Repeat(rect.height / 2f + offset.y, size.y), scale) - size.y;
        Widgets.BeginGroup(rect);
        for (float x = startX; x < rect.width; x += size.x)
        {
            for (float y = startY; y < rect.height; y += size.y)
            {
                GUI.DrawTexture(new Rect(x, y, size.x, size.y), tile);
            }
        }
        Widgets.EndGroup();
        return false;
    }

    // A position inside the group, moved so that it lands on a whole screen pixel once the group's own origin is added.
    private static float Snap(float origin, float local, float scale) => Mathf.Round((origin + local) * scale) / scale - origin;
}
