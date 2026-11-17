# UI Rethemed - Era Looks Testing

Updated through: 0.3.0 plus unreleased changes on 2026-11-17

## Agent read route

- Headings: `rg -n '^## ' TESTING.md`.
- Every code change: [Quick tier](#quick-tier).
- A palette, pattern or texture change: [Visual tier](#visual-tier), for the affected era only.
- A release, or an update to LFS Core, an LFS module or RimWorld: [Full tier](#full-tier).
- Writing or triaging cases: the shared harness guide, `python tools/eralooks_test.py guide`.

## Test prerequisites

- The shared harness checked out beside this repository (`../RimWorldDevTests`), or `RIMWORLD_DEV_TESTS_ROOT` set to it.
- RimWorld 1.7 with Ideology and Biotech, and the LFS modules listed in `DevTests/devtests.json` installed from the Workshop.
- The installed mod must match the working tree, or the launcher refuses the run. Build, then deploy:

  ```bash
  dotnet build Source/EraLooks.csproj -c Release
  ```

  ```bash
  python -I tools/deploy.py
  ```

- Runs use their own save-data folder (`work/userdata/eralooks` in the harness), so the player's settings, mod list and saves are never touched.
- Any error that names `EraLooks` fails the run, even when every case passed (`FailOnModErrors`). An exception in the once-a-frame update is logged with that marker by `DevTests/TestState.cs` (`UpdateErrors`), because the engine reports it outside `Log.Error`.
- Close the runner session before rebuilding the DLL: `python tools/eralooks_test.py close-session`.

## Quick tier

For every code change. One launch, no screenshots, a few minutes including startup.

```bash
python tools/eralooks_test.py run run-all
```

Runs `eralooks-checks` and `eralooks-switching` after the harness's own diagnostic.

| Case | Expected |
| --- | --- |
| `eralooks.defs-load` | Every era palette and pattern loads, with its texture. |
| `eralooks.patches-applied` | Every patch the loaded mods allow is in place (the frame update, pattern scroll, status colours, each theme copy's pattern and colours, plain animal draws, Factions rows and pages). No `[EraLooks]` warning logged at startup. This is the case that catches an LFS update quietly turning a patch off. |
| `eralooks.status-green` | Under an era palette with its own green, status reads show Core's standard green while the accent keeps the era's. Under the player's own pick they pass through. |
| `eralooks.theme-copies-sync` | Each module with its own palette object shows Core's colours in the medieval look. |
| `eralooks.faction-pattern` | With Show each faction's era on, a faction gets its era's pattern; off, or with the own pick for that era, Factions decides. |
| `eralooks.reload-keeps-look` | After a save and reload the colony's era look shows, and no Era Looks error was logged while loading. |
| `eralooks.follows-era` | Each tech level shows its era pattern and green. |
| `eralooks.own-pick-kept` | The player's saved Core pick is never written. |
| `eralooks.per-era-choice` | A per-era palette or pattern replaces that half of the era look, and the own pick for either half shows the player's own. |
| `eralooks.follow-off` | With Follow the era off, the own pick shows in every era. |
| `eralooks.era-hatch-lines` | Every era pattern hatches with Core's fine lines. |

## Visual tier

For art changes. The developer judges the screenshots. The only case checks that the files were written.

One era beside the default look (about a minute of shots):

```bash
python tools/eralooks_test.py run eralooks-screens-medieval
```

The looks are `animal`, `neolithic`, `medieval`, `industrial`, `spacer`, `ultra` and `archotech`. Each shot is the main bar's menu and every LFS tab, the colonist's inspector tabs and Core's settings page.

Every look, plus this mod's settings page and the faction window and relations graph for one faction per era (about four minutes):

```bash
python tools/eralooks_test.py run eralooks-screens
```

Screenshots are saved in the run's evidence folder, which `latest` prints.

## Full tier

Before a release, and after an update to LFS Core, an LFS module or RimWorld: the quick tier, the full screenshots and the performance tests.

```bash
python tools/eralooks_test.py run run-all eralooks-screens --timeout 2400
```

Then the [performance test](#performance-test).

## Performance test

Release requires the harness's own `perf` batch, which measures ms per tick with Dubs Performance Analyzer, and the mod's frame-path cases.

```bash
python tools/eralooks_test.py run perf eralooks-perf --mods perf
```

| Case | Scale | Budget |
| --- | --- | --- |
| `perf.frame-update` | `CoreBridge.Update` at 60 frames (one second) | under 1 ms |
| `perf.pattern-draw` | The theme-copy pattern prefix at 50 draws a frame (a busy Architect window), at the title banner's size | under 0.5 ms added |

Release check: `python tools/eralooks_test.py release-check`.

## Sandbox

Not a check: a colony to look at by hand, with one faction per era and wild and tame animals.

```bash
python tools/eralooks_test.py run eralooks-sandbox --keep-open
```

## Result recording

The harness writes `summary.txt` and `report.json` per run. Read the latest with `python tools/eralooks_test.py latest`. Record a result here only for a release or a notable finding.

## 2026-11-17 - Quick tier after the tiered split

- Version/environment: 0.3.0 plus the `CoreBridge` load fix (`71ffb6a`); RimWorld 1.7 with Ideology and Biotech; default mod set.
- Actions: `run run-all`.
- Expected: all quick-tier cases pass, with no Era Looks errors.
- Observed: run `20261117-140733-run-all-s1-7d371044`. All 11 cases passed, with 1 logged error (the harness's deliberate diagnostic) and none naming Era Looks. No "Update threw" in `Player.log`, so the `CoreBridge` load fix holds, and the gene-icon errors are gone now that Biotech is loaded. All patches were in place: 6 theme-copy patterns, 128 status-colour transpilers, 298 Inspector Tabs reads, 11 animal draws, and the Factions rows and pages. 118 copied colours matched Core.
- Status: Passed.
- Follow-up: `UpdateErrors` has not yet seen an exception, because the fix removed the only known one. Its first real catch will confirm it.
