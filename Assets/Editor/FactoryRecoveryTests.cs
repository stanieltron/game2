using System;
using BootstrapFactory;
using UnityEngine;

public static class FactoryRecoveryTests
{
    static void Check(bool condition,string name){if(!condition)throw new Exception("RECOVERY FAILED: "+name);Debug.Log("PASS: "+name);}
    static void Advance(Simulation s,float seconds){for(int i=0;i<seconds*20;i++)s.Tick(.05f);}
    public static void Run(Catalog data)
    {
        var s=new Simulation(data);s.world.cargo=new Inventory();s.world.beltKits=0;
        Check(s.StartDroneMining("silicate")&&s.AvailableBuilders==3,"mining reserves one builder without materials");
        Advance(s,29);Check(s.world.cargo.Get("silicate")==0,"manual mining is slow");
        Advance(s,32);Check(s.world.cargo.Get("silicate")==2,"manual drone mines silicate into cargo");
        s.StopDroneMining();Check(s.AvailableBuilders==4,"stopped mining returns drone");
        Check(s.StartCoreCraft("emergency_refractory"),"empty-stock factory can craft refractory from mined silicate");
        Advance(s,20);Check(s.CoreReservedPower==80&&Math.Abs(s.world.coreProgress-20)<.1f,"Core emergency fabrication runs at full speed on its reserved supply");
        s=new Simulation(data,JsonUtility.FromJson<World>(JsonUtility.ToJson(s.world)));Advance(s,131);
        Check(s.world.cargo.Get("refractory")==1&&!s.CoreCrafting,"Core job survives reload and finishes without external power");
        s.StartDroneMining("iron");Advance(s,31);s.StopDroneMining();Check(s.StartCoreCraft("emergency_plate"),"Core uses raw iron directly");Advance(s,151);
        Check(s.world.cargo.Get("plate")==1,"zero-stock recovery produces construction material");
        s.StartDroneMining("copperOre");Advance(s,31);s.StopDroneMining();s.StartCoreCraft("emergency_wire");Advance(s,151);Check(s.world.cargo.Get("wire")==2,"Core recovers copper wire supply");
        s.world.cargo.Add("silicate",2);s.StartCoreCraft("emergency_refractory");s.CancelCoreCraft();s.CancelCoreCraft();Check(s.world.cargo.Get("silicate")==2,"cancel refunds reserved ingredients exactly once");
        Check(!s.StartCoreCraft("work_frame"),"Core cannot bypass advanced production");
        s.world.produced.Add("plate",20);s.world.produced.Add("wire",10);s.world.cargo.Add(data.techs[0].cost);s.StartResearch();Check(s.world.researching,"research starts with the catalog payment");s.StartCoreCraft("emergency_refractory");Advance(s,10);Check(s.world.research==0,"research pauses during Core crafting");
        s.CancelCoreCraft();Advance(s,1);Check(s.world.research>0,"research resumes after Core crafting ends");
        s.StartDroneMining("carbon");Advance(s,12);s=new Simulation(data,JsonUtility.FromJson<World>(JsonUtility.ToJson(s.world)));Advance(s,19);Check(s.world.cargo.Get("carbon")==1&&s.AvailableBuilders==3,"mining assignment and progress survive save/load");
        var powered=new Simulation(data);var generator=powered.Place("generator",40,40,0);generator.built=35;generator.input.Add("carbon",1);powered.world.cargo.Add("silicate",2);powered.StartCoreCraft("emergency_refractory");powered.Tick(.05f);
        Check(powered.generation==240&&powered.CoreReservedPower==80&&powered.ratio==1,"Core reserves 80 PU and leaves surplus power");
        s.StopDroneMining();s.StartDroneMining("iron");s.StartDroneMining("copperOre");s.StartDroneMining("silicate");s.StartDroneMining("iron");
        Check(s.AvailableBuilders==0&&!s.StartDroneMining("carbon"),"multiple assignments cannot exceed owned drones");
        s.RemoveMiningDrone("iron");Check(s.AvailableBuilders==1&&s.world.mining.Count==3,"minus returns one mining drone");
        Debug.Log("FACTORY_RECOVERY_TESTS_PASSED");
    }
}
