# Bootstrap Factory

The headless [balancing engine](Tools/Balance/README.md) exports the economy and runs editable JSON build orders against the actual game simulation. Run `pwsh -NoProfile -File Tools/Balance/run.ps1` to compare the supplied orders, or add `-Command export` to extract costs, production, power, research and project data. The expansion balance pass targets 90–120 minutes for strong factory planning and 120–180 minutes for ordinary attentive play; see the measured limits and profiles in that guide.

A Unity 6 top-down factory prototype based on `bootstrap_factory_implementation_spec_v2.docx`. Starts at landing and ends at a raid-ready Fighter Mk1. Raids are outside the specification's scope.

## Play

Run `Builds/BootstrapFactory-Updated/BootstrapFactory.exe`. The first launch opens the field manual. Saves are stored in `%USERPROFILE%/AppData/LocalLow/Landing Works/Bootstrap Factory/factory-v1.json`, with a backup of the previous save.

Select a building on the left and click the ground to place its blueprint. Miners require a resource patch. Four construction drones work independently; blueprints without sufficient cargo wait for materials. Clicking a built machine opens its recipe, input, and output controls.

| Control | Action |
| --- | --- |
| Left click | Place or inspect |
| Right mouse / drag | Mark one object / add a group to the deletion selection |
| Delete selected button | Delete all marked objects as one undoable action |
| Tab / Build menu button | Open or close the construction list |
| Ctrl+Z | Undo the last deletion group |
| Escape | Close inspection / cancel building tool / clear marks |
| R | Rotate placement or selected machine and its ports |
| B | Select belt; drag a straight preview, release to build |
| X | Toggle mark mode; click to mark/unmark, drag to paint the chosen action |
| WASD / middle mouse | Pan |
| Scroll / Home | Zoom / return to Core |
| Space | Pause / resume |
| + / - or . / , | Increase / decrease developer speed |
| F1 | Field manual |
| F5 / F9 | Save / load |

The **DEV SPEED** toolbar offers 1x, 4x, 16x, 64x and 256x simulation multipliers, plus pause. The current multiplier is displayed above the presets. Space resumes at the previous multiplier. Acceleration applies equally to production, mining, fuel burn, construction, research, changeovers and belt transport; balance data is unchanged. The field manual pauses the simulation. Reclaim refunds construction materials and buffered ingredients. Starter belt kits are used before Steel Plate.

## First production loop

1. Place an iron miner with its output facing east. The iron deposit is west of the Core.
2. Place a Primitive Smelter to its east. Connect its input edge to the miner's orange output port with belts, or use **Collect output** on the miner and **Feed inputs** on the smelter.
3. Feed the resulting Crude Steel into a Universal Workshop with the Steel Plate recipe.
4. Collect plates, or route the workshop output into a cyan input port of the Command Core.
5. Build a Carbon Miner and Basic Generator. Feed Carbon into the generator. The Core's emergency 80 PU cannot support unlimited expansion.
6. Mine and smelt Copper, then change the workshop to Copper Wire. Produce 20 Plate and 10 Wire to qualify for Basic Industry I. Starter materials don't count toward this milestone.
7. Research and follow the Operations checklist. Complete the Fighter Bay and persistent Fighter projects, then collect sortie reserves and confirm raid readiness.

Machines can transfer directly into an adjacent compatible machine; belts extend the connection. Every edge tile is a socket. A 2x2 production machine has two output sockets on its selected side and six inputs on the other three sides; larger machines scale with their edge length. Outputs alternate when both can accept cargo. Generators accept every edge; miners only output. The six-port splitter retains its explicit role configuration. Core output is opt-in: select a cargo filter in the Core inspector. Storage has 40 total item capacity and can be collected manually.

## Implemented scope

