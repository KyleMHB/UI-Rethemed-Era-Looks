using System;
using HarmonyLib;
using RimWorld;
using UIRethemed.Core;
using UnityEngine;
using Verse;

namespace EraLooks;

public class EraLooksMod : Mod
{
    public static EraLooksSettings Settings;

    // The palette shown in place of the player's own pick, or null while the own palette shows.
    public static string ShownLook => CoreBridge.Shown;

    public EraLooksMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<EraLooksSettings>();
        CoreBridge.Patch(new Harmony("kylemhb.uirethemederalooks"));
    }

    public override string SettingsCategory() => "EraLooksMod_SettingsCategory".Translate();

    public override void DoSettingsWindowContents(Rect inRect) => SettingsPage.Draw(inRect);
}

// The era groups that each get a look, one per tech level. The names are the settings keys, so
// renaming one loses the player's saved picks for it.
public enum EraGroup
{
    Animal,
    Neolithic,
    Medieval,
    Industrial,
    Spacer,
    Ultra,
    Archotech,
}

public class EraLooksSettings : ModSettings
{
    // Stands for the player's own palette or pattern from UI Rethemed - Core.
    public const string OwnPick = "OwnPick";

    // Faction era patterns may confuse players, as a faction's look then differs from the colony's. On while it's being
    // tested; off for the public release.
    public const bool FactionErasDefault = false;

    public bool followEra = true;
    // Show each faction's own era pattern in UI Rethemed - Factions.
    public bool factionEras = FactionErasDefault;

    // Def names per era group, or null for the era look made for it.
    private static readonly int GroupCount = Enum.GetValues(typeof(EraGroup)).Length;

    private string[] palettes = new string[GroupCount];
    private string[] patterns = new string[GroupCount];

    public static EraGroup? GroupOf(TechLevel era) => era switch
    {
        TechLevel.Animal => EraGroup.Animal,
        TechLevel.Neolithic => EraGroup.Neolithic,
        TechLevel.Medieval => EraGroup.Medieval,
        TechLevel.Industrial => EraGroup.Industrial,
        TechLevel.Spacer => EraGroup.Spacer,
        TechLevel.Ultra => EraGroup.Ultra,
        TechLevel.Archotech => EraGroup.Archotech,
        _ => null,
    };

    public static string DefaultPalette(EraGroup group) => group switch
    {
        EraGroup.Animal => "EraLooks_Bone",
        EraGroup.Neolithic => "EraLooks_RockArt",
        EraGroup.Medieval => "EraLooks_Tapestry",
        EraGroup.Industrial => "EraLooks_Engraving",
        EraGroup.Spacer => "EraLooks_Spacer",
        EraGroup.Ultra => "EraLooks_Ultratech",
        EraGroup.Archotech => "EraLooks_Archotech",
        _ => ThemePaletteDef.DefaultName,
    };

    public static string DefaultPattern(EraGroup group) => group switch
    {
        EraGroup.Animal => "EraLooks_Scree",
        EraGroup.Neolithic => "EraLooks_SplitWood",
        EraGroup.Medieval => "EraLooks_CutStone",
        EraGroup.Industrial => "EraLooks_TreadPlate",
        EraGroup.Spacer => "EraLooks_HullPanels",
        EraGroup.Ultra => "EraLooks_Honeycomb",
        EraGroup.Archotech => "EraLooks_EtchedCircuits",
        _ => PatternDef.DefaultName,
    };

    public string PaletteFor(EraGroup group) => palettes[(int)group];
    public string PatternFor(EraGroup group) => patterns[(int)group];
    public void SetPalette(EraGroup group, string defName) => palettes[(int)group] = defName;
    public void SetPattern(EraGroup group, string defName) => patterns[(int)group] = defName;

    // Whether any era has a palette or pattern other than the one made for it.
    public bool AnyChosen => Array.Exists(palettes, p => p != null) || Array.Exists(patterns, p => p != null);

    public void ResetLooks()
    {
        palettes = new string[GroupCount];
        patterns = new string[GroupCount];
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref followEra, "followEra", true);
        Scribe_Values.Look(ref factionEras, "factionEras", FactionErasDefault);
        foreach (EraGroup group in Enum.GetValues(typeof(EraGroup)))
        {
            Scribe_Values.Look(ref palettes[(int)group], group.ToString().ToLowerInvariant() + "Palette");
            Scribe_Values.Look(ref patterns[(int)group], group.ToString().ToLowerInvariant() + "Pattern");
        }
    }
}
