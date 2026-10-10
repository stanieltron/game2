using System;
using System.Linq;
using BootstrapFactory;
using UnityEngine;

public static class FactoryBeltPlacementTests
{
    static void Check(bool condition,string name){if(!condition)throw new Exception("BELT PLACEMENT FAILED: "+name);Debug.Log("PASS: "+name);}
    public static void Run(Catalog data)
    {
        var s=new Simulation(data);var start=new Vector2Int(40,40);int kits=s.world.beltKits,count=s.world.machines.Count;
        var longPreview=Simulation.BeltLine(start,new Vector2Int(35,48),0);
        var shortPreview=Simulation.BeltLine(start,new Vector2Int(38,33),0);
        Check(longPreview.Count==6&&shortPreview.Count==3&&shortPreview.All(t=>t.y==40),"dragging left five then right three leaves three straight tiles");
        Check(s.world.machines.Count==count&&s.world.beltKits==kits,"preview neither builds nor spends");
        s.CommitBeltRoute(shortPreview,2);Check(s.world.machines.Count==count+3&&s.world.beltKits==kits-3&&s.At(35,40)==null,"release commits only the shortened route");
        var belt=s.At(39,40);belt.input.Add("copper",2);s.SyncPackets(belt);belt.tier=3;belt.progress=.4f;int uid=belt.uid;
        s.CommitBeltRoute(shortPreview,0);
        Check(s.At(39,40).uid==uid&&belt.input.Get("copper")==2&&belt.packets.Count==2&&belt.tier==3&&s.world.beltKits==kits-3,"overbuilding reorients belts in place without charging or losing cargo/upgrades");
        Check(shortPreview.All(t=>s.At(t.x,t.y).direction==0),"overlaid route applies new direction to every existing belt");
        var blocker=s.Place("storage",43,40,0);var obstructed=Simulation.BeltLine(start,new Vector2Int(45,40),0);int before=s.world.machines.Count;
        Check(s.CommitBeltRoute(obstructed,1)==null&&s.world.machines.Count==before&&s.At(42,40)==null&&belt.direction==0,"obstruction rejects entire route without partial placement or rotation");
        var vertical=Simulation.BeltLine(new Vector2Int(50,40),new Vector2Int(72,43),1);s.CommitBeltRoute(vertical,1);
        Check(vertical.Count==4&&vertical.All(t=>t.x==50&&s.At(t.x,t.y).direction==1),"vertical route ignores sideways mouse movement");
        Check(Simulation.BeltLine(start,start,0).Count==1,"click without dragging previews one belt");
    }
}
