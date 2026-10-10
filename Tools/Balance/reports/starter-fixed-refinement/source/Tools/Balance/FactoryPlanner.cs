using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BootstrapFactory;

namespace FactoryBalance
{
    // An optimistic manual-logistics benchmark: all materials are conserved and all
    // simulation work is real. It is not an automated belt-layout player.
    public partial class FactoryPlanner
    {
        readonly Simulation sim;
        readonly SearchPolicy policy;
        int activeLanes;
        readonly Dictionary<string,double> capitalSpent=new Dictionary<string,double>();
        readonly Dictionary<string,double> statusSeconds=new Dictionary<string,double>();
        readonly Dictionary<string,double> transferCredit=new Dictionary<string,double>();
        readonly List<object> samples=new List<object>();
        double energy,capitalWait,transportWait,nextSample,queueSince,powerWait;
        string waitingSpec;
        public FactoryPlanner(Catalog catalog, SearchPolicy policy, float step=.5f)
        {sim=new Simulation(catalog);this.policy=policy;this.scale=policy.scale;this.upgrade=policy.upgrade;this.step=step;}
        void AddCredit(string key,double rate,double max){transferCredit[key]=Math.Min(max,(transferCredit.TryGetValue(key,out var value)?value:0)+rate*step);}
        bool Transfer(string family,int amount){if(policy==null||!policy.transport)return true;
            if(!transferCredit.TryGetValue(family,out var available)||available+1e-6<amount){transportWait+=step;return false;}
            transferCredit[family]=available-amount;return true;}
        void CollectOutputs(){
            if(policy==null||!policy.transport){foreach(var m in sim.world.machines)sim.Collect(m);return;}
            var active=sim.world.machines.Where(m=>sim.Complete(m)&&!sim.IsBay(m)).Select(m=>sim.data.Building(m.def).family).Where(f=>f!="miner"&&f!="belt"&&f!="core").Distinct().ToArray();
            var tiles=sim.world.machines.Count(m=>m.def=="belt"&&sim.Complete(m));
            double capacity=Math.Min((activeLanes>0?activeLanes:policy.lanes)*3.0,tiles*3.0/Math.Max(1,active.Length)/policy.tilesPerLane)*sim.ratio;
            foreach(var family in active)AddCredit(family,capacity,Math.Max(3,sim.data.recipes.Where(r=>r.family==family).Select(r=>r.inputs.Sum(x=>x.amount)).DefaultIfEmpty(3).Max()));
            foreach(var m in sim.world.machines){string key="output:"+m.uid;AddCredit(key,3*sim.ratio,3);
                foreach(var stack in m.output.stacks){int n=Math.Min(stack.amount,(int)transferCredit[key]);if(n>0){m.output.Add(stack.item,-n);sim.world.cargo.Add(stack.item,n);transferCredit[key]-=n;}}}
        }
        readonly int scale;
        readonly bool upgrade;
        readonly float step;
        readonly int logisticsTiles;
        readonly List<object> events = new List<object>();
        readonly List<object> milestones = new List<object>();
        readonly List<string> queue = new List<string>();
        readonly Dictionary<string,int> depths = new Dictionary<string,int>();
        readonly Dictionary<string,double> busy = new Dictionary<string,double>();
        double time,brown,lastProgress;
        int position;
        string stage;
        public FactoryPlanner(Catalog catalog, int scale, bool upgrade, float step=.5f, int logisticsTiles=0)
        { sim=new Simulation(catalog);this.scale=scale;this.upgrade=upgrade;this.step=step;this.logisticsTiles=logisticsTiles; }
        static void Add(Dictionary<string,int> d,string k,int n){if(!d.ContainsKey(k))d[k]=0;d[k]+=n;}
        Dictionary<string,int> Stock(IEnumerable<Stack> values)
        { var d=new Dictionary<string,int>();foreach(var s in values)Add(d,s.item,s.amount);return d; }
        int Get(Dictionary<string,int> d,string item)=>d.TryGetValue(item,out var n)?n:0;
        string Resource(string spec)=>spec.Contains(":")?spec.Split(':')[1]:"";
        string Def(string spec)=>spec.Split(':')[0];
        void Event(string kind,string detail){if(policy==null||policy.trace)events.Add(new {time,stage,kind,detail});}
        void Enqueue(string def,int count)
        {
            int have=sim.world.machines.Count(m=>m.def==Def(def)&&(Resource(def)==""||m.resource==Resource(def)))+queue.Count(q=>q==def);
            for(int i=have;i<count;i++)queue.Add(def);
        }
        void Begin(string name){stage=name;Event("stage",name);}
        void Mark(){milestones.Add(new {stage,seconds=time,tech=sim.world.tech,machines=sim.world.machines.Count,brownoutSeconds=brown});}
        void ConfigureLegacy(int tech)
        {
            if(tech==0)
            {
                Enqueue("miner:iron",1);Enqueue("miner:copperOre",1);Enqueue("miner:carbon",1);
                Enqueue("smelter",1);Enqueue("workshop",2);Enqueue("generator",1);Enqueue("miner:silicate",1);
            }
            bool industrialAvailable=upgrade&&tech>=sim.data.Building("industrialGenerator").tech;
            if(tech==1&&sim.data.Building("structural").tech==1)
            { Enqueue("structural",1);Enqueue("electrical",1);Enqueue("mechanical",1); }
            if(tech==1&&!industrialAvailable) { Enqueue("generator",2);Enqueue("miner:carbon",2);Enqueue("smelter",2); }
            if(industrialAvailable)
            {
                Enqueue("industrialMiner:carbon",1);Enqueue("industrialGenerator",1);
                Enqueue("industrialMiner:iron",1);Enqueue("industrialMiner:copperOre",1);Enqueue("industrialSmelter",2);
                if(tech>=2){Enqueue("industrialMiner:carbon",Math.Max(1,scale));Enqueue("industrialGenerator",scale+1+(int)Math.Ceiling(logisticsTiles*4.0/sim.data.Building("industrialGenerator").generation));}
            }
            if(tech>=sim.data.Building("structural").tech)
            {
                if(!industrialAvailable){Enqueue("generator",3);Enqueue("miner:carbon",3);}
                int size=tech<2?1:scale*2;
                Enqueue("structural",size);Enqueue("electrical",size);Enqueue("mechanical",size);
                if(tech>=sim.data.Building("fluid").tech)Enqueue("fluid",scale);
            }
            if(industrialAvailable&&tech>=2)
            {
                Enqueue("industrialMiner:carbon",Math.Max(1,scale));Enqueue("industrialGenerator",scale+1);
                Enqueue("industrialMiner:iron",scale*2);Enqueue("industrialMiner:copperOre",scale);
                Enqueue("industrialMiner:silicate",1);Enqueue("industrialSmelter",scale*3);
                Enqueue("belt",logisticsTiles);
            }
            if(!(upgrade&&tech>=sim.data.Building("industrialGenerator").tech)&&tech>=2)
            {
                // Avoid a large investment in obsolete machines one research before replacement.
                int early=upgrade?1:scale;
                Enqueue("miner:iron",early*3);Enqueue("miner:copperOre",early*2);
                Enqueue("smelter",early*4);Enqueue("workshop",early*2);
                Enqueue("miner:carbon",early*3);Enqueue("generator",early*3);
            }
        }
        void Configure(int tech)
        {
            activeLanes=policy?.laneTargets!=null&&tech<policy.laneTargets.Length?Math.Max(1,policy.laneTargets[tech]):policy?.lanes??1;
            if(policy?.stages==null)ConfigureLegacy(tech);
            else if(tech<policy.stages.Count)foreach(var target in policy.stages[tech]){
                if(sim.data.Building(Def(target.spec)).tech>tech)throw new Exception("Policy requests locked building: "+target.spec);
                Enqueue(target.spec,target.count);
            }
            if(policy!=null&&policy.transport){
                var families=sim.world.machines.Select(m=>sim.data.Building(m.def).family).Concat(queue.Select(q=>sim.data.Building(Def(q)).family)).Where(f=>f!="miner"&&f!="belt"&&f!="core"&&f!="fighterPad").Distinct().Count();
                int before=queue.Count;Enqueue("belt",families*activeLanes*policy.tilesPerLane);
                // Bootstrap needs transport first; later expansion funds generation before adding belt load.
                var added=queue.Skip(before).ToArray();queue.RemoveRange(before,queue.Count-before);int at=tech==0?0:queue.FindIndex(spec=>sim.data.Building(Def(spec)).family=="generator")+1;
                if(tech>0&&at==0)at=queue.Count;
                queue.InsertRange(at,added);
            }
        }
        readonly Dictionary<string,RecipeDef> recipeCache=new Dictionary<string,RecipeDef>();
        readonly Dictionary<string,RecipeDef[]> familyRecipeCache=new Dictionary<string,RecipeDef[]>();
        IEnumerable<KeyValuePair<string,RecipeDef>> ProductionChoices(Dictionary<string,RecipeDef> recipes,string family){
            if(policy==null||!policy.mixedRecipes)return recipes;
            if(!familyRecipeCache.TryGetValue(family,out var alternatives)){
                alternatives=sim.data.recipes.Where(r=>r.family==family&&r.family!="emergency").ToArray();familyRecipeCache[family]=alternatives;
            }
            return alternatives.Where(r=>r.tech<=sim.world.tech).SelectMany(r=>r.outputs.Where(o=>recipes.ContainsKey(o.item)).Select(o=>new KeyValuePair<string,RecipeDef>(o.item,r)));
        }
        string recipeCacheKey="";
        HashSet<string> availableFamilies=new HashSet<string>();
        string[] demandOrder;
        RecipeDef Best(string item)
        {
            if(recipeCache.TryGetValue(item,out var cached))return cached;
            var best=sim.data.recipes.Where(r=>r.family!="emergency"&&r.tech<=sim.world.tech&&availableFamilies.Contains(r.family)&&r.outputs.Any(o=>o.item==item)).OrderBy(r=>r.seconds/r.outputs.First(o=>o.item==item).amount).FirstOrDefault();
            recipeCache[item]=best;return best;
        }
        int Depth(string item)
        {
            if(depths.TryGetValue(item,out var n))return n;
            var r=sim.data.recipes.FirstOrDefault(v=>v.family!="emergency"&&v.outputs.Any(o=>o.item==item));
            n=r==null?0:1+r.inputs.Max(s=>Depth(s.item));depths[item]=n;return n;
        }
        void PlaceAffordable()
        {
            // Limit funded construction to the requested sequence; no material grants.
            if(queue.Count==0){waitingSpec=null;return;}string spec=queue[0];var b=sim.data.Building(Def(spec));
            if(waitingSpec!=spec){waitingSpec=spec;queueSince=time;}
            if(!(b.id=="belt"&&sim.world.beltKits>0)&&!sim.world.cargo.Has(b.cost)){capitalWait+=step;return;}
            int x=0,y=0;bool found=false;
            if(Resource(spec)!="")
            {
                foreach(var deposit in sim.deposits.Where(d=>d.item==Resource(spec)))
                {
                    for(int dx=-deposit.rx;dx<=deposit.rx&&!found;dx++)for(int dy=-deposit.ry;dy<=deposit.ry&&!found;dy++)
                    {x=deposit.x+dx;y=deposit.y+dy;found=sim.Resource(x,y)==Resource(spec)&&sim.PlacementError(b.id,x,y)=="";}
                    if(found)break;
                }
            }
            else
            {
                while(position<500&&!found){int slot=position++;x=35+slot%14*5;y=-80+slot/14*5;found=sim.PlacementError(b.id,x,y)=="";}
            }
            if(!found)throw new Exception("No placement for "+spec);
            var cargoBefore=Stock(sim.world.cargo.stacks);var m=sim.Place(b.id,x,y,0);if(m==null)throw new Exception(sim.notice);
            queue.RemoveAt(0);Event("build",spec);
            if(policy==null||policy.trace)events.Add(new {time,stage,kind="funding",detail=spec,waitSeconds=time-queueSince});
            waitingSpec=null;foreach(var cost in b.cost){if(!capitalSpent.ContainsKey(cost.item))capitalSpent[cost.item]=0;capitalSpent[cost.item]+=Math.Max(0,Get(cargoBefore,cost.item)-sim.world.cargo.Get(cost.item));}
            lastProgress=time;
        }
        Dictionary<string,int> Demand(Dictionary<string,int> root, out Dictionary<string,RecipeDef> recipes)
        {
            var needed=new Dictionary<string,int>(root);recipes=new Dictionary<string,RecipeDef>();
            var future=new Dictionary<string,int>();
            foreach(var m in sim.world.machines)
            {
                var r=sim.data.Recipe(m.recipe);
                if(r!=null&&(m.working||m.input.Has(r.inputs)))foreach(var o in r.outputs)Add(future,o.item,o.amount);
            }
            foreach(var item in demandOrder??(demandOrder=sim.data.items.Select(i=>i.id).OrderByDescending(Depth).ToArray()))
            {
                int deficit=Math.Max(0,Get(needed,item)-sim.world.cargo.Get(item)-Get(future,item));needed[item]=deficit;
                if(deficit==0)continue;
                var r=Best(item);if(r==null)continue;recipes[item]=r;
                int batches=(int)Math.Ceiling(deficit/(double)r.outputs.First(o=>o.item==item).amount);
                foreach(var input in r.inputs)Add(needed,input.item,input.amount*batches);
            }
            return needed;
        }
        void Step(Dictionary<string,int> goal)
        {
            if(time>=(policy?.maxSeconds??172800))throw new Exception("Simulation time limit: "+stage);
            CollectOutputs();
            if(upgrade&&(policy==null||policy.reclaim))
            {
                bool specialists=new[]{"structural","electrical","mechanical"}.All(id=>sim.world.machines.Any(m=>m.def==id&&sim.Complete(m)));
                foreach(var m in sim.world.machines.ToArray())
                {
                    bool replaced=m.def=="workshop"&&specialists;
                    replaced|=m.def=="smelter"&&sim.world.machines.Any(n=>n.def=="industrialSmelter"&&sim.Complete(n));
                    replaced|=m.def=="miner"&&sim.world.machines.Any(n=>n.def=="industrialMiner"&&n.resource==m.resource&&sim.Complete(n));
                    if(replaced){sim.Remove(m);Event("reclaim",m.def+":"+m.resource);}
                }
            }
            PlaceAffordable();
            var root=new Dictionary<string,int>(goal);
            // Produce capital for the next building while other machines work on the goal.
            if(queue.Count>0&&!(queue[0]=="belt"&&sim.world.beltKits>0))foreach(var s in sim.data.Building(Def(queue[0])).cost)Add(root,s.item,s.amount);
            availableFamilies=sim.world.machines.Where(m=>sim.Complete(m)&&!sim.IsBay(m)).Select(m=>sim.data.Building(m.def).family).ToHashSet();
            string cacheKey=sim.world.tech+":"+string.Join(",",availableFamilies.OrderBy(x=>x));
            if(cacheKey!=recipeCacheKey){recipeCache.Clear();recipeCacheKey=cacheKey;}
            var need=Demand(root,out var recipes);
            foreach(var m in sim.world.machines.Where(m=>sim.Complete(m)&&!sim.IsBay(m)))
            {
                var b=sim.data.Building(m.def);
                if(b.family=="generator")
                {
                    // Retire basic generators once enough industrial capacity exists.
                    float industrial=sim.world.machines.Where(v=>v.def=="industrialGenerator"&&sim.Complete(v)).Sum(v=>sim.data.Building(v.def).generation);
                    float load=sim.world.machines.Where(v=>sim.Complete(v)&&!sim.IsBay(v)&&sim.data.Building(v.def).family!="generator").Sum(v=>sim.data.Building(v.def).power)+180;
                    m.enabled=m.def!="generator"||industrial+80<load*1.15f;
                    if(m.enabled&&m.input.Get("carbon")<3&&sim.world.cargo.Get("carbon")>0&&Transfer("generator",1)){sim.world.cargo.Add("carbon",-1);m.input.Add("carbon",1);}continue;
                }
                if(b.family=="miner") { m.enabled=true;continue; }
                if(m.working||m.input.Total>0)continue;
                var candidates=ProductionChoices(recipes,b.family).Where(p=>p.Value.tech<=sim.world.tech&&p.Value.family==b.family&&Get(need,p.Key)>0&&sim.world.cargo.Has(p.Value.inputs))
                    .Where(p=>p.Value.inputs.All(i=>sim.world.cargo.Get(i.item)-Math.Min(sim.world.cargo.Get(i.item),Get(root,i.item))>=i.amount))
                    .OrderByDescending(p=>m.recipe==p.Value.id).ThenByDescending(p=>Depth(p.Key)).ToArray();
                if(candidates.Length==0)continue;
                var chosen=policy!=null&&policy.recipeRule==1?candidates.OrderByDescending(p=>Get(need,p.Key)*p.Value.seconds).First():candidates[0];var r=chosen.Value;
                if(!Transfer(b.family,r.inputs.Sum(x=>x.amount)))continue;
                sim.SetRecipe(m,r.id);sim.world.cargo.Spend(r.inputs);m.input.Add(r.inputs);
                need[chosen.Key]=Math.Max(0,need[chosen.Key]-r.outputs.First(o=>o.item==chosen.Key).amount);
                lastProgress=time;
            }
            foreach(var m in sim.world.machines.Where(m=>m.working)) {if(!busy.ContainsKey(m.def))busy[m.def]=0;busy[m.def]+=step;}
            float supply=80+sim.world.machines.Where(m=>sim.Complete(m)&&m.enabled&&sim.data.Building(m.def).family=="generator"&&(m.fuelSeconds>0||m.input.Get("carbon")>0)).Sum(m=>sim.data.Building(m.def).generation);
            float reserved=sim.world.researching?sim.data.techs[sim.world.tech].power:0;
            reserved+=sim.world.machines.Where(m=>sim.IsBay(m)&&m.enabled).Sum(m=>sim.ProjectPower(m));
            var consumers=sim.world.machines.Where(m=>sim.Complete(m)&&!sim.IsBay(m)&&sim.data.Building(m.def).family!="generator").OrderBy(m=>m.resource=="carbon"?0:sim.data.Building(m.def).family=="belt"?1:2).ThenBy(m=>((int)(time/10)+m.uid)%Math.Max(1,sim.world.machines.Count)).ToArray();
            foreach(var m in consumers){var b=sim.data.Building(m.def);bool wants=b.family=="belt"||b.family=="core"||b.family=="miner"&&m.output.Total<40||m.working||m.input.Total>0;
                m.enabled=wants&&reserved+b.power<=supply;if(m.enabled)reserved+=b.power;else if(wants)powerWait+=step;}
            sim.Tick(step);time+=step;if(sim.ratio<.99f)brown+=step;
            energy+=(sim.demand*sim.ratio+sim.critical)*step/3600;
            foreach(var m in sim.world.machines){string key=m.def+" / "+m.status;if(!statusSeconds.ContainsKey(key))statusSeconds[key]=0;statusSeconds[key]+=step;}
            if(policy!=null&&policy.trace&&time>=nextSample){samples.Add(new {seconds=time,stage,tech=sim.world.tech,ratio=sim.ratio,demand=sim.demand,generation=sim.generation,queue=queue.FirstOrDefault(),shortfall=queue.Count==0?new Dictionary<string,int>():sim.data.Building(Def(queue[0])).cost.ToDictionary(c=>c.item,c=>Math.Max(0,c.amount-sim.world.cargo.Get(c.item))),cargo=Stock(sim.world.cargo.stacks)});nextSample=time+60;}
            if(time-lastProgress>7200)throw new Exception("No production start for two hours: "+stage+" queue="+string.Join(",",queue)+" cargo="+JsonSerializer.Serialize(sim.world.cargo,Runner.Json));
        }
        void Ensure(Stack[] cost, Stack[] future=null)
        {
            var goal=Stock(cost);
            if(future!=null)foreach(var s in future)Add(goal,s.item,s.amount);
            while(!sim.world.cargo.Has(cost)||queue.Count>0||sim.world.machines.Any(m=>!sim.Complete(m)&&!sim.IsBay(m)))Step(goal);
        }
        public object Run()
        {
            string error=null;
            try
            {
                Begin("Bootstrap");Configure(0);Ensure(Array.Empty<Stack>());
                while(sim.world.produced.Get("plate")<20||sim.world.produced.Get("wire")<10)
                {
                    var target=new Dictionary<string,int>();
                    if(sim.world.produced.Get("plate")<20)target["plate"]=sim.world.cargo.Get("plate")+20-sim.world.produced.Get("plate");
                    if(sim.world.produced.Get("wire")<10)target["wire"]=sim.world.cargo.Get("wire")+10-sim.world.produced.Get("wire");
                    Step(target);
                }
                Mark();
                for(int t=0;t<sim.data.techs.Length;t++)
                {
                    Begin(sim.data.techs[t].name);Ensure(sim.data.techs[t].cost);sim.StartResearch();
                    if(!sim.world.researching)throw new Exception(sim.notice);
                    var advance=t==sim.data.techs.Length-1?Stock(Simulation.BayPhases.SelectMany(p=>p.cost).Concat(Simulation.FighterPhases.SelectMany(p=>p.cost))):Stock(sim.data.techs[Math.Min(t+1,sim.data.techs.Length-1)].cost);
                    while(sim.world.researching)Step(advance);Mark();
                    if(t<sim.data.techs.Length-1){Begin("Expand after tech "+sim.world.tech);Configure(sim.world.tech);Ensure(Array.Empty<Stack>());Mark();}
                }
                Begin("Fighter Bay");
                if(policy?.reclaimAtBay!=null)foreach(var target in policy.reclaimAtBay.OrderBy(x=>x.Key)){
                    var surplus=sim.world.machines.Where(m=>m.def==Def(target.Key)&&(Resource(target.Key)==""||m.resource==Resource(target.Key))).OrderBy(m=>m.uid).Skip(Math.Max(0,target.Value)).ToArray();
                    foreach(var machine in surplus){sim.Remove(machine);Event("reclaim",target.Key);}
                }
                Enqueue("fighterPad",1);Ensure(Array.Empty<Stack>());
                var bay=sim.world.machines.Single(m=>sim.IsBay(m));
                for(int i=0;i<Simulation.BayPhases.Length;i++)
                {
                    var phase=Simulation.BayPhases[i];
                    var future=Simulation.BayPhases.Skip(i+1).SelectMany(p=>p.cost).Concat(Simulation.FighterPhases.SelectMany(p=>p.cost)).ToArray();
                    Begin("Bay: "+phase.name);Ensure(phase.cost,future);sim.SupplyProject(bay);int prior=bay.projectPhase;
                    while(bay.projectPhase==prior)Step(Stock(future));Mark();
                }
                if(!sim.StartFighter(bay))throw new Exception(sim.notice);
                for(int i=0;i<Simulation.FighterPhases.Length;i++)
                {
                    var phase=Simulation.FighterPhases[i];var future=Simulation.FighterPhases.Skip(i+1).SelectMany(p=>p.cost).ToArray();
                    Begin("Fighter: "+phase.name);Ensure(phase.cost,future);sim.SupplyProject(bay);int prior=bay.assemblyPhase;
                    while(bay.assemblyPhase==prior)Step(Stock(future));Mark();
                }
            }
            catch(Exception e){error=e.Message;}
            return new {powerOffSeconds=brown,powerWaitMachineSeconds=powerWait,energyPUHours=energy,capitalWaitSeconds=capitalWait,transportWaitMachineSeconds=transportWait,capitalSpent,statusSeconds,samples,success=sim.BootstrapComplete,error,scale,upgrade,step,logisticsTiles,seconds=time,brownoutSeconds=brown,milestones,events,busyMachineSeconds=busy,
                counts=sim.world.machines.GroupBy(m=>m.def).ToDictionary(g=>g.Key,g=>g.Count()),cargo=sim.world.cargo,produced=sim.world.produced};
        }
        public static void Compare(string catalog,string output,float step=.5f)
        {
            Directory.CreateDirectory(output);
            bool failed=false;
            foreach(int scale in new[]{1,2,3,0})
            {
                var p=new FactoryPlanner(Runner.Load(catalog),Math.Max(1,scale),scale>0,step);var r=p.Run();
                var json=JsonSerializer.Serialize(r,Runner.Json);
                File.WriteAllText(Path.Combine(output,"planner-scale-"+scale+".json"),json);
                var view=JsonDocument.Parse(json).RootElement;
                bool success=view.GetProperty("success").GetBoolean();failed|=!success;
                Console.WriteLine("Scale "+scale+": "+(success?"PASS":"FAIL")+"; minutes="+(view.GetProperty("seconds").GetDouble()/60).ToString("F2")+"; "+view.GetProperty("error").ToString());
            }
            File.Copy(catalog,Path.Combine(output,"catalog.snapshot.json"),true);
            if(failed)throw new Exception("A planner policy failed; inspect its report");
        }
        public static void LogisticsCheck(string catalog,string output)
        {
            var planner=new FactoryPlanner(Runner.Load(catalog),2,true,.5f,180);
            var json=JsonSerializer.Serialize(planner.Run(),Runner.Json);
            File.WriteAllText(Path.Combine(output,"planner-logistics-allowance.json"),json);
            var view=JsonDocument.Parse(json).RootElement;
            if(!view.GetProperty("success").GetBoolean())throw new Exception(view.GetProperty("error").ToString());
            Console.WriteLine("180-belt allowance: PASS; minutes="+(view.GetProperty("seconds").GetDouble()/60).ToString("F2"));
        }
    }
}
