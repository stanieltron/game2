using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BootstrapFactory;

// Headless adapters for Unity's clone serialization and integer belt coordinates.
namespace UnityEngine
{
    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x,y;
        public Vector2Int(int x,int y){this.x=x;this.y=y;}
        public static Vector2Int zero=>new Vector2Int(0,0);
        public static Vector2Int operator +(Vector2Int a,Vector2Int b)=>new Vector2Int(a.x+b.x,a.y+b.y);
        public static Vector2Int operator -(Vector2Int a,Vector2Int b)=>new Vector2Int(a.x-b.x,a.y-b.y);
        public static bool operator ==(Vector2Int a,Vector2Int b)=>a.Equals(b);
        public static bool operator !=(Vector2Int a,Vector2Int b)=>!a.Equals(b);
        public bool Equals(Vector2Int other)=>x==other.x&&y==other.y;
        public override bool Equals(object other)=>other is Vector2Int v&&Equals(v);
        public override int GetHashCode()=>HashCode.Combine(x,y);
    }
    public static class Debug { public static void Log(object message) => Console.WriteLine(message); }
    public static class JsonUtility
    {
        public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, FactoryBalance.Runner.Json);
        public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value, FactoryBalance.Runner.Json);
    }
}

namespace UnityEditor
{
    public class MenuItem : Attribute { public MenuItem(string name) {} }
}

