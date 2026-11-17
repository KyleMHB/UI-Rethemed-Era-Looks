using System;
using System.Collections.Generic;
using System.Linq;
using RimWorldDevTests;

namespace EraLooks.DevTests;

public sealed class ModHarness : HarnessSettings
{
    public override string DisplayName => "Era Looks";
    public override string ArgPrefix => "eralooks";
    public override string PackageId => "kylemhb.uirethemederalooks";
    public override Type ProductionType => typeof(EraLooksMod);
    public override string ErrorMarker => "EraLooks";
    // An Era Looks error fails the run even when every case passed: most of the mod is patches on other mods, and a broken
    // one shows up as a logged error long before a case notices.
    public override bool FailOnModErrors => true;
    public override string EvidenceFolder => "EraLooksTests";
    public override string ColonyName => "Era Looks Test Colony";
    public override string[] RuntimeFolders => new[] { "About", "1.7", "Defs", "Languages", "Textures" };
    public override string[] RuntimeFiles => new[] { "LoadFolders.xml" };

    // The tiers are in TESTING.md: run-all is the quick tier; the screens batches are the visual tier.
    public override IEnumerable<Batch> CreateBatches() =>
        new Batch[] { new ChecksBatch(), new SwitchingBatch(), new LooksBatch(), new NotificationsBatch(), new SandboxBatch(), new PerfBatch() }
            .Concat(LooksBatch.Looks.Where(l => l.name != "default").Select(l => (Batch)new LooksBatch(l.name)))
            .Concat(LooksBatch.Looks.Where(l => l.name != "default").Select(l => (Batch)new NotificationsBatch(l.name)));

    public override IEnumerable<string> RunAll => new[] { "eralooks-checks", "eralooks-switching" };

    public override void OnBatchEnd()
    {
        TestState.Restore();
        UpdateErrors.Reset();
    }
}
