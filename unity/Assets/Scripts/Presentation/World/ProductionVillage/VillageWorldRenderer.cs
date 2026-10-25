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
        private readonly Dictionary<string,Image> _lines=new Dictionary<string,Image>();
        private readonly Dictionary<string,Image> _chevrons=new Dictionary<string,Image>();
        private readonly Dictionary<long,Image> _cargo=new Dictionary<long,Image>();
        private GameObject _ghost;
        private FacilityKind _ghostKind;
        public bool IndustrialMode {get;set;}
        public Vector3 HarborPosition {get;}=new Vector3(-6.8f,.1f,5.7f);
        public VillageWorldRenderer(Transform parent,Camera camera,VillagePrefabCatalog prefabs,GameObject harbor)
        {
            _parent=parent;_prefabs=prefabs;
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
            // 2. 並び連携ボーナスのゴールド連結枠 (Synergy Frame)
            var synKeys=new HashSet<string>();
            if(view.GroupViewActive&&view.Synergies!=null)
            {
                foreach(var s in view.Synergies)
                {
                    var skey=s.Name+":"+s.CoreNodeId;synKeys.Add(skey);
                    if(!_synergies.TryGetValue(skey,out var sbox))
                        _synergies[skey]=sbox=Rect(ProductionWorldNames.Synergy+skey,_canvas,Vector2.zero,Vector2.zero,new Color(1f,.80f,.18f,.24f));
                    var cx=(s.MinX+s.Width*.5f-12)*Cell;var cy=(s.MinY+s.Height*.5f-12)*Cell;
                    sbox.color=s.Active?new Color(1f,.80f,.18f,.18f):new Color(.65f,.72f,.75f,.08f);
                    sbox.rectTransform.anchoredPosition=new Vector2(cx,cy);
                    sbox.rectTransform.sizeDelta=new Vector2((s.Width+.35f)*Cell,(s.Height+.35f)*Cell);
                }
            }
            foreach(var k in _synergies.Keys.Where(k=>!synKeys.Contains(k)).ToArray()){Object.Destroy(_synergies[k].gameObject);_synergies.Remove(k);}
            // 3. 足元の面は強化レベル、手前の細い帯は稼働状態。
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
                fp.color=VillageLevelColors.Tile(n.Level);
                var stopped=n.Status=="ガスを待っています"||n.Status=="出力の置き場が満杯";
                var waiting=n.Status=="材料を待っています";
                _statusBars[n.Id].color=stopped?new Color(.95f,.28f,.28f,.95f)
                    :waiting?new Color(.96f,.65f,.14f,.95f)
                    :n.Amplified?new Color(.18f,.83f,.92f,.95f):Color.clear;
                var working=running&&n.Status=="生産中"&&n.Progress<1;
                if(!_working.TryGetValue(n.Id,out var previous)||previous!=working)
                {
                    _working[n.Id]=working;
                    foreach(var motion in model.GetComponentsInChildren<ProjectPenguin.Presentation.World.ProductionIslandMotion>())motion.SetRunning(working);
                    foreach(var animator in model.GetComponentsInChildren<Animator>(true))animator.enabled=working;
                }
            }
            var selection=view.Nodes.FirstOrDefault(n=>n.Id==selected);_selection.gameObject.SetActive(selection!=null);
            if(selection!=null){_selection.rectTransform.anchoredPosition=Flat(Position(selection.X,selection.Y,selection.Size));_selection.rectTransform.sizeDelta=Vector2.one*selection.Size*Cell;_selection.transform.SetAsLastSibling();}
            // 4. コンベア・物流ライン + 方向パルスマーカー
            var keys=new HashSet<string>();
            var pulse=Mathf.Repeat(Time.unscaledTime*.85f,1f);
            foreach(var r in view.Routes)
            {
                var key=r.From+":"+r.To;keys.Add(key);var a=view.Nodes.First(n=>n.Id==r.From);var b=view.Nodes.First(n=>n.Id==r.To);
                var pa=Flat(Position(a.X,a.Y,a.Size));var pb=Flat(Position(b.X,b.Y,b.Size));
                if(!_lines.TryGetValue(key,out var line))_lines[key]=line=Rect(ProductionWorldNames.Route+key,_canvas,Vector2.zero,Vector2.zero,new Color(.08f,.24f,.34f,.72f));
                line.color=IndustrialMode?new Color(.96f,.65f,.14f,.78f):new Color(.12f,.58f,.72f,.62f);
                DrawLine(line,pa,pb,IndustrialMode?.068f:.048f);
                if(!_chevrons.TryGetValue(key,out var chev))_chevrons[key]=chev=Rect(ProductionWorldNames.Route+"Chev"+key,_canvas,Vector2.zero,new Vector2(.14f,.09f),new Color(1f,.88f,.35f,.92f));
                chev.rectTransform.anchoredPosition=Vector2.Lerp(pa,pb,pulse);
                var delta=pb-pa;chev.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            }
            foreach(var key in _lines.Keys.Where(k=>!keys.Contains(k)).ToArray()){Object.Destroy(_lines[key].gameObject);_lines.Remove(key);}
            foreach(var key in _chevrons.Keys.Where(k=>!keys.Contains(k)).ToArray()){Object.Destroy(_chevrons[key].gameObject);_chevrons.Remove(key);}
        }
        public void UpdateCargo(VillageView view)
        {
            if(view==null)return;
            foreach(var id in _cargo.Keys.Where(id=>!view.Cargo.Any(c=>c.Id==id)).ToArray()){Object.Destroy(_cargo[id].gameObject);_cargo.Remove(id);}
            foreach(var c in view.Cargo)
            {
                if(!_cargo.TryGetValue(c.Id,out var icon))_cargo[c.Id]=icon=Rect(ProductionWorldNames.Cargo+c.Id,_canvas,Vector2.zero,Vector2.one*.19f,CargoColor(c.Item));
                icon.color=CargoColor(c.Item);
                var a=Flat(Position(c.FromX,c.FromY,view.Nodes.First(n=>n.Id==c.From).Size));var b=Flat(Position(c.ToX,c.ToY,view.Nodes.First(n=>n.Id==c.To).Size));
                icon.rectTransform.anchoredPosition=Vector2.Lerp(a,b,Mathf.Clamp01((float)(view.AtMs-c.Start)/(c.End-c.Start)));
            }
        }
        private static Color CargoColor(Item item)=>item switch
        {
            Item.Fish=>new Color(.18f,.66f,.93f),
            Item.Surimi=>new Color(.36f,.75f,.75f),
            Item.Salt=>new Color(.96f,.72f,.18f),
            Item.Kelp=>new Color(.18f,.80f,.56f),
            Item.Kamaboko=>new Color(1f,.48f,.56f),
            Item.Chikuwa=>new Color(.95f,.61f,.07f),
            Item.DriedFish=>new Color(.90f,.49f,.13f),
            Item.Oden=>new Color(1f,.82f,.23f),
            _=>new Color(1f,.62f,.13f)
        };
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
        private static void DrawLine(Image image,Vector2 a,Vector2 b,float width)
        {var rt=image.rectTransform;var delta=b-a;rt.pivot=new Vector2(0,.5f);rt.anchoredPosition=a;rt.sizeDelta=new Vector2(delta.magnitude,width);rt.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);}
        private static Image Rect(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchoredPosition=position;rt.sizeDelta=size;var i=go.GetComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
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
        private GameObject Model(GameObject prefab,float width,string name)
        {
            var holder=new GameObject(name);holder.transform.SetParent(_parent,false);
            if(prefab==null)return holder;
            var instance=Object.Instantiate(prefab,holder.transform);
            foreach(var b in instance.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=b is ProjectPenguin.Presentation.World.ProductionIslandMotion || b is ProjectPenguin.Presentation.World.ProductionIslandPenguinView;
            foreach(var a in instance.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var renderers=instance.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return holder;
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            instance.transform.localScale*=width/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z));
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            instance.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);return holder;
        }
    }
}