- All 38 item/unit definitions, 20 building types (including the new splitter), and four technologies from the baseline.
- 53 machine/emergency recipes, including workshop and specialized variants, industrial equipment prerequisites, drones, fuel, ammo, and Fighter subsystem workpieces.
- Exact starter cache, material-paid construction queues, parallel drones, production milestones, timed research, and specialized machine changeovers.
- Priority power for Core recovery, then Carbon miners connected to the last generator, fuel-consuming generators, proportional brownouts, pausable consumers, and powered belts including empty tiles.
- Three belt steps, upgrades, buffered directional transport, reclaim, JSON save/load, and raid-stock validation.
- Procedural top-down graphics, construction drones, map pan/zoom, placement preview, minimap, machine inspector, cargo browser, research and milestone panels.

## Decisions where the specification is incomplete

- A sector-wide shared power grid. Power poles can be built but are decorative; there is no range/connectivity simulation yet.
- Inexhaustible resource patches on a 240-by-180 build sector with 68 procedurally distributed deposits. No avatar, combat, pollution, or day/night mechanics.
- Manual cargo transfer is available at any distance. Construction materials and research are drawn from Command Cargo; production itself uses local machine buffers.
- Universal and specialized production buffers hold 120 total units; miners and generators hold 40; each belt has three transport slots. Core cargo is unbounded.
- The expansion profile supersedes baseline specialized rates: Structural Fabricators roll 48 Plates/min; Electrical Assemblers make 32 Copper Bus/min. Basic Electrical Parts still take 40 seconds in the starter Workshop, versus 7.5 seconds in a specialized assembler. Recipe changes still take 20 seconds, rewarding dedicated lines and batching.
- Reclaim refunds the entire build investment; changing recipes returns inputs and resets work. Starter belt-kit reclaim returns the kit.
- One persistent Fighter is assembled in the Bay. Reaching readiness does not consume it or reserves. Raid simulation is outside scope.

## Unity MCP

CoplayDev MCP for Unity v10.0.0 is installed as a local Unity package under `.setup/unity-mcp`. Its Python server runs at `http://127.0.0.1:8080/mcp`; Codex has a global `unity` entry pointing to it. The editor connects automatically using `Assets/Editor/FactoryMcpSetup.cs`.

Run `Start-UnityMCP.ps1` to start the local MCP server and editor. Codex may need its MCP connections reloaded to expose a newly added server in an existing chat. The CLI verification helper works independently:

```powershell
.setup/unity-mcp/Server/.venv/Scripts/python.exe Tools/mcp_check.py
.setup/unity-mcp/Server/.venv/Scripts/python.exe Tools/mcp_check.py resource mcpforunity://instances
```

To reproduce the ignored local package checkout:

```powershell
git clone --depth 1 --branch v10.0.0 https://github.com/CoplayDev/unity-mcp.git .setup/unity-mcp
uv sync --project .setup/unity-mcp/Server
```

The Microsoft Store Hub license was initially visible only inside its package cache. The existing active entitlement file was copied to the standard local Unity license folder so the same installed editor could run from the CLI. No credentials or license data are stored in this project.

## Build and validate

Open the project in Unity 6000.6.4f1. Use **Factory → Create Landing Scene**, **Factory → Run Simulation Tests**, **Factory → Test Full Campaign**, or **Factory → Build Windows**. The Windows build runs the simulation suite automatically.

Balance data lives in `Assets/Resources/catalog.json`; runtime reads it directly. `Tools/create_catalog.py` regenerates that file from the versioned baseline plus `Tools/Balance/profiles/expansion-v1.json`, overwriting manual JSON tuning. Use `--baseline --output <path>` to reproduce the old rules without overwriting the live catalog. Existing built executables need rebuilding to include changed resource data.

The full-campaign test automates manual transfer and crafting with real costs, timers, research, power and starting stock. It exercises progression through industrial construction, drone production and the Fighter stock requirement. The simulation suite also checks conservation, belt capacity, placement, reclaim, changeover, serialization and brownout recovery.

Running the built player with `--factory-qa` uses an isolated temporary simulation, captures gameplay, moving cargo, splitter inspection, deletion selection and undo screens under `QA/`, and exits without loading or saving the player's factory.

## Logistics and interaction update

Belts display their actual stored cargo as outlined colored squares. They accept rear and side drop-offs, curve toward their output arrow, and stop when blocked. Opposing fronts do not transfer. At high developer speeds individual items naturally become hard to track; use 1x to inspect flow.

