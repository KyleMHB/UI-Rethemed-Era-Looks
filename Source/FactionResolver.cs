using RimWorld;
using UIRethemed.Core;
using UIRethemed.Factions;

namespace EraLooks;

// The only code that names UI Rethemed - Factions' types. FactionEras calls Register only after it finds FactionsApi
// loaded, so the game never resolves these types when Factions isn't installed.
internal static class FactionResolver
{
    // Below Factions' own resolver (priority 0), so a faction def that names its own pattern keeps it.
    private const int Priority = -100;

    internal static void Register() => FactionsApi.AddPatternResolver(FactionEras.PatternOf, Priority);

    // Every resolver's answer, so the graph and leader card match the faction's icon and header card.
    internal static PatternDef PatternFor(Faction faction) => FactionsApi.PatternFor(faction);
}
