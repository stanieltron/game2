using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using BootstrapFactory;
namespace FactoryBalance
{
 public class SearchTrial
 {
  public int id,generation; public bool success; public string error;
  public double seconds,energyPUHours,brownoutSeconds,capitalWaitSeconds,rawExtracted,transportWaitMachineSeconds;
  public SearchPolicy policy;
 }
 public static class BatchSearch
 {
  static readonly JsonSerializerOptions Compact=new JsonSerializerOptions(Runner.Json){WriteIndented=false};
  static SearchPolicy Clone(SearchPolicy p)=>JsonSerializer.Deserialize<SearchPolicy>(JsonSerializer.Serialize(p,Compact),Compact);
  static SearchPolicy Seed(Catalog data,int scale)
  {
   var p=new SearchPolicy{scale=scale,stages=new List<List<BuildTarget>>()};
   p.stages.Add(new List<BuildTarget>{new BuildTarget("miner:iron",1),new BuildTarget("miner:copperOre",1),new BuildTarget("miner:carbon",1),new BuildTarget("smelter",1),new BuildTarget("workshop",2),new BuildTarget("generator",1),new BuildTarget("miner:silicate",1)});
   // Optional reinvestment follows a viable starter factory. Raising a starter
   // target in place can otherwise spend the workshop/generator's seed capital.
   p.stages[0].AddRange(new[]{new BuildTarget("smelter",1),new BuildTarget("miner:iron",1),new BuildTarget("smelter",1),new BuildTarget("miner:copperOre",1),new BuildTarget("smelter",1)});
   for(int tech=1;tech<data.techs.Length;tech++){
    var stage=new List<BuildTarget>();
    foreach(var spec in new[]{"industrialMiner:carbon","industrialGenerator","industrialMiner:iron","industrialMiner:copperOre","industrialMiner:silicate","industrialSmelter","structural","electrical","mechanical","fluid"}){
     var b=data.Building(spec.Split(':')[0]);if(b.tech>tech)continue;
     int n=tech==1?1:spec=="industrialSmelter"?scale*3:spec=="industrialGenerator"?scale+1:spec.EndsWith(":iron")||new[]{"structural","electrical","mechanical"}.Contains(spec)?scale*2:spec.EndsWith(":silicate")?1:scale;
     stage.Add(new BuildTarget(spec,n));
    }
    p.stages.Add(stage);
   }
   return p;
  }
  static SearchPolicy Mutate(SearchPolicy source,Random rng,Catalog data,int mutations)
  {
   var p=Clone(source);p.trace=false;
   for(int i=0;i<mutations;i++){
    int op=rng.Next(6);
    if(op==0){p.lanes=rng.Next(1,5);continue;}
    if(op==1){p.recipeRule=rng.Next(2);p.reclaim=rng.Next(5)!=0;continue;}
    int stage=rng.Next(p.stages.Count);var list=p.stages[stage];if(list.Count==0)continue;
    int at=rng.Next(stage==0?7:0,list.Count);
    if(op==2&&stage>0){var value=list[at];list.RemoveAt(at);list.Insert(rng.Next(list.Count+1),value);}
    else if(op==3&&stage>0&&stage<p.stages.Count-1){list[at].count=0;} // Defer this investment until a later stage.
    else {int min=stage==0?1:0;list[at].count=Math.Max(min,Math.Min(12,list[at].count+(rng.Next(2)==0?-1:1)));}
   }
   return p;
  }
  static SearchTrial Evaluate(Catalog data,SearchPolicy policy,int id,int generation,float step)
  {
   var result=JsonSerializer.SerializeToElement(new FactoryPlanner(data,policy,step).Run(),Compact);
   double Number(string key)=>result.GetProperty(key).GetDouble();
   double raw=0;foreach(var item in result.GetProperty("produced").GetProperty("stacks").EnumerateArray())if(new[]{"iron","copperOre","carbon","silicate"}.Contains(item.GetProperty("item").GetString()))raw+=item.GetProperty("amount").GetInt32();
   return new SearchTrial{id=id,generation=generation,policy=policy,success=result.GetProperty("success").GetBoolean(),error=result.GetProperty("error").GetString(),seconds=Number("seconds"),energyPUHours=Number("energyPUHours"),brownoutSeconds=Number("brownoutSeconds"),capitalWaitSeconds=Number("capitalWaitSeconds"),transportWaitMachineSeconds=Number("transportWaitMachineSeconds"),rawExtracted=raw};
  }
  public static void Run(string catalog,string output,int trials,int seed,int workers,float step,int batchSize)
  {
   var data=Runner.Load(catalog);var results=new List<SearchTrial>();var seen=new HashSet<string>();var clock=Stopwatch.StartNew();
   File.Copy(catalog,Path.Combine(output,"catalog.snapshot.json"),true);
   File.WriteAllText(Path.Combine(output,"settings.json"),JsonSerializer.Serialize(new{trials,seed,workers,step,batchSize,model="Game ticks + pooled T1 input trunks; automatic shared cargo; staged-policy search; no player action time",goal="Constructed Fighter"},Runner.Json));
   using(var journal=new StreamWriter(Path.Combine(output,"trials.jsonl"),false)){
    for(int start=0;start<trials;start+=batchSize){
     var parents=results.Where(r=>r.success).OrderBy(r=>r.seconds).Take(12).Select(r=>r.policy).ToArray();
     var batch=new SearchTrial[Math.Min(batchSize,trials-start)];var policies=new SearchPolicy[batch.Length];
     for(int j=0;j<batch.Length;j++){
      int id=start+j;var rng=new Random(unchecked(seed+id*7919));SearchPolicy p;int attempts=0;
      do {var basis=parents.Length>0&&rng.NextDouble()<.75?parents[rng.Next(parents.Length)]:Seed(data,rng.Next(1,4));
       p=id<3?Seed(data,id+1):Mutate(basis,rng,data,rng.Next(1,9));
       if(++attempts>200)throw new Exception("Cannot generate another distinct policy");
      }while(!seen.Add(JsonSerializer.Serialize(new{p.stages,p.lanes,p.reclaim,p.recipeRule,p.transport,p.tilesPerLane},Compact)));
      p.name="trial-"+id;policies[j]=p;
     }
     Parallel.For(0,batch.Length,new ParallelOptions{MaxDegreeOfParallelism=workers},j=>{batch[j]=Evaluate(data,policies[j],start+j,start/batchSize,step);});
     foreach(var trial in batch){results.Add(trial);journal.WriteLine(JsonSerializer.Serialize(trial,Compact));}journal.Flush();
     var best=results.Where(r=>r.success).OrderBy(r=>r.seconds).FirstOrDefault();
     Console.WriteLine($"{results.Count}/{trials} policies; {results.Count(r=>r.success)} completed Fighters; best {(best==null?"none":(best.seconds/60).ToString("F2")+" min")}; wall {clock.Elapsed.TotalSeconds:F1}s");
    }
   }
   var winners=results.Where(r=>r.success).OrderBy(r=>r.seconds).ToArray();
   var frontier=winners.Where(a=>!winners.Any(b=>b.seconds<=a.seconds&&b.energyPUHours<=a.energyPUHours&&b.rawExtracted<=a.rawExtracted&&(b.seconds<a.seconds||b.energyPUHours<a.energyPUHours||b.rawExtracted<a.rawExtracted))).ToArray();
   File.WriteAllText(Path.Combine(output,"pareto.json"),JsonSerializer.Serialize(frontier,Runner.Json));
   File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(new{evaluated=results.Count,successes=winners.Length,failures=results.Count-winners.Length,wallSeconds=clock.Elapsed.TotalSeconds,bestMinutes=winners.Length>0?(double?)(winners[0].seconds/60):null,paretoCount=frontier.Length},Runner.Json));
   if(winners.Length>0){var best=Clone(winners[0].policy);best.trace=true;
    File.WriteAllText(Path.Combine(output,"best-policy.json"),JsonSerializer.Serialize(best,Runner.Json));
    File.WriteAllText(Path.Combine(output,"best-replay.json"),JsonSerializer.Serialize(new FactoryPlanner(data,best,step).Run(),Runner.Json));
   }
  }
  public static void Replay(string catalog,string policyFile,string output,float step)
  {
   var policy=JsonSerializer.Deserialize<SearchPolicy>(File.ReadAllText(policyFile),Runner.Json);policy.trace=true;
   File.WriteAllText(output,JsonSerializer.Serialize(new FactoryPlanner(Runner.Load(catalog),policy,step).Run(),Runner.Json));
  }
 }
}