The 2x2 splitter has six numbered ports. While the Belt tool is selected, empty adjacent connection tiles show cyan input squares and orange output squares. The map has no port labels; connection markers disappear when belt placement is canceled. Click the splitter to choose one input and any two distinct outputs; selecting an occupied role swaps assignments. It alternates available outputs and falls back to the unblocked one. R rotates the complete port arrangement. Its provisional cost is 4 Plate + 2 Wire, construction 8 seconds, power 8 PU; existing recipe balance is unchanged.

The persistent **DO THIS** tracker has 14 steps with explicit **Complete step** buttons. Construction is counted only when finished. Clicking a building opens its paused inspection window with buffers, recipes, rotation and input/output diagram. Escape closes it. The dedicated build menu opens/closes with its button or Tab; its categories and list scroll to all unlocked structures. Deletion controls remain accessible when the construction list is closed.

Right-drag marks overlapping structures in red; releasing the mouse does not delete anything. Additional drags add more objects to the selection. Press **Delete selected** to delete the marked group, or **Clear marks** / Escape to cancel. Ctrl+Z or **Undo delete** restores the last group, including buffered materials and production state. The Core is protected. Undo requires the original footprints to remain free and refunded materials to remain available; history holds 30 groups in the current session and is not saved.

New landings place starter deposits farther from the Core. Existing saves keep their original starter extraction sites while gaining the expanded map and additional deposits. Click the minimap to travel quickly. Existing saves are not reset automatically.

Building placement previews show cyan input and orange output connection squares and rotate with R. An existing incompatible belt is marked red: a machine input requires a belt pointing straight into its port, while an output may drop onto a perpendicular belt.

Belt placement previews render the actual belt and a prominent direction arrow. R or the Rotate button rotates the next placement even after using the build menu. The construction panel stays on the active tool while painting belts; new placements use the displayed direction.


Rendering regression checks: QA captures are grouped by actual window resolution. Run python Tools/verify_belt_render.py QA/1280x720 QA/1600x900 QA/1920x1080 after capturing those sizes. It checks rail pixels on the placed vertical belt and all four belt previews. Line drawing uses local axis-aligned spans, avoiding rotation pivots inside scaled GUI groups.


Unfinished sites render as dashed blueprint footprints (orange while unfunded, cyan while funded), not finished machines. Inspection and the side panel list Command Cargo, required amount and exact shortage per construction material, plus stock held in storage. Paid sites report supplied materials. Placement also shows shortage quantities. Factory / Build Windows Update creates Builds/BootstrapFactory-Updated without overwriting an open game.


All construction tools reveal free input/output connection squares on existing machines. Delete removes the marked selection (Ctrl+Z restores it); Escape clears marks. The Research tab now separates lifetime production prerequisites from material payment, shows per-material progress and uncollected stock, and displays time, power, pause status and exact blocking requirements. Research costs remain unchanged and are paid once from Command Cargo.


## Emergency recovery and visible unlocks

Press **C**, click **Core / Recovery**, or select the Core. Use **+ / -** beside each resource to assign or return individual drones. Each assigned drone mines two raw units per minute from the nearest matching starter-region deposit, requires no grid power, and cannot construct while assigned. Several drones can mine the same or different resources.

The Core has exactly three emergency recipes: 1 Iron Ore -> 1 Plate; 1 Copper Ore -> 2 Wire; 2 Silicate -> 1 Refractory. Each requests 200 PU and has a 60-second nominal cycle. The Core's 80 PU alone gives 40% speed (150 seconds per batch). Recovery has first power priority; surplus generation powers the rest of the factory. Input reservation, cancellation refunds and progress persist across saves.

Building selection now always shows costs, missing amounts in the build menu, and a prominent shortage banner on the map. Switching explicitly to Cargo or Research cancels placement. Research has **Requirements** and **Unlocks** views; unlocks list newly available buildings, recipes and belt upgrades from the actual catalog.

Recovery tests exercise a zero-material start through refractory, plate and wire production, drone reservation, save/load, cancellation refunds, and Core-only slowdown and surplus allocation with external generation. QA screenshots include shortages, research unlocks and Core recovery.

