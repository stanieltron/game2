using System;
using System.Collections.Generic;
using System.Linq;

namespace BootstrapFactory
{
    [Serializable] public class Stack { public string item; public int amount; public Stack() {} public Stack(string id, int n) { item=id; amount=n; } }
    [Serializable] public class ItemDef { public string id,name,category,cargoClass; public int capacity; }
    [Serializable] public class RecipeDef { public string id,name,family; public Stack[] inputs,outputs; public float seconds,powerOverride; public int tech; }
    [Serializable] public class BuildingDef { public string id,name,family; public Stack[] cost; public float seconds,power,rate,generation,fuelRate; public int tech,size; }
    [Serializable] public class TechDef { public int id; public string name; public Stack[] cost; public float seconds,power; }
    [Serializable] public class Catalog
    {
        // Refund applies to installed capital only; stored cargo remains recoverable.
        public float buildingRefund=1;
        public ItemDef[] items; public RecipeDef[] recipes; public BuildingDef[] buildings; public TechDef[] techs;
        [NonSerialized] Dictionary<string,BuildingDef> buildingIndex;
        [NonSerialized] BuildingDef[] indexedBuildings;
        [NonSerialized] Dictionary<string,RecipeDef> recipeIndex;
        [NonSerialized] RecipeDef[] indexedRecipes;
        public BuildingDef Building(string id) {
            if(buildingIndex==null||indexedBuildings!=buildings){var index=new Dictionary<string,BuildingDef>();foreach(var b in buildings)if(!index.ContainsKey(b.id))index.Add(b.id,b);buildingIndex=index;indexedBuildings=buildings;}
            return buildingIndex[id];
        }
        public RecipeDef Recipe(string id) {
            if(id==null)return null;
            if(recipeIndex==null||indexedRecipes!=recipes){var index=new Dictionary<string,RecipeDef>();foreach(var r in recipes)if(!index.ContainsKey(r.id))index.Add(r.id,r);recipeIndex=index;indexedRecipes=recipes;}
            recipeIndex.TryGetValue(id,out var recipe);return recipe;
        }
        public string Name(string id) { return items.FirstOrDefault(i=>i.id==id)?.name ?? id; }
    }
    [Serializable] public class Inventory
    {
        public List<Stack> stacks = new List<Stack>();
        [NonSerialized] Dictionary<string,Stack> itemIndex;
        [NonSerialized] List<Stack> indexedStacks;
        [NonSerialized] int indexedCount;
        Stack Find(string id) {
            if(itemIndex==null||indexedStacks!=stacks||indexedCount!=stacks.Count){var index=new Dictionary<string,Stack>();foreach(var s in stacks)if(!index.ContainsKey(s.item))index.Add(s.item,s);itemIndex=index;indexedStacks=stacks;indexedCount=stacks.Count;}
            if(id==null)return null;itemIndex.TryGetValue(id,out var value);return value;
        }
        public int Get(string id) { return Find(id)?.amount ?? 0; }
        public int Total { get { return stacks.Sum(s=>s.amount); } }
        public bool Has(IEnumerable<Stack> cost) { return cost.All(s=>Get(s.item)>=s.amount); }
        public void Add(string id,int n) { var s=Find(id); if(s==null){s=new Stack(id,0);stacks.Add(s);itemIndex.Add(id,s);indexedCount=stacks.Count;} s.amount+=n; if(s.amount<0) throw new InvalidOperationException("Negative inventory: "+id); }
        public void Add(IEnumerable<Stack> v,int sign=1) { foreach(var s in v) Add(s.item,s.amount*sign); }
        public bool Spend(IEnumerable<Stack> v) { if(!Has(v))return false;Add(v,-1);return true; }
        public void MoveAllTo(Inventory other) { foreach(var s in stacks) { other.Add(s.item,s.amount);s.amount=0;} }
    }
    [Serializable] public class Machine
    {
        public bool raised; public int outputNext;
        public int projectPhase,assemblyPhase; public bool phasePaid,assemblyStarted; public float phaseProgress;
        public int uid,x,y,direction,tier=1; public int splitInput=0,splitA=2,splitB=3,splitNext; public List<BeltCargo> packets=new List<BeltCargo>(); public string def,resource,recipe,filter="";
        public bool enabled=true,paid,working; public float built,progress,changeover,fuelSeconds,transport;
        public Inventory input=new Inventory(),output=new Inventory(),investment=new Inventory();
        [NonSerialized] public string status="";
    }
    [Serializable] public class World
    {
        public int rulesVersion; public List<MiningAssignment> mining=new List<MiningAssignment>(); public PersistentFighter fighter;
        public int version=1,tech,drones=4,beltKits=30,nextId=1,mapVersion,seed=73421,questStep; public int beltDeliveries; public float time,research;
        public string droneResource="",coreRecipe="";public int droneX,droneY;public float droneProgress,coreProgress;
        public bool researching,won; public Inventory cargo=new Inventory(),produced=new Inventory();
        public List<Machine> machines=new List<Machine>();
    }
    public partial class Simulation
    {
        public readonly Catalog data; public World world;
        public float generation,critical,demand,ratio=1; public string notice="Landing complete. Establish an iron production line.";
        public static readonly int[] DX={1,0,-1,0}, DY={0,1,0,-1};
        readonly Dictionary<int,Machine> grid=new Dictionary<int,Machine>();
        public Simulation(Catalog catalog,World saved=null) { data=catalog;world=saved??new World{mapVersion=2}; if(saved==null){world.cargo.Add("plate",120);world.cargo.Add("wire",50);world.cargo.Add("refractory",24);world.machines.Add(new Machine{uid=world.nextId++,def="core",x=0,y=0,paid=true});} MigrateV2();GenerateDeposits();Reindex(); }
        int Key(int x,int y) { return (x+512)+(y+512)*1024; }
        public void Reindex() { grid.Clear();bridges.Clear();foreach(var m in world.machines){if(m.raised){bridges[Key(m.x,m.y)]=m;continue;}var b=data.Building(m.def);for(int x=m.x;x<m.x+b.size;x++)for(int y=m.y;y<m.y+b.size;y++)grid[Key(x,y)]=m;} }
        public Machine At(int x,int y) { grid.TryGetValue(Key(x,y),out var m); return m; }
        public string Resource(int x,int y) { resourceGrid.TryGetValue(Key(x,y),out var id);return id??""; }
        public bool Complete(Machine m) { return IsBay(m)?m.projectPhase>=5:m.built+0.001f>=data.Building(m.def).seconds; }
        public string PlacementError(string id,int x,int y)
        {
            var b=data.Building(id); if(b.tech>world.tech)return "Research required";
            if(x < -120 || y < -90 || x+b.size>120 || y+b.size>90)return "Outside landing sector";
            for(int a=x;a<x+b.size;a++)for(int c=y;c<y+b.size;c++)if(At(a,c)!=null)return "Occupied tile";
            if(b.family=="miner" && Resource(x,y)=="")return "Place the miner on a resource deposit";
            return "";
        }
        public Machine Place(string id,int x,int y,int direction)
        {
            notice=PlacementError(id,x,y);if(notice!="")return null;
            var b=data.Building(id);var m=new Machine{uid=world.nextId++,def=id,x=x,y=y,direction=direction,resource=b.family=="miner"?Resource(x,y):""};
            m.recipe=data.recipes.FirstOrDefault(r=>r.family==b.family && r.tech<=world.tech)?.id??"";
            if(id=="belt" && world.beltKits>0){world.beltKits--;m.paid=true;}else Pay(m);
            world.machines.Add(m);Reindex();notice=m.paid?"Construction queued":"Blueprint placed: waiting for materials";return m;
        }
        void Pay(Machine m) {var cost=data.Building(m.def).cost;if(world.cargo.Spend(cost)){m.paid=true;m.investment.Add(cost);} }
        public void Remove(Machine m)
        {
            if(m==null||!world.machines.Contains(m)||m.def=="core"||(IsBay(m)&&(m.projectPhase>0||m.assemblyStarted||world.fighter?.bayId==m.uid)))return;
            if(!m.raised&&BridgeAt(m.x,m.y)!=null)Remove(BridgeAt(m.x,m.y));
            m.input.MoveAllTo(world.cargo);m.output.MoveAllTo(world.cargo);
            var r=data.Recipe(m.recipe);if(m.working&&r!=null)world.cargo.Add(r.inputs);
            if(m.def=="belt" && m.paid && m.investment.Get("plate")==0)world.beltKits++;
            float refund=Math.Max(0,Math.Min(1,data.buildingRefund));
            foreach(var investment in m.investment.stacks){world.cargo.Add(investment.item,(int)Math.Floor(investment.amount*refund));investment.amount=0;}
            world.machines.Remove(m);Reindex();notice="Reclaimed building and materials";
        }
        public void SetRecipe(Machine m,string recipe)
        {
            var next=data.Recipe(recipe);var b=data.Building(m.def);
            if(next==null||next.family!=b.family||next.tech>world.tech||m.recipe==recipe)return;
            if(m.working){var old=data.Recipe(m.recipe);if(old!=null)m.input.Add(old.inputs);}
            m.input.MoveAllTo(world.cargo);m.working=false;m.progress=0;m.recipe=recipe;
            m.changeover=new[]{"structural","mechanical","electrical","fluid"}.Contains(b.family)?20:0;
        }
        public void Feed(Machine m)
        {
            var b=data.Building(m.def);if(IsBay(m)){SupplyProject(m);return;}if(b.family=="generator"){Transfer(world.cargo,m.input,"carbon",10,40);return;}
            var r=data.Recipe(m.recipe);if(r==null)return;
            foreach(var s in r.inputs)Transfer(world.cargo,m.input,s.item,Math.Max(0,s.amount*5-m.input.Get(s.item)),120);
        }
        public void Collect(Machine m) {m.output.MoveAllTo(world.cargo);if((data.Building(m.def).family=="storage"||data.Building(m.def).family=="splitter"))m.input.MoveAllTo(world.cargo);notice="Cargo collected";}
        static int Transfer(Inventory from,Inventory to,string id,int amount,int capacity)
        {int n=Math.Max(0,Math.Min(Math.Min(from.Get(id),amount),capacity-to.Total));if(n>0){from.Add(id,-n);to.Add(id,n);}return n;}
        public bool Upgrade(Machine m)
        {
            if(m.def!="belt"||m.tier>=3||world.tech<(m.tier==1?1:3))return false;
            var cost=new[]{new Stack("plate",1),new Stack(m.tier==1?"wire":"bus",1)};
            if(!world.cargo.Spend(cost)){notice="Missing upgrade materials";return false;}
            m.investment.Add(cost);m.tier++;return true;
        }
        public string ResearchError()
        {
            if(world.tech>=4)return "All research complete";
            if(world.researching)return "Research in progress";
            if(world.tech==0&&(world.produced.Get("plate")<20||world.produced.Get("wire")<10))return "Produce 20 Plate and 10 Wire first (starter stock does not count)";
            var missing=data.techs[world.tech].cost.Where(s=>world.cargo.Get(s.item)<s.amount).Select(s=>(s.amount-world.cargo.Get(s.item))+" "+data.Name(s.item));
            return world.cargo.Has(data.techs[world.tech].cost)?"":"Missing: "+string.Join(", ",missing);
        }
        public void StartResearch() {notice=ResearchError();if(notice!="")return;world.cargo.Spend(data.techs[world.tech].cost);world.researching=true;world.research=0;notice="Research started";}
        public static readonly Stack[] ReadyStock={new Stack("fuel",30),new Stack("ammo",300),new Stack("plate",20),new Stack("armor",4),new Stack("motor",2),new Stack("pump",2),new Stack("control",2)};
        public int ReadyAmount(string item)=>world.cargo.Get(item)+(world.fighter==null?0:item=="fuel"?world.fighter.fuel:item=="ammo"?world.fighter.ammo:0);
        public bool RaidReady {get{return world.tech>=4&&BootstrapComplete&&ReadyStock.All(s=>ReadyAmount(s.item)>=s.amount);}}
        public void Tick(float dt)
        {
            if(dt<=0)return;world.time+=dt;
            TickDroneMining(dt);
            int workers=AvailableBuilders;
            foreach(var m in world.machines){if(IsBay(m)||Complete(m))continue;if(!m.paid)Pay(m);if(m.paid&&workers>0){workers--;m.built=Math.Min(data.Building(m.def).seconds,m.built+dt);m.status="Building";}else m.status=m.paid?"Waiting for drone":"Needs materials";}
            var priorityMiners=CriticalCarbonMiners();int projectPowerWorkers=workers;
            generation=80;critical=0;demand=world.researching?data.techs[world.tech].power:0;
            foreach(var m in world.machines)
            {
                var b=data.Building(m.def);if(!m.enabled)continue;if(IsBay(m)){float load=ProjectPower(m);if(!Complete(m)){if(projectPowerWorkers<=0)load=0;else if(load>0)projectPowerWorkers--;}demand+=load;continue;}if(!Complete(m))continue;
                if(b.family=="generator")
                {
                    if(m.fuelSeconds<=0 && m.input.Get("carbon")>0){m.input.Add("carbon",-1);m.fuelSeconds+=60/b.fuelRate;}
                    if(m.fuelSeconds>0){generation+=b.generation;m.fuelSeconds=Math.Max(0,m.fuelSeconds-dt);m.status="Generating";}else m.status="Needs Carbon";
                }
                else if(priorityMiners.Contains(m.uid))critical+=b.power;
                else demand+=b.power;
            }
            float remainder=Math.Max(0,generation-CoreReservedPower);
            ratio=remainder>=critical+demand?1:0;
            float criticalRatio=remainder>=critical?1:0;
            poweredRecovery=criticalRatio>0?priorityMiners:new HashSet<int>();
            foreach(var m in world.machines)
            {
                var b=data.Building(m.def);if(IsBay(m)||!Complete(m))continue;if(!m.enabled){m.status="Paused";continue;}
                float speed=priorityMiners.Contains(m.uid)?criticalRatio:ratio;
                if(b.family=="miner")
                {if(m.output.Total>=40){m.status="Output full";continue;}m.progress+=dt*speed;float cycle=60/b.rate;while(m.progress+0.00001f>=cycle&&m.output.Total<40){m.progress-=cycle;m.output.Add(m.resource,1);world.produced.Add(m.resource,1);}m.status=speed<.99f?"Power off":"Mining";continue;}
                var r=data.Recipe(m.recipe);if(r==null)continue;
                if(m.changeover>0){m.changeover=Math.Max(0,m.changeover-dt*speed);m.status="Reconfiguring";continue;}
                if(!m.working)
                {if(m.output.Total+r.outputs.Sum(s=>s.amount)>120){m.status="Output full";continue;}if(!m.input.Spend(r.inputs)){m.status="Needs inputs";continue;}m.working=true;}
                m.progress+=dt*speed;m.status=speed<.99f?"Power off":"Producing";
                if(m.progress+0.00001f>=r.seconds)
                {m.progress-=r.seconds;m.working=false;foreach(var s in r.outputs){world.produced.Add(s.item,s.amount);if(s.item=="drone")world.drones+=s.amount;else m.output.Add(s.item,s.amount);} }
            }
            Transport(dt);
            TickCoreCraft(dt);
            TickProjects(dt,workers);
            if(world.researching){world.research+=dt*ratio;if(world.research>=data.techs[world.tech].seconds){world.tech++;world.researching=false;world.research=0;notice="Research complete: "+data.techs[world.tech-1].name;}}
        }
        static float Clamp(float n){return Math.Max(0,Math.Min(1,n));}
        bool Accepts(Machine m,string item)
        {
            var b=data.Building(m.def);if(IsBay(m))return ProjectAccepts(m,item);if(b.family=="core")return true;
            if(b.family=="belt")return m.input.Total<3;
            if(b.family=="splitter")return m.input.Total<12;
            if(b.family=="storage")return m.input.Total<40;
            if(b.family=="generator")return item=="carbon"&&m.input.Total<40;
            var r=data.Recipe(m.recipe);return r!=null&&m.input.Total<120&&r.inputs.Any(s=>s.item==item&&m.input.Get(item)<Math.Max(s.amount*5,10));
        }
    }
}
