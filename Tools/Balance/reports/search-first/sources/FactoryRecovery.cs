using System;
using System.Linq;

namespace BootstrapFactory
{
    public partial class Simulation
    {
        public const float DroneMiningSeconds=30;
        public bool DroneMining=>world.mining.Count>0;
        public bool CoreCrafting=>!string.IsNullOrEmpty(world.coreRecipe);
        public int AvailableBuilders=>Math.Max(0,world.drones-world.mining.Count);
        public float CoreRequestedPower=>CoreCrafting?200:0;
        public float CoreReservedPower=>CoreCrafting?Math.Min(200,Math.Max(80,generation)):0;
        public RecipeDef[] CoreRecipes=>new[]{"emergency_refractory","emergency_plate","emergency_wire"}.Select(id=>data.Recipe(id)).ToArray();
        public float CoreCraftSeconds(RecipeDef recipe)=>recipe.seconds;
        public bool StartDroneMining(string resource)
        {
            if(AvailableBuilders<1){notice="All drones already assigned to mining";return false;}
            var patch=deposits.Where(d=>d.item==resource&&Math.Abs(d.x)<=30&&Math.Abs(d.y)<=30).OrderBy(d=>d.x*d.x+d.y*d.y).FirstOrDefault();
            if(patch==null){notice="No matching deposit in starter-region range";return false;}
            world.mining.Add(new MiningAssignment{resource=resource,x=patch.x,y=patch.y});
            SyncMiningDisplay();notice="One drone assigned. Each drone delivers 2 raw/min to Command Cargo.";return true;
        }
        public void StopDroneMining(){world.mining.Clear();SyncMiningDisplay();notice="Mining drones returned to construction";}
        public void RemoveMiningDrone(string resource){var job=world.mining.LastOrDefault(j=>j.resource==resource);if(job!=null)world.mining.Remove(job);SyncMiningDisplay();}
        void SyncMiningDisplay(){var job=world.mining.FirstOrDefault();world.droneResource=job?.resource??"";world.droneX=job?.x??0;world.droneY=job?.y??0;world.droneProgress=job?.progress??0;}
        void TickDroneMining(float dt)
        {
            foreach(var job in world.mining)
            {
                job.progress+=dt;
                while(job.progress>=DroneMiningSeconds){job.progress-=DroneMiningSeconds;world.cargo.Add(job.resource,1);world.produced.Add(job.resource,1);}
            }
            SyncMiningDisplay();
        }
        public string CoreCraftError(string id)
        {
            if(CoreCrafting)return "Core is already crafting";
            var recipe=CoreRecipes.FirstOrDefault(r=>r.id==id);if(recipe==null)return "Only basic recipes are available at the Core";
            var missing=recipe.inputs.Where(i=>world.cargo.Get(i.item)<i.amount).Select(i=>(i.amount-world.cargo.Get(i.item))+" "+data.Name(i.item));
            return world.cargo.Has(recipe.inputs)?"":"Missing: "+string.Join(", ",missing);
        }
        public bool StartCoreCraft(string id)
        {
            notice=CoreCraftError(id);if(notice!="")return false;
            var recipe=data.Recipe(id);world.cargo.Spend(recipe.inputs);world.coreRecipe=id;world.coreProgress=0;
            notice="Core crafting: "+recipe.name+". Requests 200 PU with priority; surplus power remains available to the factory.";return true;
        }
        public void CancelCoreCraft()
        {
            if(!CoreCrafting)return;var recipe=data.Recipe(world.coreRecipe);if(recipe!=null)world.cargo.Add(recipe.inputs);
            world.coreRecipe="";world.coreProgress=0;notice="Core crafting canceled; ingredients refunded";
        }
        void TickCoreCraft(float dt)
        {
            if(!CoreCrafting)return;var recipe=data.Recipe(world.coreRecipe);if(recipe==null){world.coreRecipe="";return;}
            world.coreProgress+=dt*CoreReservedPower/200f;
            if(world.coreProgress<CoreCraftSeconds(recipe))return;
            world.cargo.Add(recipe.outputs);world.produced.Add(recipe.outputs);world.coreRecipe="";world.coreProgress=0;notice="Core completed "+recipe.name+"; output is in Command Cargo";
        }
    }
}