## Straight belt placement and unmarking

Belt placement is staged: press on the start tile, drag a straight line, then release over the map to commit. The first movement locks the horizontal/vertical axis. Moving back toward the start shrinks the preview; only the final preview is built. The starting tile is included, so moving five tiles left and then three right leaves three tiles. R reverses flow during a drag. Escape, right-click, releasing outside the map, or losing window focus cancels without spending materials. A blocking building rejects the whole route without partial placement.

Placing over an existing belt changes its direction in place, retaining cargo, tier, construction state and investment. Only new tiles use belt kits/materials. Existing tiles are not demolished or charged again.

In delete mode, clicking a marked object unmarks it. A left-drag beginning on a marked object erases marks; one beginning on an unmarked object adds marks. Each object is touched once per drag. A right-drag over an entirely marked group unmarks that group; otherwise it adds marks. Del still applies deletion, and Ctrl+Z restores the deleted group.


## V2 Fighter megaprojects

Orbital Operations I costs 20 Frame, 8 Motor, 6 Pump, 6 Power, 6 Control and 4 Drive; research takes 600 seconds at 60 PU. The Fighter Bay replaces the disposable assembly pad. Place its footprint, select it, and use **Deliver available Command Cargo**, or belt the current phase's materials into its inputs. Partial deliveries remain stored and requirements show exact shortages. Completed phases are permanent. Each of the five Bay phases occupies one construction drone, takes the specified 300/420/420/360/240 seconds, and requests 20/45/80/70/120 PU while working. Brownouts slow work.

After commissioning, select **Start Fighter Mk1 workpiece**. Its five sequential phases take 300/300/240/300/240 seconds at 120/160/160/160/180 PU. Subsystems are produced in normal specialized machines. The Fighter remains docked in its Bay, with separate skeleton, thrusters, fuel, avionics, weapon and armor condition records. Only one Fighter can be started. Empty operational Bays draw 40 PU.

Raid reserves are 30 Fuel, 300 Ammo, 20 Plate, 4 Armor, 2 Motor, 2 Pump and 2 Control. Loaded fuel/ammo count toward those totals. Refuel/rearm consumes actual cargo and uses 60 PU; major repair uses 120 PU. Damaged subsystems cost the missing fraction of their BOM rounded up; destroyed subsystems require replacement. The spec does not provide service durations, so this prototype uses 30 seconds for loading and 120 seconds for a repair batch. Combat and destruction recovery remain outside scope. Unquantified internal electrical connections are included in the specified avionics package, without an invented extra charge.

Existing saves get a one-time `.pre-v2.bak` before migration. A completed old Fighter Assembly Pad becomes an operational Bay. Unfinished pads return their old construction investment and start at foundation phase. Old in-progress Fighter ingredients are refunded. A stored legacy Fighter becomes the docked persistent asset when a completed old pad exists. Old emergency jobs are canceled/refunded, mining assignments migrate, and existing factories/research remain intact.

Validation covers exact phase durations and power, partial deliveries, worker exclusion, save/load during phases, one-Fighter enforcement, repairs, readiness, old-save migration, and a campaign from exact starter stock with no resource or technology grants.


## Full-edge ports and automatic bridges

Dragging a straight belt route through an existing perpendicular belt automatically adds an upper bridge lane. Both approaches must be inside the preview; a single click continues to reorient an ordinary belt. The lower belt retains direction, tier and cargo. Each lane has independent cargo, capacity and power, and each bridge tile costs the same as one new belt tile (starter kits first, then one Plate). A crossing uses 8 PU with both lanes enabled.

Crossing lanes only accept straight arrivals and never mix cargo. Raised rails are blue with crossbars, and the preview shows the bridge before placement. Any route overlapping an existing bridge is rejected before placing or charging anything; a bridge cannot be bridged. Rotation of crossing lanes is blocked to preserve the two axes. The inspector lets you switch between lower and upper lanes to pause or upgrade each. Deleting a crossing reclaims both layers; undo restores both inventories, directions and upgrades. Saves retain both layers.
