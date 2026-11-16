using System;
using System.Collections.Generic;
using RimWorldDevTests;

namespace EraLooks.DevTests;

public sealed class ModHarness : HarnessSettings
{
    public override string DisplayName => "Era Looks";
    public override string ArgPrefix => "eralooks";
    public override string PackageId => "kylemhb.uirethemederalooks";
    public override Type ProductionType => typeof(EraLooksMod);
    public override string ErrorMarker => "EraLooks";
    public override string EvidenceFolder => "EraLooksTests";
    public override string ColonyName => "Era Looks Test Colony";
    public override string[] RuntimeFolders => new[] { "About", "1.7", "Defs", "Languages", "Textures" };
    public override string[] RuntimeFiles => new[] { "LoadFolders.xml" };
    public override IEnumerable<Batch> CreateBatches() => new Batch[] { new LooksBatch(), new SwitchingBatch(), new SandboxBatch() };
    public override IEnumerable<string> RunAll => new[] { "eralooks-screens", "eralooks-switching" };
}
