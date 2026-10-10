using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BootstrapFactory;
namespace FactoryBalance {
 public partial class FactoryPlanner {
  public object IndustrialStep(string[] order,bool reclaim,bool bankAll){
   var start=new[]{"miner:iron","miner:copperOre","miner:carbon","miner:silicate","smelter","smelter","workshop","workshop","generator","generator"}.Concat(Enumerable.Repeat("belt",20));
   foreach(var spec in start){var b=sim.data.Building(Def(spec));sim.world.cargo.Add(b.cost);queue.Add(spec);PlaceAffordable();var m=sim.world.machines.Last();m.built=b.seconds;}
   sim.world.cargo=new Inventory();sim.world.beltKits=0;capitalSpent.Clear();events.Clear();activeLanes=20;
   var marks=new List<object>();string error=null;
   try{
    var total=sim.data.techs[0].cost.Concat(order.SelectMany(s=>sim.data.Building(Def(s)).cost)).ToArray();
    Begin("Research funding");Ensure(bankAll?Stock(total).Select(s=>new Stack(s.Key,s.Value)).ToArray():sim.data.techs[0].cost);
    marks.Add(new{action="Research paid",seconds=time});sim.StartResearch();
    while(sim.world.researching)Step(Stock(sim.data.Building(Def(order[0])).cost));
    marks.Add(new{action="Research complete",seconds=time});
    foreach(var spec in order){Begin(spec);ActionBuild(spec,1,20);Ensure(Array.Empty<Stack>());
     marks.Add(new{action="Operational: "+spec,seconds=time});
     if(reclaim){string obsolete=spec.StartsWith("industrialMiner:")?"miner:"+Resource(spec):spec=="industrialSmelter"?"smelter":null;
      var old=obsolete==null?null:sim.world.machines.FirstOrDefault(m=>m.def==Def(obsolete)&&(Resource(obsolete)==""||m.resource==Resource(obsolete)));
      if(old!=null)sim.Remove(old);
     }
    }
   }catch(Exception e){error=e.Message;}
   return new{success=error==null,error,seconds=time,order,reclaim,bankAll,marks,capitalSpent,powerWaitMachineSeconds=powerWait,energyPUHours=energy,produced=sim.world.produced,cargo=sim.world.cargo};
  }
 }
 public static class IndustrialStepStudy {
  static IEnumerable<string[]> Orders(string[] values){if(values.Length==0){yield return Array.Empty<string>();yield break;}foreach(var v in values.Distinct()){
   var rest=values.ToList();rest.Remove(v);foreach(var tail in Orders(rest.ToArray()))yield return new[]{v}.Concat(tail).ToArray();}}
  public static void Run(string catalog,string output){
   Directory.CreateDirectory(output);File.Copy(catalog,Path.Combine(output,"catalog.snapshot.json"),true);
   var orders=Orders(new[]{"industrialMiner:iron","industrialMiner:copperOre","industrialSmelter","industrialSmelter","industrialGenerator"}).ToArray();
   var cases=(from order in orders from reclaim in new[]{false,true} select (order,reclaim)).ToArray();var results=new object[cases.Length+1];
   Parallel.For(0,cases.Length,new ParallelOptions{MaxDegreeOfParallelism=15},i=>{var p=new SearchPolicy{reclaim=false,trace=false,maxSeconds=43200};results[i]=new FactoryPlanner(Runner.Load(catalog),p,.5f).IndustrialStep(cases[i].order,cases[i].reclaim,false);});
   results[cases.Length]=new FactoryPlanner(Runner.Load(catalog),new SearchPolicy{reclaim=false,maxSeconds=43200},.5f).IndustrialStep(orders[0],false,true);
   File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,Runner.Json));
   Console.WriteLine(results.Length+" upgrade orders calculated");
  }
 }
}
