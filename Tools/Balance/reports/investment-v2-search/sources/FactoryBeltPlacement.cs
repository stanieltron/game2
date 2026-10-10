using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BootstrapFactory
{
    public partial class Simulation
    {
        public static List<Vector2Int> BeltLine(Vector2Int start,Vector2Int end,int axis)
        {
            int distance=axis==0?end.x-start.x:end.y-start.y;
            var tiles=new List<Vector2Int>();int step=Math.Sign(distance);
            for(int i=0;i<=Math.Abs(distance);i++)tiles.Add(start+(axis==0?new Vector2Int(i*step,0):new Vector2Int(0,i*step)));
            return tiles;
        }
        public string BeltRouteError(IEnumerable<Vector2Int> tiles)
        {
            foreach(var tile in tiles)
            {
                if(BridgeAt(tile.x,tile.y)!=null)return "A bridge cannot be bridged at "+tile.x+", "+tile.y;
                if(At(tile.x,tile.y)?.def=="belt")continue;
                string error=PlacementError("belt",tile.x,tile.y);if(error!="")return error+" at "+tile.x+", "+tile.y;
            }
            return "";
        }
        public Machine CommitBeltRoute(List<Vector2Int> tiles,int facing)
        {
            notice=BeltRouteError(tiles);if(notice!="")return null;
            Machine last=null;int placed=0,rotated=0,bridged=0;
            foreach(var tile in tiles.Distinct())
            {
                last=At(tile.x,tile.y);
                if(WouldBridge(tiles,tile,facing)){last=PlaceBridge(tile.x,tile.y,facing);bridged++;}
                else if(last!=null){if(last.direction!=facing){last.direction=facing;rotated++;}}
                else {last=Place("belt",tile.x,tile.y,facing);placed++;}
            }
            notice=$"Belt route: {placed} new tiles, {bridged} bridges, {rotated} reoriented. Existing cargo and upgrades preserved.";return last;
        }
    }
    public partial class FactoryGame
    {
        bool beltDragging,beltFacingOverride;
        int beltAxis=-1;
        Vector2Int beltStart,beltEnd;
        readonly HashSet<int> markStrokeSeen=new HashSet<int>();
        bool markStrokeModeSet,markStrokeErase;
        List<Vector2Int> BeltPreviewTiles()=>Simulation.BeltLine(beltStart,beltEnd,beltAxis<0?direction%2:beltAxis);
        bool MapInputAvailable(Vector2 mouse)=>map.Contains(mouse)&&!questRect.Contains(mouse)&&!miniRect.Contains(mouse)&&!(build!=""&&new Rect(514,195,730,64).Contains(mouse));
        void BeginBeltDrag(Vector2Int tile)
        {
            beltDragging=true;beltStart=beltEnd=tile;beltAxis=-1;beltFacingOverride=false;GUIUtility.keyboardControl=0;
        }
        void MoveBeltDrag(Vector2Int tile)
        {
            Vector2Int delta=tile-beltStart;
            if(beltAxis<0&&delta!=Vector2Int.zero)beltAxis=Math.Abs(delta.x)>=Math.Abs(delta.y)?0:1;
            beltEnd=beltAxis==0?new Vector2Int(tile.x,beltStart.y):beltAxis==1?new Vector2Int(beltStart.x,tile.y):beltStart;
            int distance=beltAxis==0?delta.x:delta.y;
            if(beltAxis>=0&&distance!=0&&!beltFacingOverride)direction=beltAxis==0?(distance>0?0:2):(distance>0?1:3);
        }
        void CommitBeltDrag()
        {
            selected=sim.CommitBeltRoute(BeltPreviewTiles(),direction);beltDragging=false;
        }
        void UpdateBeltDrag()
        {
            if(qa)return;
            if(build!="belt"||help||modal||confirmReset||deleting){beltDragging=false;return;}
            if(Input.GetMouseButtonDown(0)&&MapInputAvailable(Mouse))BeginBeltDrag(Tile(Mouse));
            if(!beltDragging)return;
            if(Input.GetMouseButtonDown(1)||Input.GetKeyDown(KeyCode.Escape)){beltDragging=false;return;}
            if(MapInputAvailable(Mouse))MoveBeltDrag(Tile(Mouse));
            if(Input.GetMouseButtonUp(0))
            {
                if(MapInputAvailable(Mouse))CommitBeltDrag();else {beltDragging=false;sim.notice="Belt route canceled: release over the map to build";}
            }
        }
        void DrawBeltDrag()
        {
            if(!beltDragging)return;var tiles=BeltPreviewTiles();bool valid=sim.BeltRouteError(tiles)=="";
            foreach(var tile in tiles)
            {
                Rect r=TileRect(tile.x,tile.y);Color color=valid?accent:new Color(1,.3f,.2f);
                if(sim.WouldBridge(tiles,tile,direction))DrawBridge(new Machine{def="belt",x=tile.x,y=tile.y,direction=direction,raised=true},r,true);
                else DrawBelt(new Machine{def="belt",x=tile.x,y=tile.y,direction=direction},r);
                Box(r,new Color(color.r,color.g,color.b,.16f));Border(r,color,2);Arrow(r.center,direction,white,Mathf.Clamp(cell*.3f,7,16));
            }
            Rect end=TileRect(beltEnd.x,beltEnd.y);float x=Mathf.Clamp(end.x,8,map.width-420),y=Mathf.Clamp(end.y-48,85,map.height-56);
            Box(new Rect(x,y,410,43),ink);Label(x+8,y+3,394,20,$"{tiles.Count} TILES / RELEASE TO BUILD / R REVERSES",heading,accent);
            Label(x+8,y+23,394,18,valid?"Drag back to shorten. Escape or right-click cancels.":sim.BeltRouteError(tiles),small,valid?sub:accent);
        }
        void PaintMark(Machine m)
        {
            if(m==null||m.def=="core"||!markStrokeSeen.Add(m.uid))return;
            if(!markStrokeModeSet){markStrokeErase=marked.Contains(m.uid);markStrokeModeSet=true;}
            if(markStrokeErase)marked.Remove(m.uid);else marked.Add(m.uid);
            sim.notice=$"{marked.Count} marked. Click a marked object to unmark it. Del applies deletion.";
        }
        void OnApplicationFocus(bool focused){if(!focused){beltDragging=false;deleting=false;}}
    }
}
