using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BootstrapFactory;

namespace FactoryBalance
{
    public static class CampaignTrace
    {
        public static bool PauseIdle, IndustrialOnly;
        static double time, start, brown, ratioSum, prep;
        static int steps;
        static string stage;
        static readonly List<object> stages = new List<object>(), events = new List<object>();
        static readonly Dictionary<string,double> waits = new Dictionary<string,double>();
        static readonly Dictionary<string,int> batches = new Dictionary<string,int>();
        static string activity = "other";
        public static void Reset(bool pause, bool industrialOnly)
        { PauseIdle=pause;IndustrialOnly=industrialOnly; time=start=brown=ratioSum=prep=0;steps=0;stage=null;stages.Clear();events.Clear();waits.Clear();batches.Clear(); }
        public static void Begin(string name, Simulation sim)
        { End(sim);stage=name;start=time;brown=ratioSum=prep=0;steps=0; }
        public static void End(Simulation sim)
        {
            if(stage==null)return;
            stages.Add(new {stage,start,end=time,seconds=time-start,preparationSeconds=prep,brownoutSeconds=brown,meanRatio=steps>0?ratioSum/steps:1,
                tech=sim.world.tech,cargo=sim.world.cargo.stacks.Where(s=>s.amount>0).ToDictionary(s=>s.item,s=>s.amount)});
            stage=null;
        }
        public static void Event(string kind, string detail, Simulation sim)
        { events.Add(new {time,stage,kind,detail,generation=sim.generation,demand=sim.demand,ratio=sim.ratio}); }
        public static void Ready(Simulation sim) { prep=time-start;Event("materials-ready",stage,sim); }
        public static void Activity(string value) { activity=value; }
        public static void Batch(string recipe) { if(!batches.ContainsKey(recipe))batches[recipe]=0;batches[recipe]++; }
        public static void Before(Simulation sim)
        {
            if(IndustrialOnly && sim.world.machines.Any(m=>m.def=="industrialGenerator"&&sim.Complete(m)))
                foreach(var m in sim.world.machines.Where(m=>m.def=="generator"))m.enabled=false;
            if(!PauseIdle)return;
            foreach(var m in sim.world.machines)
            {
                var family=sim.data.Building(m.def).family;
                if(sim.IsBay(m)||family=="miner"||family=="generator"||family=="core")continue;
                var r=sim.data.Recipe(m.recipe);
                m.enabled=r!=null&&(m.working||m.input.Has(r.inputs));
            }
        }
        public static void After(Simulation sim)
        {
            time+=.5;steps++;ratioSum+=sim.ratio;if(sim.ratio<.999f)brown+=.5;
            if(!waits.ContainsKey(activity))waits[activity]=0;waits[activity]+=.5;
        }
        public static void Save(string path, Simulation sim)
        {
            End(sim);
            File.WriteAllText(path,JsonSerializer.Serialize(new {policy=IndustrialOnly?"retire-basic-generator":PauseIdle?"pause-idle":"baseline",seconds=time,fighterConstructed=sim.BootstrapComplete,
                stages,events,waits,batches,produced=sim.world.produced.stacks.Where(s=>s.amount>0).ToDictionary(s=>s.item,s=>s.amount),
                machines=sim.world.machines.Select(m=>new {m.def,m.x,m.y,m.enabled,m.recipe,m.input,m.output}),
                cargo=sim.world.cargo,sim.world.drones},Runner.Json));
        }
    }
}
