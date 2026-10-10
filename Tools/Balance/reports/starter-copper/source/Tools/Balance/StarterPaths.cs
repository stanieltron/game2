using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BootstrapFactory;
namespace FactoryBalance {
 public partial class FactoryPlanner {
  void StarterSupport(float extra=0){
   int generators=CountSpec("generator"),coal=CountSpec("miner:carbon");
   float demand=sim.world.machines.Sum(m=>sim.data.Building(m.def).power)+20+extra;
   int g=generators,c=coal;
   while(true){c=Math.Max(coal,(int)Math.Ceiling(g*sim.data.Building("generator").fuelRate/sim.data.Building("miner").rate));if(80+g*sim.data.Building("generator").generation>=demand+(c-coal)*sim.data.Building("miner").power)break;g++;}
   for(int i=coal;i<c;i++){Begin("Coal support");ActionBuild("miner:carbon",1,128);Ensure(Array.Empty<Stack>());}
   for(int i=generators;i<g;i++){Begin("Power support");ActionBuild("generator",1,128);Ensure(Array.Empty<Stack>());}
  }
  public object StarterPath(string allocation,bool landingStock,bool replaceAll,bool unlimitedTransport=false){
   // Scenario-owned already constructed start. Do not alter normal game landing rules.
   var original=Stock(sim.world.cargo.stacks);
   foreach(var spec in new[]{"miner:iron","smelter","workshop","miner:copperOre","smelter","workshop"}.Concat(Enumerable.Repeat("belt",15))){
    sim.world.cargo.Add(sim.data.Building(Def(spec)).cost);queue.Add(spec);PlaceAffordable();sim.world.machines.Last().built=sim.data.Building(Def(spec)).seconds;
   }
   sim.world.cargo=new Inventory();if(landingStock)foreach(var s in original)sim.world.cargo.Add(s.Key,s.Value);
   sim.world.beltKits=0;capitalSpent.Clear();events.Clear();activeLanes=20;
   // Manual transfers are legal in the game. Park the already-owned belts until
   // coal power is online: the generic dispatcher prioritizes belts over crafting.
   var parkedBelts=sim.world.machines.Where(m=>m.def=="belt").ToArray();
   foreach(var belt in parkedBelts)sim.world.machines.Remove(belt);sim.Reindex();policy.transport=false;
   var marks=new List<object>();string error=null;int iron=1,copper=1;double expanded=0,research=0;
   try{
    StarterSupport(parkedBelts.Sum(m=>sim.data.Building(m.def).power));
    sim.world.machines.AddRange(parkedBelts);sim.Reindex();policy.transport=!unlimitedTransport;
    marks.Add(new{action="Starting factory fully powered; belts enabled",seconds=time});
    foreach(char resource in allocation){
     StarterSupport(sim.data.Building("miner").power+sim.data.Building("smelter").power+sim.data.Building("workshop").power);
     foreach(var spec in new[]{"miner:"+(resource=='I'?"iron":"copperOre"),"smelter","workshop"}){Begin("Expand "+spec);ActionBuild(spec,1,128);Ensure(Array.Empty<Stack>());}
     if(resource=='I')iron++;else copper++;
     marks.Add(new{action="Basic sets: iron "+iron+", copper "+copper,seconds=time});
    }
    expanded=time;Begin("Research funding");Ensure(sim.data.techs[0].cost);sim.StartResearch();
    var first=Stock(sim.data.Building("industrialMiner").cost);
    while(sim.world.researching)Step(first);research=time;marks.Add(new{action="Industrial research complete",seconds=time});
    int targetIron=replaceAll?iron:1,targetCopper=replaceAll?copper:1;
    // Put improved iron processing online first. This is a documented upgrade policy,
    // not a search over every permutation of industrial purchases.
    var replacements=new List<(string spec,string old)>();
    replacements.Add(("industrialMiner:iron","miner:iron"));replacements.Add(("industrialSmelter","smelter"));replacements.Add(("structural","workshop"));
    replacements.Add(("industrialMiner:copperOre","miner:copperOre"));replacements.Add(("industrialSmelter","smelter"));replacements.Add(("electrical","workshop"));
    for(int i=1;i<targetIron;i++){replacements.Add(("industrialMiner:iron","miner:iron"));replacements.Add(("industrialSmelter","smelter"));replacements.Add(("structural","workshop"));}
    for(int i=1;i<targetCopper;i++){replacements.Add(("industrialMiner:copperOre","miner:copperOre"));replacements.Add(("industrialSmelter","smelter"));replacements.Add(("electrical","workshop"));}
    foreach(var pair in replacements){
     // The old machine is retired on completion, so size for net replacement load.
     StarterSupport(sim.data.Building(Def(pair.spec)).power-sim.data.Building(Def(pair.old)).power);
     Begin("Upgrade "+pair.spec);ActionBuild(pair.spec,1,128);Ensure(Array.Empty<Stack>());
     var old=sim.world.machines.FirstOrDefault(m=>m.def==Def(pair.old)&&(Resource(pair.old)==""||m.resource==Resource(pair.old)));
     if(old!=null)sim.Remove(old);marks.Add(new{action=pair.spec+" completed; basic reclaimed",seconds=time});
    }
   }catch(Exception e){error=e.Message;}
   return new{path=allocation.Length+1,allocation,landingStock,replaceAll,unlimitedTransport,iron,copper,success=error==null,error,seconds=time,expandedSeconds=expanded,researchSeconds=research,marks,capitalSpent,
    counts=sim.world.machines.GroupBy(m=>m.def+(string.IsNullOrEmpty(m.resource)?"":":"+m.resource)).ToDictionary(g=>g.Key,g=>g.Count()),
    powerWaitMachineSeconds=powerWait,energyPUHours=energy,produced=sim.world.produced,events};
  }
 }
 public static class StarterPaths {
  public static void Verify(string output){
   var c=Runner.Load(Path.Combine(output,"catalog.no-silica.json"));
   if(c.buildings.SelectMany(b=>b.cost).Concat(c.techs.SelectMany(t=>t.cost)).Any(s=>s.item=="silicate"||s.item=="refractory"))throw new Exception("Silica cost remains");
   if(c.Building("industrialMiner").rate!=40)throw new Exception("Unexpected rate change");
   var p=new FactoryPlanner(c,new SearchPolicy{reclaim=false,trace=true,maxSeconds=86400},.5f);
   var actual=JsonSerializer.SerializeToElement(p.StarterPath("I",false,true),Runner.Json);
   using(var recorded=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"results.json")))){
    var expected=recorded.RootElement.EnumerateArray().Single(r=>r.GetProperty("allocation").GetString()=="I");
    foreach(var field in expected.EnumerateObject())
     if(!actual.TryGetProperty(field.Name,out var value)||JsonSerializer.Serialize(value)!=JsonSerializer.Serialize(field.Value))throw new Exception("Replay mismatch: "+field.Name);
   }
   if(!actual.GetProperty("success").GetBoolean()||actual.GetProperty("counts").GetProperty("industrialSmelter").GetInt32()!=3)throw new Exception("Incorrect endpoint");
   Console.WriteLine("PASS: full deterministic path 2 replay, industrial endpoint, silica-cost removal, unchanged mining rate");
  }
  public static void Run(string source,string output,bool landingStock,bool replaceAll){
   Directory.CreateDirectory(output);var catalog=Runner.Load(source);
   foreach(var b in catalog.buildings)b.cost=b.cost.Where(s=>s.item!="silicate"&&s.item!="refractory").ToArray();
   foreach(var t in catalog.techs)t.cost=t.cost.Where(s=>s.item!="silicate"&&s.item!="refractory").ToArray();
   string path=Path.Combine(output,"catalog.no-silica.json");File.WriteAllText(path,JsonSerializer.Serialize(catalog,Runner.Json));
   var allocations=new List<string>{""};for(int width=2;width<=10;width++)for(int bits=0;bits<(1<<(width-2));bits++)allocations.Add("I"+string.Concat(Enumerable.Range(0,width-2).Select(i=>(bits&(1<<i))==0?'I':'C')));
   var results=new object[allocations.Count];int done=0;object gate=new object();
   Parallel.For(0,allocations.Count,new ParallelOptions{MaxDegreeOfParallelism=15},i=>{
    var p=new FactoryPlanner(Runner.Load(path),new SearchPolicy{reclaim=false,trace=true,maxSeconds=86400},.5f);
    results[i]=p.StarterPath(allocations[i],landingStock,replaceAll);
    lock(gate){done++;if(done%50==0||done==allocations.Count)Console.WriteLine(done+" / "+allocations.Count+" starter allocations calculated");}
   });
   File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(results,Runner.Json));
  }
 }
}
