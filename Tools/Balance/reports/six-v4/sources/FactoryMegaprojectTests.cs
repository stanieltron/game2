using System;
using System.Linq;
using BootstrapFactory;
using UnityEngine;

public static class FactoryMegaprojectTests
{
    static void Check(bool yes,string text){if(!yes)throw new Exception("V2 FAILED: "+text);Debug.Log("PASS: "+text);}
    static void Advance(Simulation s,float seconds){for(int i=0;i<seconds*20;i++)s.Tick(.05f);}
    public static void Run(Catalog d)
    {
        Check(Simulation.BayPhases.Sum(p=>p.seconds)==522&&Simulation.FighterPhases.Sum(p=>p.seconds)==414,"v2 minimum sequential timings");
        Check(d.techs[3].seconds==135&&d.techs[3].power==60&&d.techs[3].cost.Any(s=>s.item=="drive"&&s.amount==8),"Orbital Operations costs and power");
        Check(d.Recipe("spec_beam").seconds==1.40625f&&d.Recipe("spec_control").seconds==5.625f&&d.Recipe("work_electrical").seconds==30,"expansion profile component rates");
        var s=new Simulation(d);s.world.tech=4;s.world.cargo=new Inventory();var bay=s.Place("fighterPad",40,30,0);
        Check(bay!=null&&!s.Complete(bay)&&!s.StartFighter(bay),"Bay is a phased footprint, not an instant building");
        s.world.cargo.Add("plate",10);s.SupplyProject(bay);Advance(s,2);Check(bay.input.Get("plate")==10&&bay.phaseProgress==0,"partial delivery retained without phase progress");
        s=new Simulation(d,JsonUtility.FromJson<World>(JsonUtility.ToJson(s.world)));bay=s.world.machines.First(m=>m.def=="fighterPad");Check(bay.input.Get("plate")==10,"partial delivery survives reload");
        foreach(var item in d.items)s.world.cargo.Add(item.id,10000);
        var gen=s.Place("industrialGenerator",50,30,0);gen.built=180;gen.input.Add("carbon",10000);
        for(int phase=0;phase<5;phase++)
        {
            var p=s.CurrentPhase(bay);Check(bay.projectPhase==phase,"Bay phases ordered "+phase);s.SupplyProject(bay);
            Advance(s,1);Check(bay.phasePaid&&Math.Abs(s.demand-p.power)<.01f,"phase requests specified power "+phase);
            s=new Simulation(d,JsonUtility.FromJson<World>(JsonUtility.ToJson(s.world)));bay=s.world.machines.First(m=>m.def=="fighterPad");
            Advance(s,p.seconds);Check(bay.projectPhase==phase+1,"phase completes once after load "+phase);
        }
        Check(s.Complete(bay)&&s.StartFighter(bay)&&!s.StartFighter(bay),"commissioned Bay starts one workpiece");
        for(int phase=0;phase<5;phase++){s.SupplyProject(bay);Advance(s,Simulation.FighterPhases[phase].seconds+1);Check(bay.assemblyPhase==phase+1,"Fighter sequential assembly "+phase);}
        Check(s.BootstrapComplete&&s.world.fighter.subsystems.Count==7&&s.world.produced.Get("fighter")==1,"completed Fighter is persistent subsystem state");
        int id=bay.uid;s.DeleteGroup(new[]{bay});Check(s.world.machines.Any(m=>m.uid==id),"permanent Fighter Bay protected from mass deletion");
        Check(s.RaidReady,"v2 readiness includes craft and expanded reserves");
        s.world.cargo.Add("ammo",-s.world.cargo.Get("ammo"));Check(!s.RaidReady,"readiness rejects insufficient ammo");s.world.cargo.Add("ammo",300);
        Check(s.StartFighterService("rearm"),"rearm reserves ammo");Advance(s,31);Check(s.world.fighter.ammo==300&&s.RaidReady,"loaded ammo counts toward readiness");
        s.world.fighter.subsystems[0].health=.5f;s.world.fighter.subsystems[1].health=0;
        var costs=s.RepairRequirements();Check(costs.Any(x=>x.item=="thruster"&&x.amount==3)&&costs.Any(x=>x.item=="frame"&&x.amount==36),"replacement and fractional repair BOM");
        Check(s.StartFighterService("repair"),"repair reserves factory materials");Advance(s,121);Check(s.world.fighter.subsystems.All(x=>x.health==1),"repair restores subsystems");
        var old=new World{tech=4};old.machines.Add(new Machine{uid=1,def="core",paid=true});old.machines.Add(new Machine{uid=2,def="fighterPad",x=40,y=40,paid=true,built=240});old.cargo.Add("fighter",1);old.droneResource="iron";old.droneProgress=12;
        s=new Simulation(d,old);Check(s.BootstrapComplete&&s.world.mining.Count==1&&s.world.mining[0].progress==12,"legacy pad Fighter and mining migrate without reset");
        s=new Simulation(d,JsonUtility.FromJson<World>(JsonUtility.ToJson(s.world)));Check(s.world.mining.Count==1&&s.world.cargo.Get("fighter")==0,"migration is idempotent");
        var blocked=new Simulation(d);blocked.world.tech=4;var b=blocked.Place("fighterPad",40,40,0);b.input.Add(Simulation.BayPhases[0].cost);for(int i=0;i<4;i++)blocked.StartDroneMining("iron");Advance(blocked,5);Check(b.phaseProgress==0,"mining drones cannot build megaprojects");
        blocked.StopDroneMining();Advance(blocked,5);Check(b.phaseProgress>4,"returning drones resumes construction");
        Debug.Log("FACTORY_V2_TESTS_PASSED");
    }
}
