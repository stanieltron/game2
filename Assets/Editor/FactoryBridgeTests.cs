using System;
using System.Linq;
using BootstrapFactory;
using UnityEngine;

public static class FactoryBridgeTests
{
    static void Check(bool ok,string name){if(!ok)throw new Exception("PORT/BRIDGE FAILED: "+name);Debug.Log("PASS: "+name);}
    static Machine Put(Simulation s,string id,int x,int y,int direction){var m=s.Place(id,x,y,direction);if(m==null)throw new Exception(s.notice);m.built=s.data.Building(id).seconds;m.paid=true;return m;}
    static void RunFor(Simulation s,float seconds){for(int i=0;i<seconds*20;i++)s.Tick(.05f);}
    public static void Run(Catalog d)
    {
        for(int dir=0;dir<4;dir++)
        {
            var s=new Simulation(d);var machine=Put(s,"workshop",40,40,dir);var ports=s.Ports(machine);
            Check(ports.Count(p=>p.input)==6&&ports.Count(p=>p.output)==2,"2x2 machine has six inputs and two outputs / rotation "+dir);
            Check(ports.Where(p=>p.output).All(p=>p.side==dir)&&ports.Select(p=>new Vector2Int(p.NextX,p.NextY)).Distinct().Count()==8,"all edge sockets have distinct adjacent belt cells / rotation "+dir);
            var outlets=ports.Where(p=>p.output).Select(p=>Put(s,"storage",p.NextX,p.NextY,p.side)).ToArray();machine.output.Add("plate",8);RunFor(s,4);
            Check(outlets.All(m=>m.input.Get("plate")==4),"both outputs dispatch fairly / rotation "+dir);
            foreach(var port in ports.Where(p=>p.input))
            {
                var feed=Put(s,"belt",port.NextX,port.NextY,(port.side+2)%4);feed.input.Add("steel",1);
            }
            machine.enabled=false;RunFor(s,1);machine.enabled=true;RunFor(s,3);
            Check(machine.input.Get("steel")+(machine.working?1:0)+machine.output.Get("plate")==6,"all six input sockets deliver usable recipe ingredients / rotation "+dir);
        }
        for(int dir=0;dir<4;dir++)
        {
            var s=new Simulation(d);int crossDir=(dir+1)%4;var center=new Vector2Int(40,40);var along=new Vector2Int(Simulation.DX[dir],Simulation.DY[dir]);var across=new Vector2Int(Simulation.DX[crossDir],Simulation.DY[crossDir]);
            var under=Put(s,"belt",40,40,dir);under.tier=2;under.input.Add("iron",1);s.SyncPackets(under);int uid=under.uid,kits=s.world.beltKits;
            var line=Simulation.BeltLine(center-across,center+across,crossDir%2);s.CommitBeltRoute(line,crossDir);
            var upper=s.BridgeAt(40,40);Check(upper!=null&&upper.raised&&under.uid==uid&&under.direction==dir&&under.tier==2&&under.input.Get("iron")==1,"crossing preserves lower belt and cargo / rotation "+dir);
            Check(s.world.beltKits==kits-3,"bridge charges one additional belt tile plus approaches / rotation "+dir);
            RunFor(s,2);var feed=s.At((center-across).x,(center-across).y);feed.input.Add("copper",3);
            var lowerOut=Put(s,"storage",(center+along).x,(center+along).y,dir);var upperOut=Put(s,"storage",(center+across*2).x,(center+across*2).y,crossDir);
            RunFor(s,5);Check(lowerOut.input.Get("iron")==1&&lowerOut.input.Get("copper")==0&&upperOut.input.Get("copper")==3&&upperOut.input.Get("iron")==0,"crossing lanes conserve cargo without mixing / rotation "+dir);
            int count=s.world.machines.Count;var forbidden=Simulation.BeltLine(center-along*2,center+along*2,dir%2);
            Check(s.CommitBeltRoute(forbidden,dir)==null&&s.world.machines.Count==count,"existing bridge rejects route atomically / rotation "+dir);
            int before=under.direction;s.Rotate(under);Check(under.direction==before,"rotation cannot corrupt bridge axes");
            s=new Simulation(d,JsonUtility.FromJson<World>(JsonUtility.ToJson(s.world)));under=s.At(40,40);upper=s.BridgeAt(40,40);
            Check(upper!=null&&under.direction==dir&&upper.direction==crossDir,"both crossing layers survive reload");
            under.input.Add("iron",1);upper.input.Add("copper",1);s.SyncPackets(under);s.SyncPackets(upper);
            s.DeleteGroup(new[]{under});Check(s.At(40,40)==null&&s.BridgeAt(40,40)==null,"deleting crossing reclaims both lanes");
            Check(s.UndoDelete()&&s.At(40,40).input.Get("iron")==1&&s.BridgeAt(40,40).input.Get("copper")==1,"undo restores both lanes and independent inventories");
        }
        var click=new Simulation(d);var plain=Put(click,"belt",40,40,0);click.CommitBeltRoute(new System.Collections.Generic.List<Vector2Int>{new Vector2Int(40,40)},1);
        Check(plain.direction==1&&click.BridgeAt(40,40)==null,"single click still reorients an ordinary belt");
        Debug.Log("FACTORY_PORT_BRIDGE_TESTS_PASSED");
    }
}
