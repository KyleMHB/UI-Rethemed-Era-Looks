using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorldDevTests;
using Verse;

namespace EraLooks.DevTests;

// Animals for the Wildlife and Animals tabs, which are empty on the quickstart map. The wild ones span the threat levels
// Wildlife colours its rows by (passive through dangerous, and a predator). The tame ones are one hungry and hurt and one
// well, so the Animals tab shows its level colours at both ends. A kind the loaded content lacks is skipped.
internal static class TestAnimals
{
    private static readonly string[] Wild = { "Deer", "Ibex", "Boomalope", "Muffalo", "Cougar", "Thrumbo" };
    private static readonly string[] Tame = { "Muffalo", "Husky" };

    internal static void Spawn(Map map, IntVec3 near, BatchContext ctx)
    {
        var spawned = new List<string>();
        foreach (var name in Wild)
        {
            var pawn = SpawnKind(name, map, near, null);
            if (pawn != null) spawned.Add("wild " + name);
        }
        for (int i = 0; i < Tame.Length; i++)
        {
            var pawn = SpawnKind(Tame[i], map, near, Faction.OfPlayer);
            if (pawn == null) continue;
            if (i == 0)
            {
                if (pawn.needs?.food != null) pawn.needs.food.CurLevelPercentage = 0.15f;
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, 6f));
            }
            spawned.Add("tame " + Tame[i]);
        }
        ctx.Setup("Animals: " + (spawned.Count == 0 ? "none of the kinds are loaded" : string.Join(", ", spawned)));
    }

    private static Pawn SpawnKind(string name, Map map, IntVec3 near, Faction faction)
    {
        var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(name);
        if (kind == null) return null;
        var pawn = PawnGenerator.GeneratePawn(kind, faction);
        var cell = CellFinder.RandomClosewalkCellNear(near, map, 10);
        GenSpawn.Spawn(pawn, cell, map);
        return pawn;
    }
}
