using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using BootstrapFactory;
using UnityEditor;
using UnityEngine;

// A real economy playthrough: no free materials, instant construction, or tech grants.
// The driver performs the same manual cargo transfers exposed by the game UI.
public static class FactoryCampaignTest
{
    static Simulation sim;static int position,ticks;static string gatheringRaw="";
    static readonly Dictionary<string,Machine> machines=new Dictionary<string,Machine>();
    [MenuItem("Factory/Test Full Campaign")]
    public static void Run()
    {
        sim=new Simulation(JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Resources/catalog.json")));position=0;ticks=0;gatheringRaw="";machines.Clear();
        Build("miner","iron",-18,-4);Build("miner","copperOre",21,-12);Build("miner","carbon",-15,19);Build("miner","silicate",23,18);
        Building("smelter");Building("workshop");Building("generator");
        Ensure("plate",sim.world.cargo.Get("plate")+20);Ensure("wire",sim.world.cargo.Get("wire")+10);
        for(int t=0;t<4;t++)
        {
            var cost=Reserve(sim.data.techs[t].cost);sim.world.cargo.Add(cost.stacks);sim.StartResearch();
            if(!sim.world.researching)throw new Exception("Research failed: "+sim.notice);
            while(sim.world.researching)Step();
        }
        // Cover new construction and drone production as well as the capstone.
        Building("industrialMiner");Building("industrialSmelter");Building("industrialGenerator");
        int drones=sim.world.drones;Ensure("drone",1);if(sim.world.drones!=drones+1)throw new Exception("Drone production failed");
        var bay=sim.Place("fighterPad",60,-35,0);
        if(bay==null)throw new Exception("Bay placement failed");
        for(int phase=0;phase<5;phase++)
        {
            var delivery=Reserve(Simulation.BayPhases[phase].cost);bay.input.Add(delivery.stacks);
            while(bay.projectPhase==phase)Step();
        }
        if(!sim.StartFighter(bay))throw new Exception("Fighter workpiece could not start");
        for(int phase=0;phase<5;phase++)
        {
            var delivery=Reserve(Simulation.FighterPhases[phase].cost);bay.input.Add(delivery.stacks);
            while(bay.assemblyPhase==phase)Step();
        }
        var stock=Reserve(Simulation.ReadyStock);sim.world.cargo.Add(stock.stacks);
        if(!sim.RaidReady)throw new Exception("Campaign cannot reach raid readiness");
        var result=$"PASS: full campaign from exact starter stock to raid-ready Fighter Mk1. Tech {sim.world.tech}; drones {sim.world.drones}; simulated hours {sim.world.time/3600:0.00}; ticks {ticks}. No resource or tech grants.";
        File.WriteAllText("Logs/campaign-test.txt",result);Debug.Log(result);
    }
    static void Step()
    {
        if(ticks%20000==0)File.WriteAllText("Logs/campaign-progress.txt",$"ticks={ticks}; hours={sim.world.time/3600:0.00}; tech={sim.world.tech}; machines={sim.world.machines.Count}; phase={sim.world.machines.FirstOrDefault(m=>m.def=="fighterPad")?.projectPhase}; fighter={sim.world.fighter!=null}");
        if(++ticks>1500000)throw new Exception("Campaign stalled: "+sim.notice);
        foreach(var m in sim.world.machines)
        {
            if(gatheringRaw!="carbon"&&sim.data.Building(m.def).family=="generator"&&m.input.Get("carbon")<2&&sim.world.cargo.Get("carbon")>0){sim.world.cargo.Add("carbon",-1);m.input.Add("carbon",1);}
        }
        sim.Tick(.5f);foreach(var m in sim.world.machines)sim.Collect(m);
    }
    static Inventory Reserve(Stack[] cost)
    {
        var stock=new Inventory();foreach(var s in cost){Ensure(s.item,s.amount);sim.world.cargo.Add(s.item,-s.amount);stock.Add(s.item,s.amount);}return stock;
    }
    static Machine Build(string id,string key,int x,int y)
    {
        var b=sim.data.Building(id);var stock=Reserve(b.cost);sim.world.cargo.Add(stock.stacks);var m=sim.Place(id,x,y,0);
        if(m==null)throw new Exception("Placement failed "+id+": "+sim.notice);machines[key]=m;while(!sim.Complete(m))Step();return m;
    }
    static Machine Building(string family)
    {
        if(machines.TryGetValue(family,out var m))return m;
        if(family=="industrialMiner")return Build("industrialMiner",family,-20,-4);
        var b=sim.data.buildings.First(x=>(x.family==family||x.id==family)&&x.tech<=sim.world.tech);
        int slot=position++;return Build(b.id,family,18+(slot%4)*5,-24+(slot/4)*6);
    }
    static void Ensure(string item,int count)
    {
        if(item=="drone")
        {
            var m=Building("drone");var r=sim.data.Recipe("drone");var stock=Reserve(r.inputs);sim.SetRecipe(m,r.id);m.input.Add(stock.stacks);int before=sim.world.drones;while(sim.world.drones==before)Step();return;
        }
        if(new[]{"iron","copperOre","carbon","silicate"}.Contains(item)){string prior=gatheringRaw;gatheringRaw=item;while(sim.world.cargo.Get(item)<count)Step();gatheringRaw=prior;return;}
        while(sim.world.cargo.Get(item)<count)
        {
            var r=sim.data.recipes.Where(v=>v.family!="emergency"&&v.tech<=sim.world.tech&&v.outputs.Any(s=>s.item==item)).OrderBy(v=>machines.ContainsKey(v.family)?0:1).ThenBy(v=>v.tech).FirstOrDefault();
            if(r==null)throw new Exception("Cannot produce "+item+" at tech "+sim.world.tech);
            // Construct first: its materials may consume some of the requested product.
            var m=Building(r.family);var stock=Reserve(r.inputs);sim.SetRecipe(m,r.id);m.input.Add(stock.stacks);
            int before=sim.world.produced.Get(item);while(sim.world.produced.Get(item)==before)Step();
        }
    }
}
