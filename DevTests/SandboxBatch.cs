using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorldDevTests;
using Verse;

namespace EraLooks.DevTests;

// A colony to look at by hand, not a check: run it with --keep-open and the game is left paused on the quickstart map.
// It adds one visible faction for every tech level from animal to archotech, so the faction window shows each era.
// Vanilla and the DLCs have no visible animal, medieval or archotech faction, so for those (and any other era the loaded
// mods lack) it registers a copy of a tribe or outlander faction def with that tech level. The copies exist only in this
// process: a save made here needs the developer DLL to load again.
public class SandboxBatch : Batch
{
    public override string Id => "eralooks-sandbox";
    public override int SegmentCount => 1;
    public override IReadOnlyList<PlannedCase> Plan { get; } = new[]
    {
        new PlannedCase("eralooks.sandbox-factions", Levels.Mechanical),
    };

    private static readonly TechLevel[] Eras =
    {
        TechLevel.Animal, TechLevel.Neolithic, TechLevel.Medieval, TechLevel.Industrial,
        TechLevel.Spacer, TechLevel.Ultra, TechLevel.Archotech,
    };

    public override IEnumerable<Wait> Segment(int index, BatchContext ctx)
    {
        var scope = ctx.Begin("eralooks.sandbox-factions");
        var missing = new List<string>();
        foreach (var era in Eras)
        {
            var present = Find.FactionManager.AllFactionsVisible.FirstOrDefault(f => !f.IsPlayer && f.def.techLevel == era);
            if (present != null)
            {
                ctx.Setup($"{era}: already has {present.Name} ({present.def.defName})");
                continue;
            }
            var def = RealDef(era) ?? CopyDef(era);
            if (def == null)
            {
                missing.Add(era.ToString());
                continue;
            }
            var faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
            Find.FactionManager.Add(faction);
            ctx.Setup($"{era}: added {faction.Name} ({def.defName})");
        }
        yield return Wait.Frames(5);
        scope.Check(missing.Count == 0, "no faction def to copy for " + string.Join(", ", missing));
    }

    private static bool Visible(FactionDef d) => !d.isPlayer && !d.hidden && d.humanlikeFaction;

    private static FactionDef RealDef(TechLevel era) =>
        DefDatabase<FactionDef>.AllDefs.Where(d => Visible(d) && d.techLevel == era && !d.permanentEnemy).FirstOrDefault()
        ?? DefDatabase<FactionDef>.AllDefs.FirstOrDefault(d => Visible(d) && d.techLevel == era);

    private static readonly MethodInfo Clone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo CachedDescription = typeof(FactionDef).GetField("cachedDescription", BindingFlags.Instance | BindingFlags.NonPublic);

    private static FactionDef CopyDef(TechLevel era)
    {
        string defName = "EraLooksSandbox_" + era;
        var existing = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
        if (existing != null) return existing;
        // Tribes for the eras before industry, outlanders after it.
        var source = DefDatabase<FactionDef>.GetNamedSilentFail(era <= TechLevel.Medieval ? "TribeCivil" : "OutlanderCivil");
        if (source == null) return null;
        var copy = (FactionDef)Clone.Invoke(source, null);
        copy.defName = defName;
        copy.label = era.ToStringHuman() + " faction (sandbox)";
        copy.techLevel = era;
        CachedDescription?.SetValue(copy, null);
        DefDatabase<FactionDef>.Add(copy);
        return copy;
    }
}
