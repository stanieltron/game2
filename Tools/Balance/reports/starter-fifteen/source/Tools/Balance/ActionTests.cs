using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BootstrapFactory;
namespace FactoryBalance
{
    public static class ActionTests
    {
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        public static void Run(string catalog,string specRoot)
        {
            var data=Runner.Load(catalog);
            var twenty=new InvestmentProgram();twenty.actions.Add(new InvestmentAction("build","smelter",19));
            Check(ActionSearch.Valid(data,twenty),"Initial one plus 19 must be allowed");
            twenty.actions[0].amount=20;Check(!ActionSearch.Valid(data,twenty),"Initial one plus 20 must exceed cap");
            var locked=new InvestmentProgram();locked.actions.Add(new InvestmentAction("build","industrialSmelter"));
            Check(!ActionSearch.Valid(data,locked),"Locked industrial smelter must be rejected");
            var invalid=JsonSerializer.SerializeToElement(ActionSearch.Replay(catalog,locked,.5f,true),Runner.Json);
            Check(invalid.GetProperty("error").GetString().Contains("Locked"),"Runtime must reject locked purchases too");
            var start=JsonSerializer.SerializeToElement(ActionSearch.Replay(catalog,new InvestmentProgram(),.5f,true),Runner.Json);
            var counts=start.GetProperty("counts");
            foreach(var spec in FactoryPlanner.ActionBootstrap)Check(counts.GetProperty(spec).GetInt32()==1,"Common start: "+spec);
            Check(counts.GetProperty("belt").GetInt32()==15,"Common paid/kit belts");
            Check(start.GetProperty("seconds").GetDouble()>0&&start.GetProperty("capitalSpent").EnumerateObject().Sum(v=>v.Value.GetDouble())>0,"Bootstrap must consume time and stock");
            Check(!start.GetProperty("success").GetBoolean(),"An unfinished program is not a successful route");
            var seed=ActionSearch.Seed(data,Path.Combine(specRoot,"reference.policy.json"));
            var a=JsonSerializer.Serialize(ActionSearch.Replay(catalog,seed,.5f,true),Runner.Json);
            var b=JsonSerializer.Serialize(ActionSearch.Replay(catalog,seed,.5f,true),Runner.Json);
            Check(a==b,"Exact repeated replay including action ledger");
            using(var result=JsonDocument.Parse(a))Check(result.RootElement.GetProperty("success").GetBoolean(),"Reference action sequence must construct Fighter");
            var noReclaim=ActionSearch.Seed(data,Path.Combine(specRoot,"minimum.policy.json"));
            int firstResearch=noReclaim.actions.FindIndex(x=>x.op=="awaitResearch");
            noReclaim.actions=noReclaim.actions.Take(firstResearch+1).ToList();
            noReclaim.actions.Add(new InvestmentAction("build","industrialSmelter"));noReclaim.actions.Add(new InvestmentAction("awaitBuilt"));
            var retained=JsonSerializer.SerializeToElement(ActionSearch.Replay(catalog,noReclaim,.5f,true),Runner.Json);
            Check(retained.GetProperty("counts").GetProperty("smelter").GetInt32()==1,"No automatic scrap when industrial smelter completes");
            Check(retained.GetProperty("counts").GetProperty("industrialSmelter").GetInt32()==1,"Upgrade built");
            Console.WriteLine("PASS: paid common start, count cap, locked actions, incomplete failure, explicit reclamation, full Fighter and exact deterministic replay");
        }
    }
}
