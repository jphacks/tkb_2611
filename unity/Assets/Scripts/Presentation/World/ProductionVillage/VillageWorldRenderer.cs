using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    public sealed class VillageWorldRenderer
    {
        public const float Cell=.65f;
        private const float ModelScale=.86f;
        private readonly Transform _parent;
        private readonly Camera _camera;
        private readonly Dictionary<int,VillageEffortFlame> _flames=new Dictionary<int,VillageEffortFlame>();
        private readonly VillagePrefabCatalog _prefabs;
        private readonly RectTransform _canvas;
        private readonly GameObject _grid,_subtleGrid,_tacticalFrame;
        private readonly Image _selection,_preview;
        private readonly Dictionary<int,GameObject> _models=new Dictionary<int,GameObject>();
        private readonly Dictionary<int,FacilityKind> _kinds=new Dictionary<int,FacilityKind>();
        private readonly Dictionary<int,bool> _working=new Dictionary<int,bool>();
        private readonly Dictionary<int,Image> _footprints=new Dictionary<int,Image>();
        private readonly Dictionary<int,Image> _statusBars=new Dictionary<int,Image>();
        private readonly Dictionary<int,Image> _zones=new Dictionary<int,Image>();
        private readonly Dictionary<string,Image> _synergies=new Dictionary<string,Image>();
        private GameObject _ghost;
        private FacilityKind _ghostKind;
        public bool IndustrialMode {get;set;}
        /// <summary>盤面に寝かせた World Space Canvas。案内 (VillageGuideView) もここに描く。</summary>
        public RectTransform Canvas=>_canvas;
        public Vector3 HarborPosition {get;}=new Vector3(-6.8f,.1f,5.7f);
        public VillageWorldRenderer(Transform parent,Camera camera,VillagePrefabCatalog prefabs,GameObject harbor)
        {
            _parent=parent;_prefabs=prefabs;_camera=camera;
            var root=new GameObject(ProductionWorldNames.WorldCanvas,typeof(RectTransform),typeof(Canvas));root.transform.SetParent(parent,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;root.GetComponent<Canvas>().worldCamera=camera;
            _canvas=(RectTransform)root.transform;_canvas.sizeDelta=Vector2.one*24*Cell;_canvas.position=new Vector3(0,.035f,0);_canvas.rotation=Quaternion.Euler(90,0,0);
            // 常時うっすら見える氷タイル目地 (のっぺりした白板に見せない)
            _subtleGrid=new GameObject(ProductionWorldNames.SubtleGrid,typeof(RectTransform));_subtleGrid.transform.SetParent(_canvas,false);
            for(var i=0;i<=24;i+=2)
            {Rect(ProductionWorldNames.SubtleGrid+"X"+i,_subtleGrid.transform,new Vector2((i-12)*Cell,0),new Vector2(.014f,24*Cell),new Color(.35f,.68f,.86f,.16f));
             Rect(ProductionWorldNames.SubtleGrid+"Y"+i,_subtleGrid.transform,new Vector2(0,(i-12)*Cell),new Vector2(24*Cell,.014f),new Color(.35f,.68f,.86f,.16f));}
            // 工業モード・配置中の精密グリッド
            _grid=new GameObject(ProductionWorldNames.Grid,typeof(RectTransform));_grid.transform.SetParent(_canvas,false);
            for(var i=0;i<=24;i++)
            {
                var major=i%2==0;
                var col=major?new Color(.18f,.78f,.92f,.38f):new Color(.2f,.55f,.65f,.20f);
                var w=major?.022f:.012f;
                Rect(ProductionWorldNames.Grid+"X"+i,_grid.transform,new Vector2((i-12)*Cell,0),new Vector2(w,24*Cell),col);
                Rect(ProductionWorldNames.Grid+"Y"+i,_grid.transform,new Vector2(0,(i-12)*Cell),new Vector2(24*Cell,w),col);
            }
            _grid.SetActive(false);
            // エンドフィールド風の黄色/シアン戦術境界フレーム
            _tacticalFrame=new GameObject(ProductionWorldNames.TacticalFrame,typeof(RectTransform));_tacticalFrame.transform.SetParent(_canvas,false);
            var boardSpan=24*Cell;var halfSpan=12*Cell;var borderCol=new Color(.96f,.72f,.14f,.85f);
            Rect("Top",_tacticalFrame.transform,new Vector2(0,halfSpan),new Vector2(boardSpan,.06f),borderCol);
            Rect("Bottom",_tacticalFrame.transform,new Vector2(0,-halfSpan),new Vector2(boardSpan,.06f),borderCol);
            Rect("Left",_tacticalFrame.transform,new Vector2(-halfSpan,0),new Vector2(.06f,boardSpan),borderCol);
            Rect("Right",_tacticalFrame.transform,new Vector2(halfSpan,0),new Vector2(.06f,boardSpan),borderCol);
            _tacticalFrame.SetActive(false);
            _selection=Rect(ProductionWorldNames.Selection,_canvas,Vector2.zero,Vector2.zero,Color.clear);
            SelectionBorder(_selection.rectTransform);_selection.gameObject.SetActive(false);
            _preview=Rect(ProductionWorldNames.Preview,_canvas,Vector2.zero,Vector2.zero,Color.green);_preview.enabled=false;
            if(harbor!=null){var port=Model(harbor,2.5f,ProductionWorldNames.Port);port.transform.position=HarborPosition;}
        }
        public static Vector3 Position(int x,int y,int size)=>new Vector3((x+size*.5f-12)*Cell,.08f,(y+size*.5f-12)*Cell);
        public static Vector2 Flat(Vector3 p)=>new Vector2(p.x,p.z);
        public void Render(VillageView view,int selected,bool running=true)
        {
            foreach(var id in _models.Keys.Where(id=>!view.Nodes.Any(n=>n.Id==id&&n.Kind==_kinds[id])).ToArray())
            {
                if(_flames.TryGetValue(id,out var flame)){Object.Destroy(flame.Root);_flames.Remove(id);}
                Object.Destroy(_models[id]);_models.Remove(id);_kinds.Remove(id);_working.Remove(id);
                if(_footprints.TryGetValue(id,out var fp)){Object.Destroy(fp.gameObject);_footprints.Remove(id);_statusBars.Remove(id);}
                if(_zones.TryGetValue(id,out var zn)){Object.Destroy(zn.gameObject);_zones.Remove(id);}
            }
            // 1. 増幅器・氷室・仕分け機の稼働エリア (Cyan Coverage Zone)
            foreach(var n in view.Nodes)
            {
                if(n.CoverageRadius>0&&(IndustrialMode||n.Id==selected))
                {
                    if(!_zones.TryGetValue(n.Id,out var zone))
                        _zones[n.Id]=zone=Rect(ProductionWorldNames.Zone+n.Id,_canvas,Vector2.zero,Vector2.zero,new Color(.18f,.83f,.92f,.16f));
                    zone.enabled=true;
                    zone.rectTransform.anchoredPosition=Flat(Position(n.X,n.Y,n.Size));
                    zone.rectTransform.sizeDelta=Vector2.one*Mathf.Min(24,n.Size+n.CoverageRadius*2)*Cell;
                }
                else if(_zones.TryGetValue(n.Id,out var zone))zone.enabled=false;
            }
            // 2. 成立している施設群の金色の枠。全体表示で追加生産が動いている間は光らせる
            var synKeys=new HashSet<string>();
            var glow=.5f+.5f*Mathf.Sin(Time.unscaledTime*3.2f);
            if(view.Synergies!=null)
            {
                foreach(var s in view.Synergies)
                {
                    var skey=s.Name+":"+s.CoreNodeId;synKeys.Add(skey);
                    if(!_synergies.TryGetValue(skey,out var sbox))
                        _synergies[skey]=sbox=Rect(ProductionWorldNames.Synergy+skey,_canvas,Vector2.zero,Vector2.zero,new Color(1f,.80f,.18f,.24f));
                    var cx=(s.MinX+s.Width*.5f-12)*Cell;var cy=(s.MinY+s.Height*.5f-12)*Cell;
                    sbox.color=s.Active?new Color(1f,.80f,.18f,.18f+.16f*glow):view.GroupViewActive?new Color(.95f,.78f,.3f,.14f):new Color(1f,.84f,.3f,.10f);
                    sbox.rectTransform.anchoredPosition=new Vector2(cx,cy);
                    sbox.rectTransform.sizeDelta=new Vector2((s.Width+.35f)*Cell,(s.Height+.35f)*Cell);
                }
            }
            foreach(var k in _synergies.Keys.Where(k=>!synKeys.Contains(k)).ToArray()){Object.Destroy(_synergies[k].gameObject);_synergies.Remove(k);}
            // 3. エフォートは屋根上の炎の数と大きさ。足元は共通色、細い帯は稼働状態。
            foreach(var n in view.Nodes)
            {
                if(!_models.TryGetValue(n.Id,out var model)){_models[n.Id]=model=Model(_prefabs.Get(n.Kind),n.Size*Cell*ModelScale,ProductionWorldNames.Facility+n.Id);_kinds[n.Id]=n.Kind;}
                model.transform.position=Position(n.X,n.Y,n.Size);
                if(!_footprints.TryGetValue(n.Id,out var fp))
                {
                    _footprints[n.Id]=fp=Rect(ProductionWorldNames.Footprint+n.Id,_canvas,Vector2.zero,Vector2.one*(n.Size*Cell*.98f),Color.clear);
                    _statusBars[n.Id]=Rect("Status",fp.transform,new Vector2(0,-n.Size*Cell*.43f),new Vector2(n.Size*Cell*.72f,.07f),Color.clear);
                }
                fp.rectTransform.anchoredPosition=Flat(Position(n.X,n.Y,n.Size));
                fp.color=new Color32(144,197,216,180);
                var stopped=n.Status==VillageInsight.GasWait||n.Status==VillageInsight.Full;
                var waiting=n.Status==VillageInsight.MaterialWait;
                var pulse=.55f+.45f*Mathf.Sin(Time.unscaledTime*4f);
                _statusBars[n.Id].color=stopped?new Color(.95f,.28f,.28f,.6f+.35f*pulse)
                    :waiting?new Color(.96f,.65f,.14f,.6f+.35f*pulse)
                    :n.Amplified?new Color(.18f,.83f,.92f,.95f):Color.clear;
                var working=running&&n.Status=="生産中"&&n.Progress<1;
                if(n.Effort>0&&!_flames.ContainsKey(n.Id))_flames[n.Id]=new VillageEffortFlame(_parent,model,_camera,n.Id);
                if(_flames.TryGetValue(n.Id,out var effortFlame))effortFlame.Render(model.transform.position,n.Effort,working,_camera,Time.unscaledTime);
                if(!_working.TryGetValue(n.Id,out var previous)||previous!=working)
                {
                    _working[n.Id]=working;
                    foreach(var motion in model.GetComponentsInChildren<ProjectPenguin.Presentation.World.ProductionIslandMotion>())motion.SetRunning(working);
                    foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=working;
                }
            }
            var selection=view.Nodes.FirstOrDefault(n=>n.Id==selected);_selection.gameObject.SetActive(selection!=null);
            if(selection!=null){_selection.rectTransform.anchoredPosition=Flat(Position(selection.X,selection.Y,selection.Size));_selection.rectTransform.sizeDelta=Vector2.one*selection.Size*Cell;_selection.transform.SetAsLastSibling();}
        }
        public void Preview(FacilityKind kind,int x,int y,bool valid,bool enabled)
        {
            _grid.SetActive(enabled||IndustrialMode);
            _tacticalFrame.SetActive(enabled||IndustrialMode);
            _preview.enabled=enabled;
            if(!enabled){if(_ghost!=null)_ghost.SetActive(false);return;}
            _preview.transform.SetAsLastSibling();
            var size=IslandCatalog.Get(kind).Size;
            if(_ghost==null||_ghostKind!=kind){if(_ghost!=null)Object.Destroy(_ghost);_ghostKind=kind;_ghost=Model(_prefabs.Get(kind),size*Cell*ModelScale,ProductionWorldNames.Ghost);}
            _ghost.SetActive(true);_ghost.transform.position=Position(x,y,size)+Vector3.up*.15f;
            _preview.color=valid?new Color(.15f,.85f,.5f,.65f):new Color(.95f,.25f,.22f,.65f);
            _preview.rectTransform.anchoredPosition=Flat(Position(x,y,size));_preview.rectTransform.sizeDelta=Vector2.one*size*Cell;
        }
        private static Image Rect(string name,Transform parent,Vector2 position,Vector2 size,Color color)=>VillageModels.Rect(name,parent,position,size,color);
        private GameObject Model(GameObject prefab,float width,string name)=>VillageModels.Spawn(prefab,width,_parent,name);
        private static void SelectionBorder(RectTransform parent)
        {
            var color=new Color(.08f,.68f,.84f,1);
            for(var edge=0;edge<4;edge++)
            {
                var rt=Rect("Edge"+edge,parent,Vector2.zero,Vector2.zero,color).rectTransform;
                var horizontal=edge<2;var side=edge%2;
                rt.anchorMin=horizontal?new Vector2(0,side):new Vector2(side,0);
                rt.anchorMax=horizontal?new Vector2(1,side):new Vector2(side,1);
                rt.sizeDelta=horizontal?new Vector2(0,.035f):new Vector2(.035f,0);
            }
        }
    }
}
