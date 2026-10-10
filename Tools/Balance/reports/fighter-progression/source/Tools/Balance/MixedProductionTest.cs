using System;
using System.Collections.Generic;
using System.Linq;
using BootstrapFactory;
namespace FactoryBalance {
 public partial class FactoryPlanner {
  public static void VerifyMixed(string source){
   foreach(var test in new[]{new[]{"workshop","structural","steel","plate"},new[]{"smelter","industrialSmelter","iron","steel"}}){
    var p=new FactoryPlanner(Runner.Load(source),new SearchPolicy{mixedRecipes=true,transport=false,reclaim=false},.5f);
    p.sim.world.tech=1;
    foreach(var spec in new[]{test[0],test[1],"generator"}){
     p.sim.world.cargo.Add(p.sim.data.Building(spec).cost);p.queue.Add(spec);p.PlaceAffordable();
     var m=p.sim.world.machines.Last();m.built=p.sim.data.Building(spec).seconds;
    }
    p.sim.world.cargo=new Inventory();p.sim.world.cargo.Add(test[2],100);p.sim.world.cargo.Add("carbon",10);
    p.Step(new Dictionary<string,int>{{test[3],100}});
    foreach(var id in test.Take(2)){
     var m=p.sim.world.machines.Single(x=>x.def==id);
     if(!m.working&&m.input.Total==0&&m.output.Total==0)throw new Exception("Mixed dispatcher left "+id+" idle");
    }
    if(p.sim.world.cargo.Get(test[2])!=98)throw new Exception("Input conservation failed");
   }
   Console.WriteLine("PASS: basic and industrial machines both receive work; inputs conserved");
  }
 }
}
