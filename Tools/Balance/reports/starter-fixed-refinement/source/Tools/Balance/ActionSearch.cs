using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BootstrapFactory;

namespace FactoryBalance
{
    public class InvestmentTrial
    {
        public int id,parent=-1;public bool success;public double seconds,energyPUHours,powerWaitMachineSeconds,transportWaitMachineSeconds;
        public string error;public InvestmentProgram program;
    }
    public static class ActionSearch
    {
        static readonly JsonSerializerOptions Compact=new JsonSerializerOptions(Runner.Json){WriteIndented=false};
        static T Clone<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,Compact),Compact);
        static void Save(string path,object value){File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,Runner.Json));File.Move(path+".tmp",path,true);}
        public static object Replay(string catalog,InvestmentProgram program,float step,bool trace)
        {
            var policy=new SearchPolicy{trace=trace,reclaim=false,transport=true,lanes=20,maxSeconds=program.maxSeconds};
            return new FactoryPlanner(Runner.Load(catalog),policy,step).RunActions(program);
        }
        public static void ReplayFile(string catalog,string program,string output,float step)
        {Save(output,Replay(catalog,JsonSerializer.Deserialize<InvestmentProgram>(File.ReadAllText(program),Runner.Json),step,true));}
        static string Def(string spec)=>spec.Split(':')[0];
        public static InvestmentProgram Seed(Catalog data,string path)
        {
            var old=JsonSerializer.Deserialize<SearchPolicy>(File.ReadAllText(path),Runner.Json);
            var p=new InvestmentProgram{name=old.name+" explicit seed",lookahead=1};
            var counts=FactoryPlanner.ActionBootstrap.ToDictionary(s=>s,s=>1);counts["belt"]=15;
            void Build(string spec,int target){int have=counts.TryGetValue(spec,out var v)?v:0;for(int i=have;i<target;i++)p.actions.Add(new InvestmentAction("build",spec));counts[spec]=Math.Max(have,target);}
            for(int t=0;t<data.techs.Length;t++){
                if(t<old.stages.Count)foreach(var b in old.stages[t])Build(b.spec,b.count);
                int families=counts.Where(k=>k.Value>0).Select(k=>data.Building(Def(k.Key)).family).Where(f=>f!="miner"&&f!="belt"&&f!="core").Distinct().Count();
                Build("belt",families*(old.laneTargets?[Math.Min(t,old.laneTargets.Length-1)]??1)*5);
                p.actions.Add(new InvestmentAction("awaitBuilt"));
                if(t>0&&old.reclaim){
                    foreach(var key in counts.Keys.ToArray()){
                        bool remove=key=="workshop"&&new[]{"structural","electrical","mechanical"}.All(k=>counts.ContainsKey(k))||key=="smelter"&&counts.ContainsKey("industrialSmelter")||key.StartsWith("miner:")&&counts.ContainsKey("industrialMiner:"+key.Split(':')[1]);
                        if(remove&&counts[key]>0){p.actions.Add(new InvestmentAction("reclaim",key,counts[key]));counts[key]=0;}
                    }
                }
                p.actions.Add(new InvestmentAction("research",t.ToString()));p.actions.Add(new InvestmentAction("awaitResearch",t.ToString()));
            }
            p.actions.Add(new InvestmentAction("build","fighterPad"));
            for(int n=0;n<Simulation.BayPhases.Length;n++){p.actions.Add(new InvestmentAction("supply","bay:"+n));p.actions.Add(new InvestmentAction("awaitPhase","bay:"+n));}
            p.actions.Add(new InvestmentAction("fighter"));
            for(int n=0;n<Simulation.FighterPhases.Length;n++){p.actions.Add(new InvestmentAction("supply","fighter:"+n));p.actions.Add(new InvestmentAction("awaitPhase","fighter:"+n));}
            return p;
        }
        static string[] Specs(Catalog data,int tech)=>data.buildings.Where(b=>b.tech<=tech&&new[]{"miner","smelter","industrialSmelter","workshop","generator","structural","electrical","mechanical","fluid","belt","drone"}.Contains(b.family)).SelectMany(b=>b.family=="miner"?new[]{"iron","copperOre","carbon","silicate"}.Select(r=>b.id+":"+r):new[]{b.id}).ToArray();
        public static bool Valid(Catalog data,InvestmentProgram p)
        {
            if(p.actions.Count>p.maxActions)return false;
            var counts=FactoryPlanner.ActionBootstrap.ToDictionary(s=>s,s=>1);counts["belt"]=15;int tech=0;
            foreach(var a in p.actions){
                if(a.op=="awaitResearch")tech=int.Parse(a.spec)+1;
                if(a.op!="build"&&a.op!="reclaim")continue;
                var b=data.Building(Def(a.spec));if(b==null||b.tech>tech||a.amount<1)return false;
                int count=counts.TryGetValue(a.spec,out var n)?n:0;
                count+=a.op=="build"?a.amount:-a.amount;
                if(count<0||count>(b.id=="belt"?400:b.id=="fighterPad"?1:p.maxPerType))return false;
                counts[a.spec]=count;
            }
            return true;
        }
        public static InvestmentProgram Mutate(Catalog data,InvestmentProgram parent,Random rng)
        {
            var p=Clone(parent);int op=rng.Next(7);
            int at=rng.Next(p.actions.Count+1);int tech=p.actions.Take(at).Where(a=>a.op=="awaitResearch").Select(a=>int.Parse(a.spec)+1).DefaultIfEmpty(0).Max();
            var choices=Specs(data,tech);var spec=choices[rng.Next(choices.Length)];
            var editable=Enumerable.Range(0,p.actions.Count).Where(i=>p.actions[i].op=="build"&&p.actions[i].spec!="fighterPad"||p.actions[i].op=="reclaim"||p.actions[i].op=="wait").ToArray();
            switch(op){
                case 0:case 1:{
                    // Large and small investments are both explored; every unit is paid separately.
                    int amount=op==0?1:new[]{2,3,5,10,19}[rng.Next(5)];
                    p.actions.Insert(at,new InvestmentAction("build",spec,amount));break;
                }
                case 2:if(editable.Length>0)p.actions.RemoveAt(editable[rng.Next(editable.Length)]);break;
                case 3:if(editable.Length>0){int from=editable[rng.Next(editable.Length)];var a=p.actions[from];p.actions.RemoveAt(from);p.actions.Insert(Math.Min(at,p.actions.Count),a);}break;
                case 4:p.actions.Insert(at,new InvestmentAction("reclaim",spec));break;
                case 5:p.actions.Insert(at,new InvestmentAction("wait",null,new[]{30,120,600}[rng.Next(3)]));break;
                case 6:if(editable.Length>0){var a=p.actions[editable[rng.Next(editable.Length)]];if(a.op=="build")a.amount=new[]{1,2,3,5,10,20}[rng.Next(6)];else p.lookahead=1-p.lookahead;}break;
            }
            return p;
        }
        public static void Run(string catalog,string specRoot,string output,string progress,int trials,int seed,int workers,float step,int batchSize)
        {
            if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new Exception("Choose an empty output directory");
            Directory.CreateDirectory(output);var data=Runner.Load(catalog);File.Copy(catalog,Path.Combine(output,"catalog.snapshot.json"));
            var seeds=new[]{Seed(data,Path.Combine(specRoot,"minimum.policy.json")),Seed(data,Path.Combine(specRoot,"reference.policy.json"))};
            Save(Path.Combine(output,"settings.json"),new{trials,seed,workers,step,batchSize,maxPerType=20,maxActions=400,maxSeconds=43200,
                method="Deterministic population search over explicit action sequences; not exhaustive or globally optimal",bootstrap=FactoryPlanner.ActionBootstrap,
                assumptions="Paid common start plus 15 belts; no construction effort time; pooled T1 belt capacity; automatic recipe feeding and binary power dispatch; explicit reclaim; no emergency drone crafting",sourceCatalog=catalog});
            var rows=new List<InvestmentTrial>();var seen=new HashSet<string>();var clock=Stopwatch.StartNew();object gate=new object();int done=0;
            void Progress(string phase){Save(progress,new{phase,completed=done,planned=trials,elapsedSeconds=clock.Elapsed.TotalSeconds,workers,step,rows=rows.OrderBy(r=>r.id).Select(r=>new{r.id,r.parent,r.success,r.seconds,r.error,r.energyPUHours,r.powerWaitMachineSeconds,r.transportWaitMachineSeconds,actions=r.program.actions.Count})});}
            using(var journal=new StreamWriter(Path.Combine(output,"trials.jsonl"))){
                Progress("running");
                for(int start=0;start<trials;start+=batchSize){
                    var parents=rows.Where(r=>r.success).OrderBy(r=>r.seconds).ThenBy(r=>r.id).Take(12).ToArray();
                    var batch=new InvestmentTrial[Math.Min(batchSize,trials-start)];
                    for(int j=0;j<batch.Length;j++){
                        int id=start+j;var rng=new Random(unchecked(seed+id*7919));InvestmentProgram p=null;int parentId=-1;
                        if(id<seeds.Length)p=Clone(seeds[id]);
                        else for(int attempt=0;attempt<1000;attempt++){
                            var parent=parents.Length>0&&rng.NextDouble()<.85?parents[rng.Next(parents.Length)]:null;
                            var basis=parent?.program??seeds[rng.Next(seeds.Length)];var candidate=Mutate(data,basis,rng);
                            if(!Valid(data,candidate)||seen.Contains(JsonSerializer.Serialize(candidate.actions,Compact)+candidate.lookahead))continue;
                            p=candidate;parentId=parent?.id??-1;break;
                        }
                        if(p==null)throw new Exception("Cannot generate a novel legal route");
                        seen.Add(JsonSerializer.Serialize(p.actions,Compact)+p.lookahead);p.name="route-"+id;
                        batch[j]=new InvestmentTrial{id=id,parent=parentId,program=p};
                    }
                    Parallel.ForEach(batch,new ParallelOptions{MaxDegreeOfParallelism=workers},row=>{
                        var result=JsonSerializer.SerializeToElement(Replay(catalog,row.program,step,false),Compact);
                        row.success=result.GetProperty("success").GetBoolean();row.seconds=result.GetProperty("seconds").GetDouble();row.error=result.GetProperty("error").GetString();
                        row.energyPUHours=result.GetProperty("energyPUHours").GetDouble();row.powerWaitMachineSeconds=result.GetProperty("powerWaitMachineSeconds").GetDouble();row.transportWaitMachineSeconds=result.GetProperty("transportWaitMachineSeconds").GetDouble();
                        lock(gate){rows.Add(row);journal.WriteLine(JsonSerializer.Serialize(row,Compact));journal.Flush();done++;if(done%10==0||done==trials){Progress("running");Console.WriteLine(done+" / "+trials+" calculated");}}
                    });
                    // Barrier makes parent selection independent of worker completion order.
                }
            }
            var successful=rows.Where(r=>r.success).OrderBy(r=>r.seconds).ThenBy(r=>r.id).ToArray();
            foreach(var pair in new[]{("fastest",successful.FirstOrDefault()),("slowest",successful.LastOrDefault()),("minimum-seed",rows.First(r=>r.id==0))})if(pair.Item2!=null){
                Save(Path.Combine(output,pair.Item1+".program.json"),pair.Item2.program);
                Save(Path.Combine(output,pair.Item1+".replay.json"),Replay(catalog,pair.Item2.program,step,true));
            }
            Progress("complete");Save(Path.Combine(output,"results.json"),rows.OrderBy(r=>r.id));
        }
    }
}
