# Economy extraction and build-order simulator

For independent build decisions, use the [explicit investment-action search](#explicit-investment-action-search-october-7-2026). It replaces fixed stage widths with paid action sequences and exposes the resulting build orders in `web/action-search.html`.

## Automated economy calibration

Current preferred search: `python Tools/Balance/directional_search.py --budget 24 --workers 15 --output Tools/Balance/reports/my-directional-search`. It compares coarse changes at a constant 0.5-second simulation timestep. A rejected candidate has no descendants and receives no neighboring refinement. Only a strictly improving candidate becomes the next center; step sizes shrink around that accepted point. Changed settings can reopen directions because parameters interact. This bounded hill-climbing method does not prove global optimality. The live page shows each candidate's parent, change, measured violation and decision. `test_directional_search.py` explicitly checks that a worsening 100-to-150 result does not spawn 149/151 tests.

Performance: headless compilation now enables optimization; catalog and inventory lookups use indexes. Four complete historical replay comparisons matched exactly, and recovery/Fighter/serialization tests pass. A preliminary single-route benchmark decreased from 11.4 seconds to 5.1 seconds including startup and compilation; concurrent workload affects wall-time measurements. Use 15 workers on the current machine.

`surrogate_search.py` can fit a numerical response model to recorded game runs and screen a million parameter proposals cheaply. These are predictions, not simulations or a balance pass. Held-out log timing errors are saved. `verify_proposals.py` checks proposals against the real game. The earlier broad run is archived under `reports/calibration-v2` (627 policy simulations, no passing balance); the additional 16 model proposals received 82 real checks in `reports/proposal-validation-v2`. Known failed candidates now skip final fine-timestep confirmation. Input/source snapshots preserve the prior simulator implementation independently of later performance changes.

python Tools/Balance/calibrate.py --candidates 128 --workers 12 --finalists 3 --output Tools/Balance/reports/calibration-v2 runs deterministic economic candidates through three coarse policies, sixteen shortlisted economies through nine policies, and three finalists through the full seventeen-policy suite. The best finalist also faces 48 staged factory policies, fine-timestep confirmation, and a fresh exact replay. Add --resume with the same arguments to recover checksum-verified results. Higher --candidates budgets extend a deterministic low-discrepancy sequence; this is bounded exploration, not exhaustive continuous optimization.

Live progress: web/calibration.html reads web/calibration-progress.json every five seconds. Counts distinguish economic candidates and completed policy simulations; the scheduled simulation count grows at each screening stage. Candidate catalogs, policies, source hashes and raw results are archived under the output directory. The live catalog is not automatically overwritten by a failed candidate.

Parameters cover specialist/smelter capital, specialist cycle duration, industrial power, starter power, generator capital, industrial feed rates, research material costs, and installed-capital refund. Refund defaults to 100% when absent; partial refunds floor each invested item stack, preserving input/output cargo and recoverable in-process ingredients. The common game simulation implements this rule, so the experiment does not use a separate refund approximation. Free starter belt kits remain reusable.

Power support follows a documented conservative generator/coal sizing rule. Policy search is still restricted: it does not prove global optimality, and pooled belt budgets are not physical layouts. Near-optimal width penalties and full refund effects must be measured rather than assumed. test_calibrate.py checks candidate determinism and parameter wiring; the headless game tests check refund conservation and repeated deletion.

## Deterministic scripts (current workflow)

Use **widths.py** to vary factory size at every progression stage under the same game catalog, and **balance.py** to judge the resulting experience or search economic parameter candidates. Neither script applies candidate prices to the game.

```powershell
# A reproducible sample of staged factories, with ranked results and build orders.
python Tools/Balance/widths.py --trials 48 --workers 3 --output Tools/Balance/reports/my-widths
# Every combination in the editable grid: currently 2,448 policies.
python Tools/Balance/widths.py --trials 0 --workers 3 --output Tools/Balance/reports/all-widths
# Explicit pass/fail constraints on the current economy.
python Tools/Balance/balance.py evaluate --output Tools/Balance/reports/my-evaluation
# Deterministic bounded parameter search; separate screen and validation steps.
python Tools/Balance/balance.py search --budget 8 --workers 3 --output Tools/Balance/reports/my-calibration
```

Results are in each output directory's `report.html` and `summary.json`. Add `--resume` with identical simulation arguments to use checksum-verified cached replays. Changed physics, catalog or policies are rejected. Reporting/orchestration-only revisions with identical generated inputs reuse verified ticks and preserve prior manifests/source versions. Successful evaluation exits 0; a measured balance failure exits 2; input/execution errors exit 1. Keep output directories distinct.

Inputs: `spec/widths.json` defines width choices at landing, first industry, components and later expansion, lane counts and investment order. `spec/targets.json` defines completion-time ranges, minimum-route ratio, and measurable penalties. Widths generate separate counts for raw extraction, smelting, crafting families, generators and belts; inspect the actual counts instead of interpreting a width as the same number of every building. Stage targets are cumulative. The generator is a documented policy heuristic, not an optimal scheduler for each size.

Default penalty targets: missing two core machines +15% time; missing four +30%; extra two +10%; extra four +20%; extra six +30%; extra twelve +50%. **Six extra means three structural plus three mechanical, six total.** Full validation tries uneven expansion, late expansion, surplus reclamation, alternative early investment and recipe priorities. A timed-out overbuild never earns a passing penalty. Candidate scoring sums normalized target violations, with a required zero score and further validation before PASS.

The real `FactorySimulation`, `FactoryLogistics`, `FactoryRecovery`, `FactoryPowerPriority`, `FactoryMegaproject` and catalog execute headlessly. Policies pay costs and wait through production, research and construction. Belts remain pooled throughput budgets in the planner; physical routing, player action time, combat and arbitrary build/sell schedules are outside this benchmark. `SearchPolicy.reclaimAtBay` is an explicit optional simulated liquidation action, not a game-rule change.

Reproducibility is scoped to the recorded game source/catalog, policies and timestep. Hashes, policies, raw replays and source copies are saved. Price search enumerates a finite configured candidate list and validates the best screened candidates; a failed bounded search does not prove all possible game balances infeasible. A full width sweep does not prove optimality outside the generated policy family.

Tests: `python Tools/Balance/test_balance.py`, `python Tools/Balance/test_widths.py`, plus the existing game and web-model tests below. Current demonstration outputs: `reports/deterministic-v1` (four price candidates; no passing balance) and `reports/widths-v1` (48 completed routes). Fresh calibration replays matched exactly, and both resume paths reused cached results. Publish saved results to the local browser with `python Tools/Balance/publish_results.py --balance Tools/Balance/reports/deterministic-v1 --widths Tools/Balance/reports/widths-v1`. This updates `web/balance-v2.html`, `web/widths.html`, and `web/balance-tests.html`, and exports the best width policy/replay. The archived `sources` tree includes the catalog/spec inputs needed to reproduce a run independently.

## Current balance: six-v4

Default profile: `profiles/six-v4.json`; live report: `web/balance-v2.html` / `web/balance-v4.html`. Previous binary-v3 remains in `web/balance-v3.html`. There is no hard machine-count cap or price escalation by copy number.

The current 0.125-second count sweep finishes with 6 structural, 4 electrical, 6 mechanical, 2 fluid, 6 industrial smelters, 3 industrial iron miners, 2 copper miners, 1 coal miner, 1 silicate miner, 3 industrial generators, 2 retained basic generators and 60 belt tiles. Completion is 127.35 minutes. At 0.0625-second ticks six/seven finish in 127.77 / 127.80 minutes: effectively tied. Five and seven core specialists are almost tied with six at reference resolution; eight takes 129.17 minutes and twelve takes 133.31. Minimum upgraded one-per-family takes 334.20 minutes: **2.62×**, below the earlier 3× target. Strong play remains approximately seven minutes above two hours. Do not describe this as a proven global optimum or a steep penalty at exactly seven.

Compared with binary-v3, specialist building costs are 4× (mechanical 6×), industrial-smelter capital 3×. Specialist recipe times are 37.5%; other normal recipes, construction and research timers are 75%; mining/fuel rates scale correspondingly. Bay/Fighter phase materials are 3×, and phase timers 75%. Emergency recovery and binary power are unchanged. Fighter installed-part quantities now follow its phase BOM so repairs match construction costs. Existing saved Fighter quantities are retained.

`python Tools/Balance/six_report.py` validates catalog/profile/source agreement, measured comparison, binary power and available timestep checks, and writes the report plus exact build-order CSVs. Reproduce live comparisons with `pwsh -File Tools/Balance/run_cases.ps1 -Cases Tools/Balance/reports/six-v4/live.cases.json -Step .125`, using NEW output filenames in the cases file: the runner refuses overwrites. MaterialScale and PhaseScale default to 1; do not apply experimental factors again to the already updated game source.

Earlier cost/fast/feed/heavy/paced/candidate/audit/fine experiments under `reports/six-v4` used archived `before.FactoryMegaproject.cs.txt`; their materials/timer scaling was applied in memory. Only `live-*` replays use the final sources without overrides. Archived source/catalog snapshots and hashes provide provenance. `projectPhaseSource` in the profile records the C# phase block, checked by the report; catalog generation does not rewrite that C# block. `binary_report.py` and `verify_investment.py` describe historical profiles and can overwrite old report URLs; use `six_report.py` for the current page.

Tests: `pwsh -File Tools/Balance/run.ps1 -Command test`, `node Tools/Balance/web/test-model.js`. Existing executable is not rebuilt. Pooled belt budgets, automatic cargo delivery and automated consumer pausing remain benchmark approximations. Arbitrary building liquidation/rebuild schedules are not searched exhaustively.

## Historical balance: binary-v3

The default catalog profile is `profiles/binary-v3.json`. Main-grid and recovery-circuit power are binary. Emergency Core recipes reserve 80 PU for 150 seconds. Enabled idle consumers still draw power; the benchmark explicitly pauses idle/excess consumers, equally for all policies. Paused-machine waiting and whole-grid outage time are reported separately.

`python Tools/Balance/binary_report.py` validates the measured comparison and rebuilds `web/balance-v2.html` (current user URL), `web/balance-v3.html`, and `reports/binary-v3/report.html`. Previous v2 page is preserved as `web/balance-v2-historical.html`. Reports before binary-v3 use historical proportional power and must not be compared as current runs.

At 0.25-second ticks: minimum upgraded one-per-family route 374.36 min; moderate expansion 147.48 min; twelve-per-family expanded route 124.88 min, a 3.00× ratio. At 0.125 seconds the references are 374.31 / 126.07 min (2.97×). This is a best-tested policy, not an exhaustive optimum. The absolute strong-play target is still exceeded by roughly 5–6 minutes. Minimum includes optional industrial replacements, giving it a stronger baseline than the literal bare unlock requirement.

Specialist recipes take 2× v2 time and their capital costs are approximately half v2. The tested expanded route retains 4 iron / 2 copper / 1 coal / 1 silicate industrial miners, 6 industrial smelters, 12 each structural/electrical/mechanical machines, 4 fluid machines, 4 industrial generators, and 2 input lanes per pool. Check actual replay counts for retained starter generators. Physical layouts and player placement time are excluded. Recipe input, pooled belts, output budgets, construction capital, fuel, research and phased projects are included.

Replay using `balance_probe.ps1 -Catalog ... -Policy ... -Output <new-file> -Step .25` with PhaseScale left at 1. Catalog/source snapshots and policy/replay files are under `reports/binary-v3`. Candidate files without `final`, `quarter`, or `eighth` names are tuning experiments, not final comparisons. `verify_investment.py` verifies the historical v2 results only. Tests: `pwsh -File Tools/Balance/run.ps1 -Command test`, `node Tools/Balance/web/test-model.js`.

## Historical balance: investment-v2

The applied catalog is generated from **`profiles/investment-v2.json`**. `expansion-v1.json` and all prior reports are historical. Open `web/balance-v2.html` for the current comparison, or `reports/investment-v2/report.html` offline. The existing executable is not rebuilt by catalog changes.

Whole-chain changes:

- Basic Miner / Smelter / Workshop draw 14 / 24 / 28 PU. Industrial mining and smelting produce 30/min at 35 / 60 PU: five times basic throughput, half the power per item. Industrial purchases and specialist factories have higher capital payments; full reclaim remains available.
- First research costs 120 Plate, 40 Wire, 8 Refractory. Later research consumes more intermediate components. Frames require 3 Beam + 3 Plate; drive units require 3 Motor + 3 Bearing + 2 Frame; cabinets require 3 Power + 3 Control + 2 Frame + 3 Bus. Both universal and specialized frame recipes share the new material cost.
- Fixed Bay work is 696 seconds (11.6 minutes), Fighter work 552 seconds (9.2 minutes), and final research 180 seconds. This reallocates time from fixed timers to production. Phase timings remain executable in `FactoryMegaproject.cs` and are documented in the profile's `projectPhaseSeconds` field; catalog generation does not rewrite C#.
- The headless policy now stages belt lanes through `laneTargets`. At landing, belts precede production; later expansion funds its next generator before the extra belt load. New search seeds establish a viable bootstrap factory, support early expansion, build initial specialists before mass industrial construction, and grow lanes from 1 to 2. Physical belt layout remains outside the model.

Live-rule, matched benchmarks (0.5-second ticks; player action effort excluded):

| Policy | Fighter minutes |
| --- | ---: |
| Minimal early factory | 141.57 |
| Supported second line | 127.63 |
| Supported third line | 118.25 |
| Keep the later factory small | 143.17 |
| One industrial Iron/Copper miner | 158.76 |
| One Structural Fabricator | 173.62 |
| One Mechanical Assembler | 167.34 |
| One Electrical Assembler | 132.40 |
| One industrial generator | 136.31 |
| Keep basic coal supply | 157.62 |
| Equivalent basic equipment with adequate power | 169.15 |
| One Fluid factory (sufficient at this milestone) | 117.73 |

The reference third-line plan uses two basic Iron miners shared across the material mix; blindly increasing every building count is slower. Four/five basic-line experiments approach parity or lose time, rather than giving another large benefit. Full reclamation makes overspending recoverable. First industrial smelter completion moves from 51.16 minutes on the minimal route to 39.36 with the supported second line and 32.92 with the third. Late reference Iron capacity is 120/min: twenty times the landing miner, ten times the doubled early supply.

These are **controlled policies, not skill-level estimates or globally optimal schedules**. A fresh 12-policy search also reaches 119.17 minutes. The reference route is 117.875 minutes at 0.25-second ticks; the five checked routes differ by at most 22.5 seconds. One Fluid factory is sufficient now; later tech may create the need to duplicate it. The tested basic-only alternative is viable, but slower and more energy intensive.

Evidence and repeatability:

- `reports/investment-v2/*.policy.json` and matching `*.replay.json` hold the applied-rule comparisons; `*.quarter.json` are timestep checks. Replay with `search.ps1 -Policy <file> -Output <new file>` using the adjacent catalog snapshot.
- `verify_investment.py` checks the saved pacing, efficiency and branch-deficit contracts. Run fresh replays before using it to validate later balance changes. `balance_report.py` rebuilds the comparison page.
- `reports/whole-chain-r1` through `r4` contain discarded candidate sweeps. Their phase-scale values refer to the pre-change 52-minute phase baseline; **do not replay their 0.4 scale on the already shortened live phases**. The saved pre-change C# phase source and catalog are in `profiles/pre-investment-v2.*`.
- `run.ps1 -Command test` and `node web/test-model.js` pass. Tests for idle power derive the required consumer count from the catalog; project tests lock the approved timers and recipe/material model. Core recovery and research-payment tests remain active.

## Historical basic-expansion investment window

First research now costs **60 Plate, 20 Wire, 4 Refractory**, up from 20 Plate, 10 Wire, 4 Refractory. Its timer remains 60 seconds. This affects all tier-one unlocks. The override is stored in `profiles/expansion-v1.json` under `techs`; the catalog generator now applies technology overrides as well as building/recipe overrides.

Controlled tests in `reports/industrial-window-funded/comparison.json` compare the same downstream industrial targets with one through four basic smelters. The starter factory is built before optional expansion, preventing larger targets from spending the workshop/generator's starter materials. Three smelters include an extra Iron miner; four add another Copper miner. The new payment produces Fighter times of **135.66, 133.25, 128.44, 130.06 minutes** respectively. Under the previous payment the same orders took 127.59, 126.58, 124.78, 126.21 minutes. Basic expansion already helped modestly; the new gate increases its benefit. The second/third smelter's individual funding waits are about 2.10/1.74 minutes. All four routes retain the same ranking at 0.25-second ticks (completion changes by at most 16.75 seconds). The batch search now includes optional early expansion targets after the viable starter factory, so it can explore these orders without spending essential starter capital prematurely. These are matched-policy comparisons, not global optima or claims of pre-unlock payback for every path.

A 100-Plate/30-Wire research payment delayed progression more; a 600-second timer with the old payment mostly extended waiting. Neither was applied. The early draft experiment in `reports/industrial-window` has invalid expansion orders that starve their starter workshops/generator; use the corrected `industrial-window-funded` results. `industrial_window.py` reproduces the four gate comparisons in a fresh output directory. Its `current` label denotes the old 20/10/4 payment for this experiment, not the subsequently applied live payment.

The new research gate is represented in the live catalog and rebuilt HTML planner. It does not rebuild an existing Unity executable. Earlier 48-policy reports and campaign timings below retain their original catalog snapshots; they are historical, not measurements of the new payment. Best matched completion remains above the 90–120 minute target; later pacing has not been retuned to compensate.

## Chronological batch search

`search.ps1` searches staged investment policies using the real game simulation. It never grants research, money or production materials. Construction and research are paid from evolving inventory; mining, production, changeovers, construction, power, research and the Fighter project advance concurrently. No player input/placement time is charged. The game catalog is read-only.

Run a large search (PowerShell 7.4+, Python 3 for the report; no external packages):

```powershell
pwsh -NoProfile -File Tools/Balance/search.ps1 -Trials 1000 -Workers 4 -BatchSize 24 -Seed 73421
```

The default output is a new dated directory under `Tools/Balance/reports`. Open its `report.html`. Search results are written to `trials.jsonl` after each batch, so completed batches survive interruption. This version does not resume an interrupted search automatically. A nonempty output directory is rejected rather than overwritten. The 48-policy validation batches took about 106–161 seconds on the development machine; 1,000 similar evaluations would take roughly 35–60 minutes, depending strongly on failed/slow policies and hardware. Four workers are independent simulations, not four agents.

Use `-Catalog path/to/candidate.catalog.json` to compare a proposed balance without changing the live game. Hold seed, timestep, batch size and evaluation budget constant when comparing catalogs. The source and catalog snapshots identify the rules used. Replaying currently compiles the workspace sources; restore the saved sources in an isolated workspace if game logic has changed since the run.

Replay a policy, using its adjacent catalog snapshot by default:

```powershell
pwsh -NoProfile -File Tools/Balance/search.ps1 -Policy Tools/Balance/reports/search-validated/best-policy.json -Output Tools/Balance/reports/replay.json
```

An exported report policy should be placed alongside that run's `catalog.snapshot.json`, or pass `-Catalog` explicitly. Use `-Step 0.25` for timestep sensitivity; the maximum allowed step is 0.5 seconds to avoid skipping short recipe cycles. The report is an offline results viewer, not a browser control panel for launching processes.

### What is searched

Policies contain ordered cumulative building targets at landing and after each research tier. Mutation changes counts, within-stage order, deferred investments, the number of parallel T1 input lanes, reclaim decisions and one of two recipe-priority heuristics. A seeded evolutionary search mixes mutations of fast successful policies with fresh seeds. Duplicate configurations are excluded. Initial bootstrap equipment is retained so most candidates are feasible. Policies stop at six simulated hours by default; hitting that limit records a failed search policy, not proof of an unrecoverable game state. Counts are bounded at 12 and lanes at 4 in the generated search space; hand-authored policies can use different targets.

This does **not** enumerate every possible action sequence, optimize arbitrary recipe assignments, or freely interleave every investment with research. Stage expansion must finish before the next research payment. Zero/decreased targets do not demolish buildings; they defer/avoid additional purchases. The best result is only the best policy found in this search space. The default seed adapter knows the present game's starter and industrial building IDs. New item costs and recipe chains are read from the catalog; new building families should also be added to `BatchSearch.Seed`. The research loop follows the catalog's tier count. A new end goal requires another goal driver beside the Fighter driver in `FactoryPlanner.Run`.

### Transport contract

The batch model automatically collects output into shared cargo, limited to a nominal 3 items/s per machine. Paid, completed T1 belt tiles provide shared input throughput to active production/generator families: 3 items/s per lane, with five tiles per lane by default. Incomplete infrastructure provides a proportionally smaller shared budget. Every batch pays its full ingredient count against that family's budget, and generator Carbon shares the generator input budget. Transport slows with grid speed. Belt kits are consumed before charging plate; installed belts use actual construction and power rules. Budgets have only enough buffering for one largest recipe batch (at least three items).

These are pooled logistics constraints, **not a physically routed factory**. Transfers have no spatial latency; output collection uses shared cargo; lane distribution adapts automatically between active families. Final project/construction deliveries have no route limit. The runner buys required belt tiles before the rest of each expansion stage. Layout, mergers, port contention and manual rerouting need a later routing adapter. Do not call these results a verified belt layout or player playtime.

### Saved evidence

- `trials.jsonl`: each tested policy, success/failure reason, completion time, electricity, extracted raw units, funding wait and brownout time.
- `pareto.json`: policies not dominated on completion time, electricity and total raw extraction by another tested successful policy. Raw units include surplus held in cargo and are unweighted; they are not a purchase cost. Compare individual materials before setting prices.
- `best-policy.json`: executable policy for the fastest successful run.
- `best-replay.json`: actual purchase/reclaim/research/project timeline, individual funding waits, machine-state totals and one-minute inventory/queued-cost-shortfall samples.
- `report.html`: interactive policy comparison, resource/time scatterplot, frontier filter, JSON policy export and the fastest policy's actual purchase timeline.
- `catalog.snapshot.json`, `sources/`, `source-hashes.json`, `settings.json`: provenance for the run.

Funding wait is wall time when the next queued building cannot be paid for; other work may continue. Transport wait is summed over attempted machine feeds, so it can exceed wall time. Machine-state durations include idle capacity when the scheduling policy has no useful work; an idle machine is not automatically a material shortage. `capitalSpent` records gross actual construction payments, including zero plate payment for free belt kits; reclaimed refunds appear in inventory, not as negative gross spending. Electricity estimates delivered game PU-hours, not total installed generation. This runner tracks resources, energy and time but does not collapse them into an arbitrary weighted score.

The first validated 48-policy cohort completed Fighters in roughly 128–306 minutes. This is a search-population range, **not a prediction of normal versus poor players**. The fastest initial replay was deterministic across all fields; halving its tick changed completion by 17.5 seconds. No game prices were tuned by this implementation. Strong-play targets remain 90–120 minutes and need a larger search plus balance experiments before judging whether the current rules meet them.

## Interactive HTML planner

Open [web/factory-planner.html](web/factory-planner.html) directly in a browser. It is a self-contained offline page with no network dependencies. Adjust machine counts to explore construction materials, production workload, power/Carbon shortages, and bottlenecks. Pin a configuration to compare time and Iron changes. Click a node for its bill of materials and recipes.

This is a capacity model, separate from the chronological C# campaign planner. It expands all selected factory capital plus project/research payments into raw materials and shares recipe work across each family. It does not schedule construction funding, technology acquisition, changeovers or actual belt routes. The assumptions panel describes the constant-load grid and fuel approximation. Do not compare its time directly with measured landing-to-Fighter playthroughs.

Refresh its embedded data after catalog/phase changes with `python Tools/Balance/web/build.py`. Run model checks with `node Tools/Balance/web/test-model.js`. `template.html`, `app.js` and `model.js` are the editable sources; the build produces the single deliverable file.

## Active expansion balance

`profiles/expansion-v1.json` is now applied by the catalog generator. The previous catalog is preserved as `profiles/pre-expansion.catalog.json`. Targets are 90–120 minutes for strong planning, 120–180 for normal attentive play, and substantially longer for poor investment. These are design targets; normal human pacing has not been playtested.

Structural, Mechanical and Electrical factories plus Industrial Miners/Smelters/Generators unlock at Basic Industry I. Their initial capital uses early materials, avoiding a circular requirement for advanced machinery. Advanced component recipes and Fluid factories still require Component Industry I. Industrial Construction I unlocks Fighter subsystem and Bay-service workpieces, drones and the tier-three belt upgrade. Orbital Operations I unlocks the Bay itself, so players can prefabricate subsystems during the final research.

| Equipment | Revised capital | Production / power |
| --- | --- | --- |
| Structural | 10 Plate, 4 Beam, 6 Wire | 48 Plate/min; 30 PU |
| Mechanical | 12 Plate, 4 Rod, 6 Wire | Motor cycle 6s; 32 PU |
| Electrical | 10 Plate, 10 Wire, 2 Refractory | Power/Control cycle 7.5s; 30 PU |
| Fluid | 10 Plate, 2 Frame, 1 Pump, 4 Wire | Existing recipes/rates; 34 PU |
| Industrial Miner | 10 Plate, 2 Beam, 6 Wire | 60 raw/min; 45 PU |
| Industrial Smelter | 12 Plate, 4 Beam, 4 Refractory, 6 Wire | 60 ingots/min; 75 PU |
| Industrial Generator | 16 Plate, 4 Beam, 10 Wire, 4 Refractory | 600 PU; 6 Carbon/min |

Starter costs, starter production, research timers, subsystem recipe costs/times, the five Bay phases and five Fighter phases are unchanged. Specialized `spec_*` cycles are one quarter of baseline. Existing full reclaim and free drone/Core recovery remain available: upgrading refunds starter investment rather than stranding it. Generator savings and throughput favor upgrading; twenty-second changeovers favor dedicated machines and batches. Additional machines still cost materials, power and construction time, so excessive expansion can lose time.

Run `pwsh -NoProfile -File Tools/Balance/planner.ps1 -Output Tools/Balance/reports/expansion-final` for fresh-start concurrent benchmarks. It schedules material dependencies, funds construction, selects faster recipes, produces during research/project work, and reclaims replaced starter machines using the actual simulation. Scale 0 declines industrial upgrades/reclamation; scales 1–3 expand progressively larger factories. This planner is heuristic, not an optimal-route solver.

Measured at .5-second ticks:

| Policy | Constructed Fighter |
| --- | --- |
| Smaller upgrading factory | 127.95 min |
| Medium upgrading factory | 117.92 min |
| Large upgrading factory | 119.58 min |
| Specialized crafting but no industrial upgrade/reclaim | 194.61 min |
| Medium with 180 powered belt tiles and supporting generators charged | 125.68 min |

The medium factory finishes with 4 Structural, 4 Electrical, 4 Mechanical, 2 Fluid, 6 Industrial Smelters, 9 Industrial Miners and 3 Industrial Generators. It has zero brownout time; Workshops are reclaimed at minute 13 and the primitive smelter at minute 20.2. Extra capacity pays off from small to medium, but another expansion is slower. The old serial campaign still reaches raid readiness in 37.20 hours and is deliberately not the balance target.

The benchmark uses automatic manual transfers without travel/action delay. `-LogisticsOnly` charges actual construction and idle power for 180 belt tiles and extra generators, but does **not** route those tiles; it is a cost/power sensitivity check, not a logistics validation. Normal-player time and real routing require playtesting. Do not describe 117.92 minutes as the fastest possible route or 125.68 as a working belt factory.

Use `-Step 0.25` for timestep sensitivity. Tested policies changed by less than 12 seconds when the timestep was halved. Existing recovery, project, migration and core simulation tests run with `run.ps1 -Command test`. Earlier campaign timings later in this document describe the pre-expansion catalog unless explicitly identified above.

Headless balance/recovery/project checks pass. The final full Unity suite currently reports `PORT/BRIDGE FAILED: undo restores both lanes and independent inventories` in the concurrently added `FactoryBridgeTests`; do not treat the earlier simulation-tests success file as confirmation of this latest full run. The existing Windows executable has not been rebuilt by this balance pass.

Run from the project root with **PowerShell 7.4+**. No Unity editor, .NET SDK, or downloaded packages are needed. The wrapper compiles the existing game simulation sources in memory. Gameplay files and player saves are untouched.

```powershell
pwsh -NoProfile -File Tools/Balance/run.ps1 -Command export
pwsh -NoProfile -File Tools/Balance/run.ps1 -Command run
pwsh -NoProfile -File Tools/Balance/run.ps1 -Command test
pwsh -NoProfile -File Tools/Balance/run.ps1 -Command campaign
```

`export` writes `reports/economy.json`, `buildings.csv`, `recipes.csv`, and `catalog.snapshot.json`. These cover item definitions, costs, construction times, recipes, nominal rates, requested power, generator output/fuel, research, starting stock, Bay/Fighter phases, and readiness reserves. The C# rules summary is a documented snapshot; executable behavior always comes from the game sources. Re-export after game changes.

`run` compares the two supplied orders through Basic Industry I. Each receives a fresh landing with exact starter stock. Actions are sequential, but placing a blueprint does **not** wait for completion: construction, mining, crafting, research and logistics run concurrently during waits. Successful means all requested actions finished, not necessarily that the campaign is complete. End with a `raidReady` wait to require that objective.

`campaign` executes the game's existing full-campaign driver, from starter stock to raid readiness, without grants. Its result goes to `Logs/campaign-test.txt`. This regression uses the live catalog and its own .5-second tick, ignoring `-Catalog`; it is a feasibility check, not an optimizer or comparison scenario.

## Compare and tune

From a PowerShell session, pass multiple scenarios as an array:

```powershell
& ./Tools/Balance/run.ps1 -Scenario @('./Tools/Balance/scenarios/industry-first.json', './Tools/Balance/scenarios/power-first.json') -Output ./Tools/Balance/reports
```

Copy `Assets/Resources/catalog.json` and edit costs/times/power in the copy, then use `-Catalog ./my-catalog.json`. This does not modify game balance. To try different build orders, copy an example JSON and rearrange/add actions. Unknown JSON fields are rejected to catch misspellings. Catalog IDs and integer coordinates are required; directions are 0 east, 1 north, 2 west, 3 south. Miners must occupy actual deposits.

Reports contain:

- `comparison.json`: results of every requested scenario.
- `<scenario>.result.json`: success/failure, elapsed seconds, final inventories/machine buffers, production totals, technology, drones, action timeline, and power metrics.
- `<scenario>.timeseries.csv`: sampled generation, demand, brownout ratio, cargo units, and available builders.
- `<scenario>.scenario.json`: input snapshot. Catalog and source SHA-256 hashes identify the rules used; keep the exported catalog snapshot alongside results.

Use unique scenario filenames in a comparison; reports use filenames as their keys. A failed order writes its partial result with the action index, shortages/statuses, and returns a failing process exit. All waits have bounded simulation time.

## Scenario format

```json
{
  "name": "iron-start",
  "step": 0.25,
  "timeout": 7200,
  "sampleEvery": 30,
  "autoCollect": true,
  "autoFuel": true,
  "actions": [
    {"op":"build", "id":"iron", "building":"miner", "x":-18, "y":-4},
    {"op":"wait", "condition":"built", "id":"iron"},
    {"op":"wait", "condition":"cargo", "item":"iron", "amount":20, "timeout":600}
  ]
}
```

| Operation | Parameters / behavior |
| --- | --- |
| `build` | unique `id`, catalog `building`, `x`, `y`, optional `direction`, `autoFeed`; queues real construction |
| `recipe` | machine `id`, catalog `recipe`; enforces family/tech, refunds inputs and applies changeover |
| `autoFeed` | `id`, `enabled`; repeatedly uses the UI's Feed action, including Bay supplies |
| `enable` | `id`, `enabled`; toggles a consumer's work and power |
| `feed`, `collect`, `supply` | `id`; one manual transfer |
| `research` | pays and starts next research, fails if prerequisites are missing |
| `mine`, `stopMining` | `resource`; assigns/releases one recovery drone |
| `coreCraft` | emergency `recipe`; pays and starts one job |
| `startFighter` | commissioned Bay `id` |
| `upgrade` | belt `id`; pays for next unlocked tier |
| `wait` | `seconds`, or `condition` plus optional per-wait `timeout` |

Wait conditions: `built` (`id`), `cargo` or `produced` (`item`, absolute `amount`), `tech` (`amount`), `coreIdle`, `phase` or `fighterPhase` (`id`, number of completed phases in `amount`), `fighter`, `raidReady`. Alias `core` exists from landing. `produced` is lifetime output and excludes starter stock. Cargo waits only count collected stock, not material in buffers or reserved for work.

For a Bay, build `fighterPad` with `autoFeed:true`, wait for `built`, `startFighter`, then wait for `fighter`. Its inputs still have to be manufactured by your configured production lines; this runner never creates missing resources or automatically invents prerequisite chains.

## Interpretation and limits

The default automatic collection/fueling plus per-machine automatic feeding model free, frequent manual interactions allowed by the game. Feed reserves up to five recipe batches, so materials can be unavailable for construction/research while held in a machine. Automatic transfers happen in machine creation order. These choices are part of the build-order policy.

For a belt-driven layout, set `autoCollect:false`, `autoFuel:false`, and omit `autoFeed`; place machines/belts with correct coordinates and ports. The actual game transport and buffer rules then apply. Idle enabled machines still consume power, and construction drones assigned to recovery mining cannot build.

Power metrics are **PU-seconds**, not joules. `generatedPUSeconds` measures available supply; `requestedPUSeconds` measures requested load, including unmet demand. Neither is actual consumed energy. Nominal per-recipe energy excludes idle draw, logistics and changeovers. `brownoutSeconds` counts a reduced ordinary-grid ratio, not the Core's independent slowdown.

The runner uses fixed ticks (default .25 seconds, maximum .5); event times are quantized and the game itself has tick-sensitive batch/fuel/transport behavior. Recheck close comparisons at .125 seconds. Floating-point game time can drift during long campaigns; report duration uses a separate double accumulator. This engine evaluates orders you specify; it does not search for an optimal one. It includes the current game's unlimited deposits and global power grid assumptions.

## Verified baseline

For a full tree and timing breakdown through Fighter construction (excluding raid reserves), run:

```powershell
pwsh -NoProfile -File Tools/Balance/run.ps1 -Command export
pwsh -NoProfile -File Tools/Balance/analysis/analyze.ps1
python Tools/Balance/analysis/summarize.py
```

The analyzer instruments a temporary copy of the current campaign driver and fails if its instrumentation anchors no longer match. It never edits gameplay sources. Reports include per-stage preparation/work times, individual building events, per-recipe production waits and batches, and all effective technology unlocks. Three runs compare the original driver, pausing idle production machines, and disabling the Basic Generator after the Industrial Generator completes. All retain the driver's optional industrial equipment and extra-drone detour. These are controlled policy comparisons, not optimal routes. `full-tech-analysis.json` also expands research + Bay + Fighter material requirements, excluding buildings, startup milestones, fuel and raid reserves and without subtracting starter stock.

The baseline reaches the constructed Fighter at 121,498.5 seconds (33.7496 hours); this is earlier than the raid-readiness endpoint below. Pausing idle machines reaches it at 112,914.5 seconds; retiring the Basic Generator reaches it at 113,348.5 seconds. Both interventions leave the original serial production policy intact.

With the current catalog, industry-first reaches Basic Industry I at 360 seconds and power-first at 395 seconds. Both timings also reproduce at .125-second ticks. This compares the supplied policies only. The full-campaign regression reaches raid readiness at tech 4 with five drones in 38.43 simulated hours (276,671 half-second ticks). Unit checks cover construction payment/concurrency, recovery, idle brownouts, pause behavior, milestones, bounded waits and determinism; a CLI check confirms that a stalled order writes a failure report and returns a nonzero exit code.
## Explicit investment-action search (October 7, 2026)

`ActionPlanner.cs` reuses the deterministic game tick/production dispatcher but replaces staged target enforcement with an ordered action program. `ActionSearch.cs` mutates individual purchases, timing and reclamation, independently per machine/resource. Staged policies are **seeds only**, not fixed expansion ratios in the action search.

```powershell
pwsh -NoProfile -File Tools/Balance/test_actions.ps1
pwsh -NoProfile -File Tools/Balance/run_actions.ps1 -Trials 1024 -Workers 15 -Output Tools/Balance/reports/actions-new
python Tools/Balance/publish_actions.py Tools/Balance/reports/actions-new --archive
python Tools/Balance/publish_actions.py Tools/Balance/reports/actions-new
pwsh -NoProfile -File Tools/Balance/run_actions.ps1 -Command replay -Catalog Tools/Balance/reports/actions-new/catalog.snapshot.json -Program Tools/Balance/reports/actions-new/fastest.program.json -Output Tools/Balance/reports/actions-new/check.json
```

Open `web/action-search.html` through the local report server. Live progress counts actual completed routes; generation size is 64, seed is 731, tick is 0.5 seconds. The default 1,024-route search has 16 generations. `-Trials`, `-Workers`, `-Seed`, `-BatchSize`, `-Step`, `-Catalog`, `-Progress` are CLI options. Existing nonempty output directories are refused. Archive inputs at the start of a run if editing source concurrently; do not overwrite an existing archive with changed model sources.

**Start:** normal landing cargo (120 plate, 50 wire, 24 refractory, 30 belt kits, four construction drones, Core). Every route constructs 15 belts, one miner for each of iron/copper ore/carbon/silicate, one basic smelter, one universal workshop and one generator, in the same order. The bootstrap consumes stock, kits and simulation time. It finishes before investment choices begin. No machines are granted.

Actions are `build` (spec and quantity; each unit funded separately, construction may overlap), `reclaim` (explicit quantity and catalog refund), `research` (zero-based tech index, wait for funding then start), `awaitResearch`, `awaitBuilt`, voluntary `wait` (seconds), `supply`/`awaitPhase` (`bay:0`…`bay:4` or `fighter:0`…`fighter:4`), and `fighter` (start workpiece). Purchases can occur during research or project work. The runner never inserts generators, miners, belts or reclamation to rescue an order. An unaffordable chosen purchase waits while its required materials are produced; an impossible order times out or fails. `lookahead` selects next-phase or remaining-project preproduction during explicit waits. During purchase funding, the dispatcher prioritizes that purchase's material requirements.

The search inserts/removes/moves/resizes purchases, inserts reclamation and 30/120/600-second waits, and changes preproduction lookahead. Research and project prerequisite actions remain ordered. Up to 20 installed machines per type/resource, 400 belt tiles and 400 program actions, with a 12-hour simulation limit. It samples purchase quantities 1/2/3/5/10/19/20; combinations of individual actions can reach every installed count within bounds. Illegal count/tech/reclaim prefixes are rejected before simulation. Failed evaluated routes remain in the results and never enter fastest/slowest-successful rankings.

**Limits:** this is bounded population search, not exhaustive enumeration, a guaranteed optimum, or exact human play. The best 12 successful routes seed later generations with additional exploration from the two starting programs; this can converge locally. Automatic recipe feeding and switching, global pooled cargo, generator fueling/retirement and rotating binary power dispatch remain idealized player automation. Input amounts, recipe times, buffers, fuel, power availability, construction and research are real game ticks. Belts contribute paid, powered pooled T1 family throughput; belt topology/storage/splitter/pole layouts and manual construction effort are not searched. Emergency drone crafting/mining is omitted. The report's summed machine-delay metrics are not wall time and must not be added to action waits.

`reports/actions-v1`: 1,024 evaluated routes, 984 successful, 40 failed; 15 workers, 385.4 seconds including report replays. Fastest tested 138.525 minutes; minimum seed 401.0667 minutes; slowest successful tested 690.3917 minutes. The minimum seed is a defined one-per-family industrial route, not a proof of the cheapest possible factory. No prices or gameplay files were modified by this search. Best/reference seed comparison is 138.525 vs 155.5917 minutes. This run does not meet the 90–120 minute best-play target and does not establish a six-machine optimum.

---

