using System;
using System.Linq;
using BootstrapFactory;

namespace FactoryBalance
{
    public static class Tests
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        public static void Run(string catalogPath)
        {
            var c = Runner.Load(catalogPath);
            var indexedInventory=new Inventory();
            Check(indexedInventory.Get("plate")==0,"Missing inventory lookup");
            indexedInventory.Add("plate",3);Check(indexedInventory.Get("plate")==3,"Lookup sees new stack");
            indexedInventory.stacks=new System.Collections.Generic.List<Stack>{new Stack("plate",9)};
            Check(indexedInventory.Get("plate")==9,"Lookup sees replaced deserialized stack list");
            indexedInventory.stacks.Add(new Stack("wire",7));Check(indexedInventory.Get("wire")==7,"Lookup sees appended stack");
            var clonedInventory=UnityEngine.JsonUtility.FromJson<Inventory>(UnityEngine.JsonUtility.ToJson(indexedInventory));
            var movedInventory=new Inventory();clonedInventory.MoveAllTo(movedInventory);
            Check(clonedInventory.Get("plate")==0&&movedInventory.Get("plate")==9,"Indexed inventories survive serialization and transfer");
            foreach(float fraction in new[]{0f,.25f,.5f,1f}){
                var refundCatalog=Runner.Load(catalogPath);refundCatalog.buildingRefund=fraction;
                var refundSim=new Simulation(refundCatalog);
                var refundMachine=new Machine{uid=100,def="workshop",x=40,y=40,paid=true};
                refundMachine.investment.Add("plate",11);refundMachine.input.Add("wire",7);refundMachine.output.Add("plate",3);
                refundSim.world.machines.Add(refundMachine);refundSim.Reindex();
                int initial=refundSim.world.cargo.Get("plate");refundSim.Remove(refundMachine);refundSim.Remove(refundMachine);
                Check(refundSim.world.cargo.Get("plate")==initial+3+(int)Math.Floor(11*fraction),"Capital refund fraction / duplicate reclaim");
                Check(refundSim.world.cargo.Get("wire")==57,"Refund must preserve buffered cargo");
            }
            for(int tech=0;tech<=4;tech++)
            {
                var reachable=new System.Collections.Generic.HashSet<string>(new[]{"iron","copperOre","carbon","silicate"});
                bool changed;
                do { changed=false;
                    foreach(var recipe in c.recipes.Where(v=>v.tech<=tech))
                        if(recipe.inputs.All(s=>reachable.Contains(s.item))&&(recipe.family=="emergency"||c.buildings.Any(b=>b.family==recipe.family&&b.tech<=tech&&b.cost.All(s=>reachable.Contains(s.item)))))
                            foreach(var output in recipe.outputs)changed|=reachable.Add(output.item);
                } while(changed);
                if(tech<4)Check(c.techs[tech].cost.All(s=>reachable.Contains(s.item)),"Reachable research payment at tech "+tech);
                Check(c.buildings.Where(b=>b.tech<=tech).All(b=>b.cost.All(s=>reachable.Contains(s.item))),"Reachable building capital at tech "+tech);
            }
            var r = new Runner(c, new Scenario());
            r.Execute(new ActionDef { op = "mine", resource = "iron" });
            r.Execute(new ActionDef { op = "wait", seconds = 30 });
            Check(r.sim.world.cargo.Get("iron") == 1 && r.sim.AvailableBuilders == 3, "Drone mining timing / builder reservation");
            r.Execute(new ActionDef { op = "coreCraft", recipe = "emergency_plate" });
            r.Execute(new ActionDef { op = "wait", seconds = 149 });
            Check(r.sim.world.produced.Get("plate") == 0, "Core craft finished early");
            r.Execute(new ActionDef { op = "wait", seconds = 1.25f });
            Check(r.sim.world.produced.Get("plate") == 1, "Core fixed 150-second emergency timing");
            Check(r.sim.ResearchError().Contains("Produce 20"), "Starter stock incorrectly counts for milestones");
            var plan = new Scenario { timeout = 3, actions = new[] { new ActionDef { op = "wait", condition = "cargo", item = "fuel", amount = 1 } } };
            var blocked = new Runner(c, plan).Run();
            Check(!blocked.success && blocked.seconds == 3 && blocked.error.Contains("Timed out"), "Unreachable wait must terminate with diagnostics");
            var build = new Runner(c, new Scenario());
            int plates = build.sim.world.cargo.Get("plate");
            build.Execute(new ActionDef { op = "build", id = "m", building = "miner", x = -18, y = -4 });
            Check(build.sim.world.cargo.Get("plate") == plates - c.Building("miner").cost.First(v => v.item == "plate").amount, "Build payment");
            build.Execute(new ActionDef { op = "wait", condition = "built", id = "m", timeout = 100 });
            Check(build.sim.Complete(build.sim.world.machines.Last()), "Construction completion");
            var parallel = new Runner(c, new Scenario());
            for (int i = 0; i < 5; i++) parallel.Execute(new ActionDef { op = "build", id = "storage" + i, building = "storage", x = 40 + i * 4, y = 20 });
            parallel.Execute(new ActionDef { op = "wait", seconds = 1 });
            Check(parallel.sim.world.machines.Count(m => m.def == "storage" && m.built > 0) == 4, "Four concurrent construction workers");
            var power = new Runner(c, new Scenario());
            int consumerCount = (int)(80 / c.Building("workshop").power) + 1;
            for (int i = 0; i < consumerCount; i++) power.Execute(new ActionDef { op = "build", id = "work" + i, building = "workshop", x = 40 + i * 4, y = 20 });
            power.Execute(new ActionDef { op = "wait", condition = "built", id = "work" + (consumerCount - 1), timeout = 100 });
            power.Execute(new ActionDef { op = "wait", seconds = 1 });
            Check(power.sim.ratio == 0, "Idle enabled machines must cause power cutoff");
            power.Execute(new ActionDef { op = "enable", id = "work" + (consumerCount - 1), enabled = false });
            power.Execute(new ActionDef { op = "wait", seconds = 1 });
            Check(power.sim.ratio == 1, "Paused consumers must release grid demand");
            // Production must stop completely below capacity and resume at nominal speed.
            var machine=power.sim.world.machines.First(m=>m.def=="workshop");
            // Use a catalog-selected workshop recipe so the check survives recipe renames.
            var testRecipe=c.recipes.First(v=>v.family=="workshop");power.sim.SetRecipe(machine,testRecipe.id);machine.input.Add(testRecipe.inputs);
            foreach(var m in power.sim.world.machines)m.enabled=true;
            power.sim.Tick(1);float progress=machine.progress;power.sim.Tick(2);
            Check(machine.progress==progress&&power.sim.ratio==0,"No fractional work during a power outage");
            power.sim.world.machines.Last(m=>m.def=="workshop").enabled=false;
            power.sim.Tick(.25f);Check(power.sim.ratio==1,"Grid restarts at full speed after load shedding");
            var recovery=new Simulation(c);recovery.world.cargo.Add("plate",1000);recovery.world.cargo.Add("wire",1000);
            Func<string,int,int,Machine> ready=(id,x,y)=>{var m=recovery.Place(id,x,y,0);Check(m!=null,recovery.notice);m.built=c.Building(id).seconds;m.paid=true;return m;};
            var coal=ready("miner",-15,19);ready("belt",-13,19);ready("generator",-12,19);
            for(int i=0;i<30;i++)ready("belt",i-15,15);
            float beforeCoal=60/c.Building("miner").rate-.5f;
            for(int i=0;i<(int)(beforeCoal/.25f);i++)recovery.Tick(.25f);
            Check(coal.progress>=beforeCoal-.01f&&recovery.ratio==0,"Coal miner works at full speed on recovery circuit during outage");
            for(int i=0;i<16;i++)recovery.Tick(.25f);
            Check(recovery.generation==240,"Recovery belt delivers coal and starts generator during outage");
            var repeatA = new Runner(c, new Scenario { actions = new[] { new ActionDef { op = "wait", seconds = 10 } } }).Run();
            var repeatB = new Runner(c, new Scenario { actions = new[] { new ActionDef { op = "wait", seconds = 10 } } }).Run();
            Check(repeatA.seconds == repeatB.seconds && repeatA.generatedPUSeconds == repeatB.generatedPUSeconds, "Determinism");
            Console.WriteLine("PASS: drone reservation, recovery timing, milestones, timeout diagnostics, construction payment/completion/concurrency, idle power cutoffs, paused consumers, determinism");
        }
    }
}
