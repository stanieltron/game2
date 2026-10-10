using System;
using System.Linq;
using System.Collections.Generic;

namespace BootstrapFactory
{
    [Serializable] public class MiningAssignment { public string resource; public int x,y; public float progress; }
    [Serializable] public class CraftSection { public string id,item; public int quantity=1; public float health=1; }
    [Serializable] public class PersistentFighter
    {
        public int bayId,fuel,ammo; public List<CraftSection> subsystems=new List<CraftSection>();
        public string service=""; public float serviceProgress; public Inventory reserved=new Inventory();
        public bool Operational=>subsystems.Count==7&&subsystems.All(s=>s.health>0);
    }
    public class ProjectPhase
    {
        public string name; public Stack[] cost; public float seconds,power;
        public ProjectPhase(string name,float seconds,float power,params Stack[] cost){this.name=name;this.seconds=seconds;this.power=power;this.cost=cost;}
    }
    public partial class Simulation
    {
        static Stack S(string item,int amount)=>new Stack(item,amount);
        public static readonly ProjectPhase[] BayPhases={
            new ProjectPhase("Foundation & frame",300,20,S("plate",60),S("beam",40),S("frame",20),S("refractory",12)),
            new ProjectPhase("Heavy mechanisms",420,45,S("plate",20),S("frame",16),S("drive",6),S("motor",8),S("actuator",6)),
            new ProjectPhase("Power & thermal",420,80,S("frame",12),S("cooling",4),S("cabinet",4),S("power",4),S("control",4)),
            new ProjectPhase("Service systems",360,70,S("plate",12),S("pump",4),S("bayFuel",2),S("ammoHandling",2),S("control",4)),
            new ProjectPhase("Commissioning",240,120,S("control",4),S("power",2),S("wire",10))};
        public static readonly ProjectPhase[] FighterPhases={
            new ProjectPhase("Skeleton setup",300,120,S("airframe",1)),
            new ProjectPhase("Propulsion & fuel",300,160,S("thruster",2),S("fuelSystem",1)),
            new ProjectPhase("Electrical & control",240,160,S("avionics",1)),
            new ProjectPhase("Weapons & armor",300,160,S("mount",2),S("armor",12)),
            new ProjectPhase("Calibration & pressure test",240,180)};
        public bool IsBay(Machine m)=>m.def=="fighterPad";
        public bool BootstrapComplete=>world.fighter!=null&&world.fighter.Operational&&world.machines.Any(m=>m.uid==world.fighter.bayId&&IsBay(m)&&Complete(m));
        public ProjectPhase CurrentPhase(Machine m)=>!IsBay(m)?null:m.projectPhase<5?BayPhases[m.projectPhase]:m.assemblyStarted&&m.assemblyPhase<5?FighterPhases[m.assemblyPhase]:null;
        public void SupplyProject(Machine m)
        {
            var p=CurrentPhase(m);if(p==null||m.phasePaid)return;
            foreach(var s in p.cost)Transfer(world.cargo,m.input,s.item,Math.Max(0,s.amount-m.input.Get(s.item)),10000);
            notice=m.input.Has(p.cost)?"Phase supplied; resume simulation to continue work":"Delivered available materials. Remaining requirements stay visible.";
        }
        public bool StartFighter(Machine m)
        {
            if(!IsBay(m)||!Complete(m)||world.fighter!=null||world.machines.Any(b=>b.assemblyStarted)){notice="One persistent Fighter only; complete its Bay first";return false;}
            m.assemblyStarted=true;m.assemblyPhase=0;m.phaseProgress=0;notice="Fighter workpiece started. Supply Skeleton setup.";return true;
        }
        bool ProjectAccepts(Machine m,string item){var p=CurrentPhase(m);return p!=null&&!m.phasePaid&&p.cost.Any(s=>s.item==item&&m.input.Get(item)<s.amount);}
        float ProjectPower(Machine m)
        {
            var p=CurrentPhase(m);if(p!=null)return m.phasePaid||m.input.Has(p.cost)?p.power:0;
            if(!Complete(m))return 0;
            return world.fighter!=null&&world.fighter.bayId==m.uid&&world.fighter.service!=""?(world.fighter.service=="repair"?120:60):40;
        }
        void TickProjects(float dt,int workers)
        {
            foreach(var m in world.machines.Where(IsBay))
            {
                if(!m.enabled){m.status="Paused";continue;}var p=CurrentPhase(m);
                if(p==null){m.status=world.fighter?.bayId==m.uid?"Fighter docked":"Bay operational";continue;}
                if(!m.phasePaid)
                {
                    if(!m.input.Has(p.cost)){m.status="Phase needs delivery";continue;}
                    if(!Complete(m)&&workers<=0){m.status="Waiting for construction drone";continue;}
                    m.input.Spend(p.cost);m.investment.Add(p.cost);m.phasePaid=true;
                }
                if(!Complete(m)){if(workers<=0){m.status="Waiting for construction drone";continue;}workers--;}
                m.phaseProgress+=dt*ratio;m.status=ratio<.99f?"Project / low power":p.name;
                if(m.phaseProgress+.0001f<p.seconds)continue;
                m.phaseProgress=0;m.phasePaid=false;
                if(m.projectPhase<5){m.projectPhase++;notice=m.projectPhase==5?"Fighter Bay commissioned. Start the persistent Fighter workpiece.":"Bay phase complete";}
                else if(++m.assemblyPhase==5){CreateFighter(m);notice="Fighter Mk1 complete. Planetary bootstrap complete; prepare sortie reserves.";}
            }
            TickFighterService(dt);
        }
        void CreateFighter(Machine bay,bool recordProduction=true)
        {
            if(world.fighter!=null)return;
            world.fighter=new PersistentFighter{bayId=bay.uid,subsystems=new List<CraftSection>{
                new CraftSection{id="Skeleton",item="airframe"},new CraftSection{id="Left thruster",item="thruster"},new CraftSection{id="Right thruster",item="thruster"},
                new CraftSection{id="Fuel system",item="fuelSystem"},new CraftSection{id="Avionics",item="avionics"},new CraftSection{id="Weapon mounts",item="mount",quantity=2},new CraftSection{id="Armor",item="armor",quantity=12}}};
            if(recordProduction)world.produced.Add("fighter",1);
        }
        public Stack[] RepairRequirements()
        {
            var stock=new Inventory();if(world.fighter==null)return stock.stacks.ToArray();
            foreach(var s in world.fighter.subsystems.Where(s=>s.health<1))
            {
                if(s.health<=0){stock.Add(s.item,s.quantity);continue;}
                var r=data.recipes.FirstOrDefault(r=>r.outputs.Any(o=>o.item==s.item));
                if(r!=null)foreach(var part in r.inputs)stock.Add(part.item,(int)Math.Ceiling(part.amount*s.quantity*(1-s.health)));
            }
            return stock.stacks.ToArray();
        }
        public bool StartFighterService(string service)
        {
            var f=world.fighter;if(f==null||f.service!="")return false;
            Stack[] cost=service=="repair"?RepairRequirements():service=="refuel"?new[]{S("fuel",Math.Min(30-f.fuel,world.cargo.Get("fuel")))}:service=="rearm"?new[]{S("ammo",Math.Min(300-f.ammo,world.cargo.Get("ammo")))}:new Stack[0];
            if(cost.Length==0||cost.Sum(s=>s.amount)<=0){notice="No service needed or no consumables available";return false;}
            if(!world.cargo.Spend(cost)){notice="Missing repair materials: "+string.Join(", ",cost.Where(s=>world.cargo.Get(s.item)<s.amount).Select(s=>(s.amount-world.cargo.Get(s.item))+" "+data.Name(s.item)));return false;}
            f.reserved.Add(cost);f.service=service;f.serviceProgress=0;return true;
        }
        void TickFighterService(float dt)
        {
            var f=world.fighter;if(f==null||f.service=="")return;var bay=world.machines.FirstOrDefault(m=>m.uid==f.bayId);
            if(bay==null||!bay.enabled||!Complete(bay))return;
            f.serviceProgress+=dt*ratio;if(f.serviceProgress<(f.service=="repair"?120:30))return;
            if(f.service=="repair")foreach(var s in f.subsystems)s.health=1;
            if(f.service=="refuel")f.fuel+=f.reserved.Get("fuel");if(f.service=="rearm")f.ammo+=f.reserved.Get("ammo");
            f.reserved=new Inventory();f.service="";f.serviceProgress=0;notice="Fighter service complete";
        }
        void MigrateV2()
        {
            if(world.fighter!=null&&(world.fighter.subsystems==null||world.fighter.subsystems.Count==0))world.fighter=null;
            if(world.mining==null)world.mining=new List<MiningAssignment>();
            if(world.rulesVersion>=2)return;
            if(!string.IsNullOrEmpty(world.droneResource))world.mining.Add(new MiningAssignment{resource=world.droneResource,x=world.droneX,y=world.droneY,progress=world.droneProgress});
            world.droneResource="";
            if(CoreCrafting)CancelCoreCraft(); // Refund old recipes before replacing their semantics.
            foreach(var m in world.machines.Where(IsBay))
            {
                if(m.working){world.cargo.Add(new[]{S("airframe",1),S("thruster",2),S("fuelSystem",1),S("avionics",1),S("mount",2),S("armor",6)});m.working=false;}
                m.recipe="";m.projectPhase=m.paid&&m.built>=240?5:0;
                if(m.projectPhase==0)m.investment.MoveAllTo(world.cargo);
                if(m.output.Get("fighter")>0)m.output.MoveAllTo(world.cargo);
            }
            var dock=world.machines.FirstOrDefault(m=>IsBay(m)&&Complete(m));
            if(dock!=null&&world.cargo.Get("fighter")>0){world.cargo.Add("fighter",-1);CreateFighter(dock,false);dock.assemblyStarted=true;dock.assemblyPhase=5;}
            world.rulesVersion=2;
        }
    }
}
