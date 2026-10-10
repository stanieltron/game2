using System;
using System.Collections.Generic;
using System.Linq;
using BootstrapFactory;

namespace FactoryBalance
{
    public class InvestmentAction
    {
        public string op, spec;
        public int amount=1;
        public InvestmentAction() {}
        public InvestmentAction(string op,string spec=null,int amount=1){this.op=op;this.spec=spec;this.amount=amount;}
    }
    public class InvestmentProgram
    {
        public string name="action route";
        public int maxPerType=20, maxActions=400, lookahead=0;
        public double maxSeconds=43200;
        public List<InvestmentAction> actions=new List<InvestmentAction>();
    }
    public partial class FactoryPlanner
    {
        // These are paid from the normal landing stock, not granted at time zero.
        public static readonly string[] ActionBootstrap={"miner:iron","miner:copperOre","miner:carbon","smelter","workshop","generator","miner:silicate"};
        int CountSpec(string spec)=>sim.world.machines.Count(m=>m.def==Def(spec)&&(Resource(spec)==""||m.resource==Resource(spec)));
        Dictionary<string,int> FollowingGoal(InvestmentProgram program,int at)
        {
            var goal=new Dictionary<string,int>();
            for(int i=at;i<program.actions.Count;i++){
                var a=program.actions[i];
                if(a.op=="research"){
                    int t=int.Parse(a.spec);if(t>=sim.data.techs.Length)continue;
                    foreach(var s in sim.data.techs[t].cost)Add(goal,s.item,s.amount);
                    break;
                }
                if(a.op=="supply"){
                    var parts=a.spec.Split(':');int n=int.Parse(parts[1]);
                    var p=parts[0]=="bay"?Simulation.BayPhases[n]:Simulation.FighterPhases[n];
                    foreach(var s in p.cost)Add(goal,s.item,s.amount);
                    if(program.lookahead==0)break;
                }
            }
            return goal;
        }
        void ActionBuild(string spec,int amount,int cap)
        {
            var b=sim.data.Building(Def(spec));
            if(b==null||b.id=="core")throw new Exception("Unknown/unsupported purchase: "+spec);
            if(b.tech>sim.world.tech)throw new Exception("Locked purchase: "+spec);
            if(amount<1||amount>400||CountSpec(spec)+amount>(b.id=="belt"?400:cap))throw new Exception("Machine count bound: "+spec);
            if(b.family=="miner"&&!new[]{"iron","copperOre","carbon","silicate"}.Contains(Resource(spec)))throw new Exception("Miner requires resource");
            for(int n=0;n<amount;n++){
                queue.Add(spec);
                // A purchase returns when funded. Construction can overlap the next action.
                while(queue.Count>0)Step(new Dictionary<string,int>());
            }
        }
        Machine ActionBay()=>sim.world.machines.SingleOrDefault(m=>sim.IsBay(m))??throw new Exception("No Fighter Bay");
        public object RunActions(InvestmentProgram program)
        {
            string error=null;int completed=0;var decisions=new List<object>();
            if(policy==null||policy.reclaim)throw new Exception("Action runner requires explicit reclamation");
            try {
                if(program.maxPerType<1||program.maxPerType>20||program.actions.Count>program.maxActions)throw new Exception("Program bound exceeded");
                activeLanes=20; // Capacity comes from paid belt tiles; no free lane upgrades.
                Begin("Paid common start");ActionBuild("belt",15,20);
                foreach(var spec in ActionBootstrap)ActionBuild(spec,1,20);
                Ensure(Array.Empty<Stack>());Mark();
                for(int at=0;at<program.actions.Count&&!sim.BootstrapComplete;at++){
                    var a=program.actions[at];double start=time,off=powerWait,transportBefore=transportWait;
                    var before=Stock(sim.world.cargo.stacks);Begin(a.op+" "+a.spec);
                    var goal=FollowingGoal(program,at+1);
                    switch(a.op){
                        case "build":ActionBuild(a.spec,a.amount,program.maxPerType);break;
                        case "reclaim":{
                            if(a.amount<1||a.amount>20)throw new Exception("Invalid reclaim amount");
                            var victims=sim.world.machines.Where(m=>m.def==Def(a.spec)&&(Resource(a.spec)==""||m.resource==Resource(a.spec))).OrderByDescending(m=>m.uid).Take(a.amount).ToArray();
                            if(victims.Length!=a.amount||victims.Any(m=>sim.IsBay(m)||m.def=="core"))throw new Exception("Invalid reclaim: "+a.spec);
                            foreach(var m in victims){sim.Remove(m);Event("reclaim",a.spec);}break;
                        }
                        case "research":{
                            int t=int.Parse(a.spec);if(sim.world.tech!=t||sim.world.researching)throw new Exception("Research sequence mismatch");
                            var cost=sim.data.techs[t].cost;
                            while(!sim.world.cargo.Has(cost))Step(Stock(cost));
                            sim.StartResearch();if(!sim.world.researching)throw new Exception(sim.notice);Event("research-start",a.spec);break;
                        }
                        case "awaitResearch":
                            while(sim.world.researching)Step(goal);
                            if(sim.world.tech<int.Parse(a.spec)+1)throw new Exception("Research has not started");break;
                        case "wait":{
                            if(a.amount<1||a.amount>3600)throw new Exception("Wait outside 1..3600 seconds");
                            double until=time+a.amount;while(time<until)Step(goal);break;
                        }
                        case "awaitBuilt":
                            while(sim.world.machines.Any(m=>!sim.IsBay(m)&&!sim.Complete(m)))Step(goal);break;
                        case "supply":{
                            var bay=ActionBay();var parts=a.spec.Split(':');int n=int.Parse(parts[1]);
                            if((parts[0]=="bay"?bay.projectPhase:bay.assemblyPhase)!=n||parts[0]=="fighter"&&!bay.assemblyStarted)throw new Exception("Project phase mismatch");
                            var phase=sim.CurrentPhase(bay);if(phase==null||bay.phasePaid)throw new Exception("Project already supplied");
                            var need=phase.cost.Select(s=>new Stack(s.item,Math.Max(0,s.amount-bay.input.Get(s.item)))).ToArray();
                            while(!sim.world.cargo.Has(need))Step(Stock(need));
                            sim.SupplyProject(bay);Event("phase-supply",a.spec);break;
                        }
                        case "awaitPhase":{
                            var bay=ActionBay();var parts=a.spec.Split(':');int n=int.Parse(parts[1]);
                            while((parts[0]=="bay"?bay.projectPhase:bay.assemblyPhase)<=n)Step(goal);break;
                        }
                        case "fighter":if(!sim.StartFighter(ActionBay()))throw new Exception(sim.notice);break;
                        default:throw new Exception("Unknown action: "+a.op);
                    }
                    completed++;
                    if(policy.trace)decisions.Add(new {index=at,op=a.op,spec=a.spec,amount=a.amount,startSeconds=start,endSeconds=time,waitSeconds=time-start,tech=sim.world.tech,
                        powerWaitMachineSeconds=powerWait-off,transportWaitMachineSeconds=transportWait-transportBefore,cargoBefore=before,cargoAfter=Stock(sim.world.cargo.stacks),
                        generation=sim.generation,demand=sim.demand,counts=sim.world.machines.GroupBy(m=>m.def+(m.resource==""||m.resource==null?"":":"+m.resource)).ToDictionary(g=>g.Key,g=>g.Count())});
                }
                if(!sim.BootstrapComplete)error="Program ended before Fighter completion";
            }catch(Exception e){error=e.Message;}
            return new {success=sim.BootstrapComplete,error,seconds=time,step,completedActions=completed,energyPUHours=energy,capitalWaitSeconds=capitalWait,
                powerOffSeconds=brown,powerWaitMachineSeconds=powerWait,transportWaitMachineSeconds=transportWait,capitalSpent,decisions,events,milestones,
                counts=sim.world.machines.GroupBy(m=>m.def+(m.resource==null||m.resource==""?"":":"+m.resource)).ToDictionary(g=>g.Key,g=>g.Count()),
                cargo=sim.world.cargo,produced=sim.world.produced,statusSeconds};
        }
    }
}
