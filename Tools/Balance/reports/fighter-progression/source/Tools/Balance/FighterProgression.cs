using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BootstrapFactory;
namespace FactoryBalance {
 public class FighterRoute {
  public string id,allocation="CIII";
  public int[][] widths; // Per tech 1..4: iron, copper, smelters, structural, electrical, mechanical, fluid; optional silicate.
  public bool replaceAll=true;
 }
 public partial class FactoryPlanner {
  void FighterPower(float extra=0){
   float load=sim.world.machines.Sum(m=>sim.data.Building(m.def).power)+240+extra;
   float supply=80+sim.world.machines.Where(m=>sim.data.Building(m.def).family=="generator").Sum(m=>sim.data.Building(m.def).generation);
   int added=Math.Max(0,(int)Math.Ceiling((load-supply)/sim.data.Building("industrialGenerator").generation));
   float fuel=sim.world.machines.Where(m=>sim.data.Building(m.def).family=="generator").Sum(m=>sim.data.Building(m.def).fuelRate)+added*sim.data.Building("industrialGenerator").fuelRate;
   float coal=sim.world.machines.Where(m=>m.resource=="carbon").Sum(m=>sim.data.Building(m.def).rate);
   if(coal<fuel){ActionBuild("industrialMiner:carbon",1,128);Ensure(Array.Empty<Stack>());FighterPower(extra);return;}
   for(int i=0;i<added;i++){ActionBuild("industrialGenerator",1,128);Ensure(Array.Empty<Stack>());}
  }
  public object RunFighterProgression(FighterRoute route){
   var starter=JsonSerializer.SerializeToElement(StarterPath(route.allocation,false,route.replaceAll,true),Runner.Json);
   var stages=new List<object>();string error=null;
   void Record(string label,double from){stages.Add(new{label,start=from,seconds=time,duration=time-from,counts=sim.world.machines.GroupBy(m=>m.def+(string.IsNullOrEmpty(m.resource)?"":":"+m.resource)).ToDictionary(g=>g.Key,g=>g.Count())});}
   try{
    if(!starter.GetProperty("success").GetBoolean())throw new Exception(starter.GetProperty("error").GetString());
    Record("Starter industrial milestone",0);
    for(int tech=1;tech<=sim.data.techs.Length;tech++){
     double from=time;var w=route.widths[Math.Min(tech-1,route.widths.Length-1)];
     string[] specs={"industrialMiner:iron","industrialMiner:copperOre","industrialSmelter","structural","electrical","mechanical","fluid"};
     foreach(var pair in specs.Select((spec,i)=>new{spec,target=w[i]}).Concat(new[]{new{spec="industrialMiner:silicate",target=w.Length>7?w[7]:1}})){
      if(sim.data.Building(Def(pair.spec)).tech>sim.world.tech)continue;
      while(CountSpec(pair.spec)<pair.target){Begin("Expand "+pair.spec);FighterPower(sim.data.Building(Def(pair.spec)).power);ActionBuild(pair.spec,1,128);Ensure(Array.Empty<Stack>());}
     }
     FighterPower();Record("Expand at tech "+tech,from);
     if(tech<sim.data.techs.Length){
      from=time;Begin("Research "+sim.data.techs[tech].name);Ensure(sim.data.techs[tech].cost);sim.StartResearch();
      if(!sim.world.researching)throw new Exception(sim.notice);
      var next=tech+1<sim.data.techs.Length?Stock(sim.data.techs[tech+1].cost):Stock(Simulation.BayPhases[0].cost);
      while(sim.world.researching)Step(next);Record(sim.data.techs[tech].name,from);
     }
    }
    Begin("Fighter Bay purchase");FighterPower(180);ActionBuild("fighterPad",1,128);
    var bay=ActionBay();
    for(int phase=0;phase<Simulation.BayPhases.Length;phase++){
     double from=time;var p=Simulation.BayPhases[phase];Begin("Bay: "+p.name);Ensure(p.cost);sim.SupplyProject(bay);
     var future=phase+1<Simulation.BayPhases.Length?Stock(Simulation.BayPhases[phase+1].cost):Stock(Simulation.FighterPhases[0].cost);
     while(bay.projectPhase==phase)Step(future);Record("Bay: "+p.name,from);
    }
    if(!sim.StartFighter(bay))throw new Exception(sim.notice);
    for(int phase=0;phase<Simulation.FighterPhases.Length;phase++){
     double from=time;var p=Simulation.FighterPhases[phase];Begin("Fighter: "+p.name);Ensure(p.cost);sim.SupplyProject(bay);
     var future=phase+1<Simulation.FighterPhases.Length?Stock(Simulation.FighterPhases[phase+1].cost):new Dictionary<string,int>();
     while(bay.assemblyPhase==phase)Step(future);Record("Fighter: "+p.name,from);
    }
   }catch(Exception e){error=e.Message;}
   return new{route,success=sim.BootstrapComplete,error,seconds=time,starter,stages,capitalSpent,energyPUHours=energy,powerOffSeconds=brown,powerWaitMachineSeconds=powerWait,produced=sim.world.produced,busyMachineSeconds=busy,statusSeconds,events};
  }
 }
 public static class FighterProgression {
  public static void Batch(string file,string output,int workers){
   var rows=JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(file));var result=new object[rows.Length];int done=0;object gate=new object();
   Parallel.For(0,rows.Length,new ParallelOptions{MaxDegreeOfParallelism=workers},i=>{
    var row=rows[i];var route=JsonSerializer.Deserialize<FighterRoute>(row.GetProperty("route").GetRawText(),Runner.Json);
    float dt=row.TryGetProperty("step",out var stepElement)?stepElement.GetSingle():.5f;
    var p=new FactoryPlanner(Runner.Load(row.GetProperty("catalog").GetString()),new SearchPolicy{mixedRecipes=true,transport=false,reclaim=false,trace=row.TryGetProperty("trace",out var trace)&&trace.GetBoolean(),maxSeconds=86400},dt);
    result[i]=new{id=row.GetProperty("id").GetString(),result=p.RunFighterProgression(route)};
    lock(gate){done++;if(done%25==0||done==rows.Length)Console.WriteLine(done+" / "+rows.Length+" Fighter routes calculated");}
   });File.WriteAllText(output,JsonSerializer.Serialize(result,Runner.Json));
  }
 }
}
