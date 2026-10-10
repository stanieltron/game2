using System;
using System.Linq;
using UnityEngine;

namespace BootstrapFactory
{
    public partial class FactoryGame
    {
        Vector2 projectScroll;
        void DrawProject(Machine m,Rect area)
        {
            GUI.BeginGroup(area);float w=area.width;var phase=sim.CurrentPhase(m);
            Label(0,0,w,42,sim.Complete(m)?"FIGHTER BAY / OPERATIONAL":"FIGHTER BAY / MEGAPROJECT",heading,accent);
            if(Btn(new Rect(0,43,w,29),m.enabled?"Pause project / Bay":"Resume project / Bay"))m.enabled=!m.enabled;
            if(phase!=null)
            {
                bool bay=m.projectPhase<5;int index=bay?m.projectPhase:m.assemblyPhase;
                Label(0,83,w,45,$"{(bay?"BAY":"FIGHTER")} PHASE {index+1}/5\n{phase.name}",text,white);
                Label(0,137,w,45,$"{m.phaseProgress:0} / {phase.seconds:0}s  |  {phase.power:0} PU\n"+(bay?"One construction drone required":"Sequential work inside the Bay"),small,sub);
                Bar(new Rect(0,189,w,6),m.phaseProgress/phase.seconds,green);
                if(Btn(new Rect(0,210,w,31),"Deliver available Command Cargo"))sim.SupplyProject(m);
                Label(0,252,w,32,m.phasePaid?"PHASE MATERIALS PAID":"DELIVERY / MISSING MATERIALS",heading,m.phasePaid?green:accent);
                projectScroll=GUI.BeginScrollView(new Rect(0,287,w,Math.Max(70,area.height-348)),projectScroll,new Rect(0,0,w-22,phase.cost.Length*53+30));
                for(int i=0;i<phase.cost.Length;i++)
                {
                    var s=phase.cost[i];int have=m.phasePaid?s.amount:m.input.Get(s.item),need=Math.Max(0,s.amount-have);
                    Label(0,i*53,w-25,21,catalog.Name(s.item),small,white);
                    Label(0,i*53+23,w-25,26,$"Delivered {have}/{s.amount}  |  Missing {need}",small,need>0?accent:green);
                }
                if(phase.cost.Length==0)Label(0,0,w-24,28,"No additional material inputs.",small,green);
                GUI.EndScrollView();
                Label(0,area.height-49,w,45,"Belt deliveries accepted. Completed phases are permanent. Runs while unpaused.",small,sub);
            }
            else if(sim.world.fighter==null)
            {
                Label(0,90,w,100,"Bay commissioned. Start one persistent Fighter, then deliver its subsystems through five assembly phases. Bay idle load: 40 PU.",text,sub);
                if(Btn(new Rect(0,205,w,37),"Start Fighter Mk1 workpiece"))sim.StartFighter(m);
                Label(0,267,w,100,"Sequential assembly: 9.2 minutes with power. Subsystems can be fabricated in parallel. This Bay is a permanent structure.",small,sub);
            }
            else
            {
                var f=sim.world.fighter;
                if(f.bayId!=m.uid){Label(0,93,w,90,"The persistent Fighter is docked in another Bay. Only one Fighter is supported at this stage.",text);GUI.EndGroup();return;}
                Label(0,89,w,49,f.Operational?"FIGHTER MK1 / OPERATIONAL":"FIGHTER MK1 / NEEDS REPAIR",heading,f.Operational?green:accent);
                Label(0,140,w,27,$"Loaded fuel {f.fuel}/30  |  Ammo {f.ammo}/300",small,white);
                for(int i=0;i<f.subsystems.Count;i++)Label(0,177+i*25,w,24,$"{f.subsystems[i].id}   {f.subsystems[i].health*100:0}%",small,sub);
                if(f.service!="")Label(0,362,w,49,$"{f.service.ToUpper()} / {f.serviceProgress:0}s\n{(f.service=="repair"?120:60)} PU service load",small,accent);
                else
                {
                    if(Btn(new Rect(0,365,w/2-4,30),"Refuel / 60 PU"))sim.StartFighterService("refuel");
                    if(Btn(new Rect(w/2+4,365,w/2-4,30),"Rearm / 60 PU"))sim.StartFighterService("rearm");
                    if(Btn(new Rect(0,403,w,30),"Repair subsystems / 120 PU"))sim.StartFighterService("repair");
                }
                var cost=sim.RepairRequirements();Label(0,444,w,76,cost.Length==0?"All subsystems intact. Raid simulation is outside this prototype.":"Repair requirements: "+Cost(cost),small,sub);
            }
            GUI.EndGroup();
        }
    }
}