namespace FactoryBalance
{
    public class Scenario
    {
        public string name = "scenario";
        public float step = .25f, timeout = 86400, sampleEvery = 30;
        public bool autoCollect = true, autoFuel = true;
        public ActionDef[] actions = Array.Empty<ActionDef>();
    }
    public class ActionDef
    {
        public string op, id, building, recipe, item, condition, resource;
        public int x, y, direction, amount;
        public float seconds, timeout;
        public bool enabled = true, autoFeed;
    }
    public class Result
    {
        public string name, error, sourceHash, catalogHash;
        public bool success, raidReady;
        public double seconds, brownoutSeconds, generatedPUSeconds, requestedPUSeconds;
        public int tech, drones;
        public object cargo, produced, machines;
        public List<object> timeline = new List<object>();
    }
    public class Runner
    {
        public static string SourceRoot;
        public static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
        public readonly Simulation sim;
        readonly Scenario plan;
        readonly Dictionary<string, Machine> names = new Dictionary<string, Machine>();
        readonly HashSet<int> feeding = new HashSet<int>();
        readonly List<string> samples = new List<string> { "seconds,tech,generation_PU,requested_PU,power_ratio,cargo_units,builders" };
        readonly Result result;
        double elapsed, nextSample;
        public Runner(Catalog catalog, Scenario scenario)
        {
            if (!(scenario.step > 0 && scenario.step <= .5f) || !float.IsFinite(scenario.timeout) || scenario.timeout <= 0 || !float.IsFinite(scenario.sampleEvery) || scenario.sampleEvery <= 0)
                throw new ArgumentException("step must be in (0, .5]; timeout and sampleEvery must be finite and positive");
            plan = scenario; sim = new Simulation(catalog); names.Add("core", sim.world.machines.Single());
            result = new Result { name = plan.name };
        }
        public static Catalog Load(string path) => JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path), Json);
        static object Stock(Inventory inventory) => inventory.stacks.Where(s => s.amount != 0).ToDictionary(s => s.item, s => s.amount);
        Machine Get(string id) => id != null && names.TryGetValue(id, out var m) ? m : throw new ArgumentException("Unknown machine alias: " + id);
        void Tick(float dt)
        {
            if (elapsed + dt > plan.timeout + .00001) throw new InvalidOperationException("Scenario time limit reached");
            if (plan.autoCollect) foreach (var m in sim.world.machines.Where(m => sim.Complete(m) && !sim.IsBay(m))) sim.Collect(m);
            foreach (var m in sim.world.machines.Where(m => (sim.Complete(m) || sim.IsBay(m)) && m.enabled))
            {
                if (feeding.Contains(m.uid)) sim.Feed(m);
                if (plan.autoFuel && sim.data.Building(m.def).family == "generator" && m.input.Get("carbon") < 2 && sim.world.cargo.Get("carbon") > 0)
                { sim.world.cargo.Add("carbon", -1); m.input.Add("carbon", 1); }
            }
            sim.Tick(dt); elapsed += dt;
            if (plan.autoCollect) foreach (var m in sim.world.machines.Where(m => sim.Complete(m) && !sim.IsBay(m))) sim.Collect(m);
            if (sim.ratio < .999f) result.brownoutSeconds += dt;
            result.generatedPUSeconds += sim.generation * dt;
            result.requestedPUSeconds += (sim.demand + sim.critical + sim.CoreRequestedPower) * dt;
            if (elapsed >= nextSample)
            {
                samples.Add(FormattableString.Invariant($"{elapsed:F3},{sim.world.tech},{sim.generation},{sim.demand + sim.critical + sim.CoreRequestedPower},{sim.ratio},{sim.world.cargo.Total},{sim.AvailableBuilders}"));
                nextSample = elapsed + plan.sampleEvery;
            }
        }
        void Wait(Func<bool> done, float timeout, string label)
        {
            double deadline = Math.Min(plan.timeout, elapsed + (timeout > 0 ? timeout : plan.timeout));
            while (!done())
            {
                if (elapsed >= deadline - .00001) throw new InvalidOperationException("Timed out waiting for " + label + ". " + Diagnostics());
                Tick((float)Math.Min(plan.step, deadline - elapsed));
            }
        }
        string Diagnostics() => "Cargo: " + JsonSerializer.Serialize(Stock(sim.world.cargo)) + "; " + string.Join("; ", names.Select(p => p.Key + ": " + p.Value.status)) + "; research: " + sim.ResearchError();
        bool Condition(ActionDef a)
        {
            switch (a.condition)
            {
                case "built": return sim.Complete(Get(a.id));
                case "cargo": return sim.world.cargo.Get(a.item) >= a.amount;
                case "produced": return sim.world.produced.Get(a.item) >= a.amount;
                case "tech": return sim.world.tech >= a.amount;
                case "coreIdle": return !sim.CoreCrafting;
                case "phase": return Get(a.id).projectPhase >= a.amount;
                case "fighterPhase": return Get(a.id).assemblyPhase >= a.amount;
                case "fighter": return sim.BootstrapComplete;
                case "raidReady": return sim.RaidReady;
                default: throw new ArgumentException("Unknown wait condition: " + a.condition);
            }
        }
        public void Execute(ActionDef a)
        {
            switch (a.op)
            {
                case "build":
                    if (string.IsNullOrWhiteSpace(a.id) || names.ContainsKey(a.id)) throw new ArgumentException("Build needs a unique id");
                    if (a.direction < 0 || a.direction > 3) throw new ArgumentException("direction must be 0..3");
                    var m = sim.Place(a.building, a.x, a.y, a.direction);
                    if (m == null) throw new InvalidOperationException(sim.notice);
                    names.Add(a.id, m); if (a.autoFeed) feeding.Add(m.uid); break;
                case "recipe":
                    var machine = Get(a.id); var recipe = sim.data.Recipe(a.recipe);
                    if (recipe == null || recipe.family != sim.data.Building(machine.def).family || recipe.tech > sim.world.tech) throw new ArgumentException("Recipe unavailable: " + a.recipe);
                    sim.SetRecipe(machine, a.recipe); break;
                case "autoFeed": if (a.enabled) feeding.Add(Get(a.id).uid); else feeding.Remove(Get(a.id).uid); break;
                case "enable": Get(a.id).enabled = a.enabled; break;
                case "feed": sim.Feed(Get(a.id)); break;
                case "collect": sim.Collect(Get(a.id)); break;
                case "supply": sim.SupplyProject(Get(a.id)); break;
                case "research":
                    if (sim.ResearchError() != "") throw new InvalidOperationException(sim.ResearchError());
                    sim.StartResearch(); break;
                case "mine": if (!sim.StartDroneMining(a.resource)) throw new InvalidOperationException(sim.notice); break;
                case "stopMining": sim.RemoveMiningDrone(a.resource); break;
                case "coreCraft": if (!sim.StartCoreCraft(a.recipe)) throw new InvalidOperationException(sim.notice); break;
                case "startFighter": if (!sim.StartFighter(Get(a.id))) throw new InvalidOperationException(sim.notice); break;
                case "upgrade": if (!sim.Upgrade(Get(a.id))) throw new InvalidOperationException("Belt upgrade unavailable"); break;
                case "wait":
                    if (!float.IsFinite(a.seconds) || a.seconds < 0 || !float.IsFinite(a.timeout) || a.timeout < 0) throw new ArgumentException("Invalid wait duration");
                    if (a.condition != null) Wait(() => Condition(a), a.timeout, a.condition + " " + (a.id ?? a.item) + " " + a.amount);
                    else { double until = elapsed + a.seconds; Wait(() => elapsed >= until - .00001, a.seconds, a.seconds + " seconds"); }
                    break;
                default: throw new ArgumentException("Unknown action: " + a.op);
            }
        }
        public Result Run()
        {
            int index = 0;
            try
            {
                foreach (var a in plan.actions)
                {
                    double start = elapsed; Execute(a);
                    result.timeline.Add(new { index, action = a.op, id = a.id, start, end = elapsed }); index++;
                }
                result.success = true;
            }
            catch (Exception e) { result.error = "Action " + index + ": " + e.Message; }
            result.seconds = elapsed; result.tech = sim.world.tech; result.drones = sim.world.drones; result.raidReady = sim.RaidReady;
            result.cargo = Stock(sim.world.cargo); result.produced = Stock(sim.world.produced);
            result.machines = names.Select(p => new { id = p.Key, machine = p.Value }).ToArray();
            return result;
        }
        static string Hash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        static string SourceHash() => Hash(string.Join("\n", new[] { "FactorySimulation", "FactoryLogistics", "FactoryRecovery", "FactoryPowerPriority", "FactoryMegaproject", "FactoryBridges", "FactoryBeltPlacement" }.Select(n => Path.Combine(SourceRoot, "Assets/Scripts", n + ".cs")).Where(File.Exists).Select(File.ReadAllText)));
        public static Result Run(string catalogPath, string scenarioPath, string output)
        {
            var plan = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(scenarioPath), Json);
            var runner = new Runner(Load(catalogPath), plan); var r = runner.Run();
            r.catalogHash = Hash(File.ReadAllText(catalogPath));
            r.sourceHash = SourceHash();
            string name = Path.GetFileNameWithoutExtension(scenarioPath);
            File.WriteAllText(Path.Combine(output, name + ".result.json"), JsonSerializer.Serialize(r, Json));
            File.WriteAllLines(Path.Combine(output, name + ".timeseries.csv"), runner.samples);
            File.WriteAllText(Path.Combine(output, name + ".scenario.json"), JsonSerializer.Serialize(plan, Json));
            return r;
        }
        public static void Export(string catalogPath, string output)
        {
            var c = Load(catalogPath); var s = new Simulation(c);
            var export = new {
                sourceHash = SourceHash(), catalogHash = Hash(File.ReadAllText(catalogPath)),
                catalog = c, bayPhases = Simulation.BayPhases, fighterPhases = Simulation.FighterPhases,
                readiness = Simulation.ReadyStock, starter = new { cargo = Stock(s.world.cargo), s.world.drones, s.world.beltKits },
                rules = new { coreGenerationPU = 80, droneMiningSeconds = Simulation.DroneMiningSeconds, coreRequestedPU = 80,
                    specializedChangeoverSeconds = 20, machineBuffer = 120, minerBuffer = 40, generatorBuffer = 40, storageBuffer = 40,
                    beltSlots = 3, splitterBuffer = 12, beltItemsPerSecond = new[] {3,4,5},
                    beltUpgradeCosts = new[] {new[] {new Stack("plate",1),new Stack("wire",1)},new[] {new Stack("plate",1),new Stack("bus",1)}},
                    beltUpgradeTech = new[] {1,3}, idleBayPU = 40, loadingSeconds = 30, loadingPU = 60, repairSeconds = 120, repairPU = 120,
                    firstResearchProduced = new { plate = 20, wire = 10 },
                    provenance = "Rules summary is a documented snapshot of C# constants. Simulation executes linked game source; sourceHash identifies that version.",
                    power = "Enabled completed consumers draw power even while idle. Binary main-grid cutoff after Core and connected last-generator carbon recovery route. Recovery circuit is also binary.",
                    construction = "Ordinary construction uses drones, without grid power. Bay phases use drones and power."
                },
                recipeRates = c.recipes.Select(r => new { r.id, r.family, r.tech, r.seconds, inputs = r.inputs, outputs = r.outputs,
                    outputsPerMinute = r.outputs.ToDictionary(v => v.item, v => v.amount * 60.0 / r.seconds),
                    compatibleBuildings = c.buildings.Where(b => b.family == r.family).Select(b => new {
                        b.id, requestedPU = b.power, nominalPUSecondsPerBatch = b.power * r.seconds
                    }), coreNominalPUSeconds = r.family == "emergency" ? (double?)(80 * r.seconds) : null,
                    r.powerOverride, powerNote = "Normal runtime uses building.power; powerOverride is catalog metadata. Emergency jobs reserve 80 PU for 150 seconds." })
            };
            File.WriteAllText(Path.Combine(output, "economy.json"), JsonSerializer.Serialize(export, Json));
            var rows = new List<string> { "id,family,tech,construction_seconds,power_PU,generation_PU,fuel_per_minute,rate_per_minute,cost" };
            rows.AddRange(c.buildings.Select(b => FormattableString.Invariant($"{b.id},{b.family},{b.tech},{b.seconds},{b.power},{b.generation},{b.fuelRate},{b.rate},\"{string.Join("; ", b.cost.Select(v => v.amount + " " + v.item))}\"")));
            File.WriteAllLines(Path.Combine(output, "buildings.csv"), rows);
            var recipeRows = new List<string> { "id,family,tech,cycle_seconds,inputs,outputs,outputs_per_minute" };
            recipeRows.AddRange(c.recipes.Select(r => FormattableString.Invariant($"{r.id},{r.family},{r.tech},{r.seconds},\"{string.Join("; ", r.inputs.Select(v => v.amount + " " + v.item))}\",\"{string.Join("; ", r.outputs.Select(v => v.amount + " " + v.item))}\",\"{string.Join("; ", r.outputs.Select(v => (v.amount * 60.0 / r.seconds).ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " " + v.item))}\"")));
            File.WriteAllLines(Path.Combine(output, "recipes.csv"), recipeRows);
            File.WriteAllText(Path.Combine(output, "catalog.snapshot.json"), JsonSerializer.Serialize(c, Json));
        }
    }
}
