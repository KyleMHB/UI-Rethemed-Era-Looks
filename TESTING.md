# UI Rethemed - Era Looks Testing

Updated through: 0.3.0 plus unreleased changes on 2026-11-17

## Agent read route

- Headings: `rg -n '^## ' TESTING.md`.
- What to run for a change: [Test tiers](#test-tiers), then the area table under it.
- What each case expects: [Behaviour checks](#behaviour-checks).
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

## Test tiers

| Tier | When | Runs |
| --- | --- | --- |
| Quick | Every code change | `python tools/eralooks_test.py run run-all`: `eralooks-checks` and `eralooks-switching`, no screenshots. 10 s in game after a 25 s launch (run `20261117-140733`). |
| Visual | A palette, pattern or texture change | `python tools/eralooks_test.py run eralooks-screens-<era>` for each era changed, which shoots that era beside the default look. 25 s and 34 shots for medieval (run `20261117-140836`). For a change to how letters, alerts, messages or game conditions look, also `eralooks-notifications-<era>`: 12 s and 6 shots for medieval (run `20261117-152702`). |
| Full | A release, or an update to LFS Core, an LFS module or RimWorld | `python tools/eralooks_test.py run run-all eralooks-screens eralooks-notifications --timeout 2400`, then the [performance test](#performance-test) under the `perf` mod set. |

The eras for `eralooks-screens-<era>` are `animal`, `neolithic`, `medieval`, `industrial`, `spacer`, `ultra` and `archotech`. `eralooks-screens` shoots every look plus this mod's settings page and the faction window and relations graph for one faction per era. It adds its own faction for each era first.

`eralooks-notifications` and `eralooks-notifications-<era>` set up what UI Rethemed - Notifications draws, then shoot it in each look:
- a letter of every standard letter def;
- real incidents (eclipse, solar flare, cold snap, toxic fallout, resource pod, ship chunk), for the game conditions and Calendar;
- a message of every message type;
- a downed colonist and a malnourished one, for alerts.

Each look gets three shots: the map with all of these, one test letter opened, and the Calendar. Hover cards aren't shot, because the runner can't place the mouse. The game checks no alerts in a colony's first 600 ticks, so the batch moves the clock to tick 700 without simulating. Simulating would let the colonists rescue or feed each other first. The run's setup lines list the alerts that were active. Run `20261117-153125` had 6: Colonist needs rescue (critical), Need colonist beds, Starvation, Need doctor, animal starvation and pen needed. The runner's progress panel covers the bottom of the alert list in the map shot.

| Area (source folder or feature) | Covering batches |
| --- | --- |
| `CoreBridge.cs`, Follow the era, per-era choices, `EraLooksMod.cs` settings | `eralooks-switching`; `eralooks-checks` (`reload-keeps-look`, `patches-applied`) |
| `StatusColours.cs` | `eralooks-checks` (`status-green`, `patches-applied`) |
| `ThemeCopies.cs`, plain animal draws | `eralooks-checks` (`theme-copies-sync`, `patches-applied`); `eralooks-screens` for the drawn result |
| `FactionEras.cs`, `FactionResolver.cs` | `eralooks-checks` (`faction-pattern`, `patches-applied`); `eralooks-screens` for the faction shots |
| `PatternScroll.cs` | `eralooks-checks` (`patches-applied`) only; the snapped scroll is judged by eye in game |
| `SettingsPage.cs` | `eralooks-screens` (settings page shot) only |
| `Defs/`, `Textures/` | `eralooks-checks` (`defs-load`); `eralooks-screens-<era>` |
| UI Rethemed - Notifications (drawn through Core, no Era Looks code) | `eralooks-notifications-<era>`; the Calendar tab also in `eralooks-screens` |
| Frame and pattern-draw cost | `eralooks-perf`, in the Full tier |

## Behaviour checks

| Case | Batch | Expected |
| --- | --- | --- |
| `eralooks.defs-load` | checks | Every era palette and pattern loads, with its texture. |
| `eralooks.patches-applied` | checks | Every patch the loaded mods allow is in place (the frame update, pattern scroll, status colours, each theme copy's pattern and colours, plain animal draws, Factions rows and pages). No `[EraLooks]` warning logged at startup. This is the case that catches an LFS update quietly turning a patch off. |
| `eralooks.status-green` | checks | Under an era palette with its own green, status reads show Core's standard green while the accent keeps the era's. Under the player's own pick they pass through. |
| `eralooks.theme-copies-sync` | checks | Each module with its own palette object shows Core's colours in the medieval look. |
| `eralooks.faction-pattern` | checks | With Show each faction's era on, a faction gets its era's pattern; off, or with the own pick for that era, Factions decides. |
| `eralooks.reload-keeps-look` | checks | After a save and reload the colony's era look shows, and no Era Looks error was logged while loading. |
| `eralooks.follows-era` | switching | Each tech level shows its era pattern and green. |
| `eralooks.own-pick-kept` | switching | The player's saved Core pick is never written. |
| `eralooks.per-era-choice` | switching | A per-era palette or pattern replaces that half of the era look, and the own pick for either half shows the player's own. |
| `eralooks.follow-off` | switching | With Follow the era off, the own pick shows in every era. |
| `eralooks.era-hatch-lines` | switching | Every era pattern hatches with Core's fine lines. |

## Screenshot evidence

The screenshot batches check no behaviour. Their only cases, `eralooks.screenshots-saved` and `eralooks.notifications-shots-saved`, confirm the files were written, so count it as evidence, not as a pass. The developer judges what the shots show. They are saved in the run's evidence folder, which `python tools/eralooks_test.py latest` prints.

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

Not a check, so no tier runs it: a colony to look at by hand, with one faction per era and wild and tame animals.

```bash
python tools/eralooks_test.py run eralooks-sandbox --keep-open
```

## Result recording

The harness writes `summary.txt` and `report.json` per run. Read the latest with `python tools/eralooks_test.py latest`. Record a result here only for a release or a notable finding.

## 2026-11-17 - Quick tier after the tiered split

- Version/environment: 0.3.0 plus the `CoreBridge` load fix (`71ffb6a`); RimWorld 1.7 with Ideology and Biotech; default mod set.
- Actions: `run run-all`.
- Expected: all quick-tier cases pass, with no Era Looks errors.
- Observed: run `20261117-140733-run-all-s1-7d371044`. All 11 behaviour checks passed, with 1 logged error (the harness's deliberate diagnostic) and none naming Era Looks. No "Update threw" in `Player.log`, so the `CoreBridge` load fix holds, and the gene-icon errors are gone now that Biotech is loaded. All patches were in place: 6 theme-copy patterns, 128 status-colour transpilers, 298 Inspector Tabs reads, 11 animal draws, and the Factions rows and pages. 118 copied colours matched Core.
- Status: Passed.
- Follow-up: `UpdateErrors` has not yet seen an exception, because the fix removed the only known one. Its first real catch will confirm it.
