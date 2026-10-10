using System;
using System.Linq;
using BootstrapFactory;
using UnityEngine;

public static class FactoryLogisticsTests
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("LOGISTICS FAILED: "+message);Debug.Log("PASS: "+message);}
    static Machine Put(Simulation s,string id,int x,int y,int direction=0){var m=s.Place(id,x,y,direction);if(m==null)throw new Exception(s.notice);m.paid=true;m.built=s.data.Building(id).seconds;return m;}
    static void RunFor(Simulation s,float seconds){for(int i=0;i<seconds*20;i++)s.Tick(.05f);}
    public static void Run(Catalog d)
    {
        for(int rotation=0;rotation<4;rotation++)
        {
            var s=new Simulation(d);var splitter=Put(s,"splitter",40,40,rotation);
            Check(s.Ports(splitter).Count==6,"six splitter ports at rotation "+rotation);
            var input=s.Ports(splitter).Single(p=>p.input);var feeder=Put(s,"belt",input.NextX,input.NextY,(input.side+2)%4);feeder.input.Add("iron",1);
            var outlets=s.Ports(splitter).Where(p=>p.output).Select(p=>Put(s,"storage",p.NextX,p.NextY,p.side)).ToArray();
            splitter.input.Add("iron",7);RunFor(s,4);
            Check(outlets.Sum(m=>m.input.Get("iron"))==8&&outlets.All(m=>m.input.Get("iron")==4),"rotated splitter accepts correct input and alternates equally "+rotation);
        }
        for(int rotation=0;rotation<4;rotation++)
        {
            var drop=new Simulation(d);var source=Put(drop,"storage",40,40,rotation);
            var cross=Put(drop,"belt",40+Simulation.DX[rotation],40+Simulation.DY[rotation],(rotation+1)%4);source.input.Add("iron",1);RunFor(drop,1);
            Check(cross.input.Get("iron")==1,"machine output side-drops onto transverse belt "+rotation);
            var feed=new Simulation(d);var target=Put(feed,"storage",40,40,rotation);
            var input=Put(feed,"belt",40-Simulation.DX[rotation],40-Simulation.DY[rotation],(rotation+1)%4);input.input.Add("iron",1);RunFor(feed,1);
            Check(target.input.Total==0&&input.input.Total==1,"machine input rejects transverse belt "+rotation);
            input.direction=rotation;RunFor(feed,1);Check(target.input.Get("iron")==1,"machine input accepts straight belt "+rotation);
        }
        // All 120 distinct input/output assignments must retain exactly their selected roles.
        var config=new Simulation(d);var sp=Put(config,"splitter",40,40);
        for(int i=0;i<6;i++)for(int a=0;a<6;a++)for(int b=0;b<6;b++)if(i!=a&&i!=b&&a!=b){Check(config.ConfigureSplitter(sp,i,a,b),"valid port assignment");Check(config.Ports(sp).Count(p=>p.input)==1&&config.Ports(sp).Count(p=>p.output)==2,"one input two outputs");}
        Check(!config.ConfigureSplitter(sp,1,1,3),"conflicting splitter roles rejected");
        var corner=new Simulation(d);var first=Put(corner,"belt",40,40);var turn=Put(corner,"belt",41,40,1);var end=Put(corner,"storage",41,41,1);first.input.Add("iron",3);RunFor(corner,3);Check(end.input.Get("iron")==3,"side drop-off turns ninety degrees and conserves cargo");
        var opposing=new Simulation(d);var left=Put(opposing,"belt",40,40);var right=Put(opposing,"belt",41,40,2);left.input.Add("iron",1);RunFor(opposing,2);Check(left.input.Total==1&&right.input.Total==0,"opposing belt fronts do not connect");
        var blocked=new Simulation(d);sp=Put(blocked,"splitter",40,40);var outB=blocked.Ports(sp).Single(p=>p.index==sp.splitB);var sink=Put(blocked,"storage",outB.NextX,outB.NextY,outB.side);sp.input.Add("iron",6);RunFor(blocked,3);Check(sink.input.Total==6,"splitter uses available output when other is blocked");
        var undo=new Simulation(d);var belt=Put(undo,"belt",40,40);var box=Put(undo,"storage",42,40);belt.input.Add("iron",2);undo.SyncPackets(belt);belt.packets[0].position=.7f;box.input.Add("copper",3);string cargo=JsonUtility.ToJson(undo.world.cargo);int kits=undo.world.beltKits;
        undo.DeleteGroup(new[]{belt,box});Check(undo.At(40,40)==null&&undo.At(42,40)==null,"group deletion clears all footprints");Check(undo.UndoDelete(),"group undo succeeds");Check(undo.At(40,40).input.Total==2&&undo.At(42,40).input.Total==3&&Math.Abs(undo.At(40,40).packets[0].position-.7f)<.001f,"undo restores buffers and item positions");Check(undo.world.beltKits==kits&&undo.world.cargo.Get("iron")==0&&undo.world.cargo.Get("copper")==0,"undo reverses refunds without duplication");
        undo.DeleteGroup(new[]{undo.At(40,40)});Put(undo,"belt",40,40);Check(!undo.UndoDelete()&&undo.UndoCount==1,"occupied footprint blocks undo atomically");
        var map=new Simulation(d);Check(map.deposits.Count==68&&map.Resource(-18,-4)=="iron"&&map.Resource(-15,19)=="carbon","expanded map has distant starters and procedural nodes");
        var copy=new Simulation(d,JsonUtility.FromJson<World>(JsonUtility.ToJson(corner.world)));Check(copy.At(41,41).input.Total==3&&copy.world.beltDeliveries==corner.world.beltDeliveries,"new logistics state survives save roundtrip");
        Debug.Log("FACTORY_LOGISTICS_TESTS_PASSED");
    }
}
