using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>
    /// 自動配送のつながりを 3D の部品で見せる。施設の出口・入口 (Link_Port_*)、道に並んで流れる矢印 (Link_Arrow_*)、
    /// 道の上を跳ねて運ばれる品物 (Item_*)、できあがった瞬間に飛び出す品物。規則は変えず、VillageView を読むだけ。
    /// 部品は使い回し、毎フレーム作り直さない。
    /// </summary>
    public sealed class VillageLinkView
    {
        private const float ArrowSpacing=.42f,ArrowSpeed=.55f,ArrowWidth=.26f,PortWidth=.36f,CargoWidth=.3f,PopSeconds=.9f,OverviewScale=2f;
        private static readonly Color In=new Color(.55f,.92f,1f),Out=new Color(1f,.86f,.38f),Dim=new Color(.62f,.66f,.7f),Plain=Color.white;

        private readonly Transform _root;
        private readonly VillagePrefabCatalog _catalog;
        private readonly Pool _arrows,_blocked;
        private readonly Dictionary<VillagePortState,Pool> _inputs=new Dictionary<VillagePortState,Pool>(),_outputs=new Dictionary<VillagePortState,Pool>();
        private readonly Dictionary<Item,Pool> _cargo=new Dictionary<Item,Pool>(),_pops=new Dictionary<Item,Pool>();
        private readonly Dictionary<int,int> _totals=new Dictionary<int,int>();
        private readonly List<(Item Item,Vector3 At,float Start)> _popping=new List<(Item,Vector3,float)>();
        /// <summary>今の色を覚えておき、同じ色なら MaterialPropertyBlock を書き直さない。</summary>
        private readonly Dictionary<GameObject,Color> _tints=new Dictionary<GameObject,Color>();

        public VillageLinkView(Transform parent,VillagePrefabCatalog catalog)
        {
            _root=new GameObject(ProductionWorldNames.Links).transform;_root.SetParent(parent,false);
            _catalog=catalog;
            var links=catalog?.Links;
            _arrows=new Pool(_root,links?.Arrow(false),ArrowWidth,ProductionWorldNames.LinkArrow);
            _blocked=new Pool(_root,links?.Arrow(true),ArrowWidth,ProductionWorldNames.LinkArrow+"Blocked");
            foreach(VillagePortState s in System.Enum.GetValues(typeof(VillagePortState)))
            {
                _inputs[s]=new Pool(_root,links?.Input(s),PortWidth,ProductionWorldNames.LinkPort+"In"+s);
                _outputs[s]=new Pool(_root,links?.Output(s),PortWidth,ProductionWorldNames.LinkPort+"Out"+s);
            }
        }

        /// <param name="selected">選んでいる施設。入る道 (水色) と出る道 (黄) だけを強く見せ、ほかは薄くする。0 なら全部ふつう。</param>
        /// <param name="selectable">配置中など、出入口を「選べる」形で見せるとき。</param>
        public void Render(VillageView view,VillageZoomLevel level,int selected,bool selectable,bool running)
        {
            Begin();
            if(view==null){End();return;}
            var nodes=view.Nodes.ToDictionary(n=>n.Id);
            var overview=level==VillageZoomLevel.Overview;
            var scale=overview?OverviewScale:1f;
            var time=Time.unscaledTime;

            // 1. 道ごとの出口と入口 (同じ施設の同じ辺は 1 つにまとめる)
            var outSides=new HashSet<(int,int)>();var inSides=new HashSet<(int,int)>();
            foreach(var r in view.Routes)
            {
                if(!nodes.TryGetValue(r.From,out var a)||!nodes.TryGetValue(r.To,out var b))continue;
                var (pa,pb,sa,sb)=Ends(a,b);
                var related=selected==0||r.From==selected||r.To==selected;
                var tint=selected==0?Plain:r.From==selected?Out:r.To==selected?In:Dim;
                if(outSides.Add((a.Id,sa)))Place(_outputs[selectable?VillagePortState.Selectable:VillagePortState.Connected].Next(),pa,Side(sa),scale,tint);
                if(inSides.Add((b.Id,sb)))Place(_inputs[selectable?VillagePortState.Selectable:VillagePortState.Connected].Next(),pb,-Side(sb),scale,tint);
                if(overview&&selected!=0&&!related)continue;

                // 2. 道の矢印。送れない道 (受け取り側が満杯・送り元がガス切れ) は止まった赤い矢印にする
                var blocked=b.Status==VillageInsight.Full||a.Status==VillageInsight.GasWait&&!view.Unlimited;
                var dir=pb-pa;var length=dir.magnitude;if(length<.05f)continue;dir/=length;
                var spacing=ArrowSpacing*scale;
                var shift=blocked||!running?0:Mathf.Repeat(time*ArrowSpeed,spacing);
                for(var d=spacing*.5f+shift;d<length-spacing*.25f;d+=spacing)
                    Place((blocked?_blocked:_arrows).Next(),pa+dir*d+Vector3.up*.02f,dir,scale,blocked?Plain:tint);
            }

            // 3. 道が 1 本も無い施設には「つながっていない」出入口を出す (送り出す施設は出口、受け取る施設は入口)
            foreach(var n in view.Nodes)
            {
                var recipe=IslandCatalog.Get(n.Kind).Recipes.FirstOrDefault();
                var center=VillageWorldRenderer.Position(n.X,n.Y,n.Size);var half=n.Size*VillageWorldRenderer.Cell*.47f;
                if(recipe!=null&&!view.Routes.Any(r=>r.From==n.Id))
                    Place(_outputs[selectable?VillagePortState.Selectable:VillagePortState.Free].Next(),center+Vector3.right*half,Vector3.right,scale,Plain);
                var accepts=n.Kind==FacilityKind.Warehouse||recipe!=null&&recipe.Inputs.Count>0;
                if(accepts&&!view.Routes.Any(r=>r.To==n.Id))
                    Place(_inputs[selectable?VillagePortState.Selectable:VillagePortState.Free].Next(),center-Vector3.right*half,Vector3.right,scale,Plain);
            }

            // 4. 運ばれている品物。全体表示では道ごとに先頭の 1 個だけ
            var cargo=view.Cargo.Where(c=>nodes.ContainsKey(c.From)&&nodes.ContainsKey(c.To));
            if(overview)cargo=cargo.GroupBy(c=>(c.From,c.To)).Select(g=>g.OrderByDescending(c=>Progress(view,c)).First());
            foreach(var c in cargo)
            {
                var (pa,pb,_,_)=Ends(nodes[c.From],nodes[c.To]);
                var t=Progress(view,c);
                var hop=Mathf.Abs(Mathf.Sin(t*Mathf.PI*Mathf.Max(1,Mathf.Round(Vector3.Distance(pa,pb)/.5f))))*.08f;
                var dir=pb-pa;
                var item=CargoPool(c.Item).Next();
                Place(item,Vector3.Lerp(pa,pb,t)+Vector3.up*(.1f+hop),dir.sqrMagnitude>0?dir:Vector3.forward,scale*(overview?1.2f:1f),Plain);
            }

            // 5. できあがった瞬間に、施設の上へ品物が飛び出す
            foreach(var n in view.Nodes)
            {
                var had=_totals.TryGetValue(n.Id,out var before);_totals[n.Id]=n.Total;
                var recipe=IslandCatalog.Get(n.Kind).Recipes.FirstOrDefault();
                if(had&&n.Total>before&&recipe!=null&&_popping.Count<24)
                    _popping.Add((recipe.Output.Item,VillageWorldRenderer.Position(n.X,n.Y,n.Size)+Vector3.up*(n.Size*VillageWorldRenderer.Cell*.55f),time));
            }
            foreach(var id in _totals.Keys.Where(id=>!nodes.ContainsKey(id)).ToArray())_totals.Remove(id);
            _popping.RemoveAll(p=>time-p.Start>PopSeconds);
            foreach(var p in _popping)
            {
                var k=(time-p.Start)/PopSeconds;
                var go=PopPool(p.Item).Next();
                go.transform.position=p.At+Vector3.up*(.45f*(1-(1-k)*(1-k)));
                go.transform.rotation=Quaternion.Euler(0,k*360f,0);
                go.transform.localScale=Vector3.one*scale*1.3f*Mathf.Lerp(1f,.2f,k*k);
            }
            End();
        }

        /// <summary>a の出口と b の入口の位置と、どの辺か (0:+X 1:-X 2:+Z 3:-Z)。</summary>
        private static (Vector3,Vector3,int,int) Ends(VillageNodeView a,VillageNodeView b)
        {
            var ca=VillageWorldRenderer.Position(a.X,a.Y,a.Size);var cb=VillageWorldRenderer.Position(b.X,b.Y,b.Size);
            var sa=SideOf(cb-ca);var sb=SideOf(ca-cb);
            return (ca+Side(sa)*(a.Size*VillageWorldRenderer.Cell*.47f),cb+Side(sb)*(b.Size*VillageWorldRenderer.Cell*.47f),sa,sb);
        }
        private static int SideOf(Vector3 d)=>Mathf.Abs(d.x)>=Mathf.Abs(d.z)?(d.x>=0?0:1):(d.z>=0?2:3);
        private static Vector3 Side(int s)=>s==0?Vector3.right:s==1?Vector3.left:s==2?Vector3.forward:Vector3.back;
        private static float Progress(VillageView view,VillageCargoView c)=>Mathf.Clamp01((float)(view.AtMs-c.Start)/Mathf.Max(1,c.End-c.Start));

        /// <summary>部品を地面の上に置き、進む向き (dir) を部品の正面 (-Z) に合わせる。</summary>
        private void Place(GameObject go,Vector3 at,Vector3 dir,float scale,Color tint)
        {
            dir.y=0;
            go.transform.position=at;
            go.transform.rotation=dir.sqrMagnitude>1e-6f?Quaternion.LookRotation(-dir.normalized,Vector3.up):Quaternion.identity;
            go.transform.localScale=Vector3.one*scale;
            if(!_tints.TryGetValue(go,out var current)||current!=tint){_tints[go]=tint;VillageModels.Tint(go,tint);}
        }

        private Pool CargoPool(Item item)
        {
            if(!_cargo.TryGetValue(item,out var p))_cargo[item]=p=new Pool(_root,_catalog?.ItemModel(item),CargoWidth,ProductionWorldNames.CargoModel+item);
            return p;
        }
        private Pool PopPool(Item item)
        {
            if(!_pops.TryGetValue(item,out var p))_pops[item]=p=new Pool(_root,_catalog?.ItemModel(item),CargoWidth,ProductionWorldNames.Pop+item);
            return p;
        }
        private IEnumerable<Pool> All=>new[]{_arrows,_blocked}.Concat(_inputs.Values).Concat(_outputs.Values).Concat(_cargo.Values).Concat(_pops.Values);
        private void Begin(){foreach(var p in All)p.Begin();}
        private void End(){foreach(var p in All)p.End();}

        /// <summary>同じ部品を使い回す。Begin で数え直し、End で使わなかった分を隠す。</summary>
        private sealed class Pool
        {
            private readonly Transform _parent;private readonly GameObject _prefab;private readonly float _width;private readonly string _name;
            private readonly List<GameObject> _items=new List<GameObject>();private int _used;
            public Pool(Transform parent,GameObject prefab,float width,string name){_parent=parent;_prefab=prefab;_width=width;_name=name;}
            public void Begin()=>_used=0;
            public GameObject Next()
            {
                if(_used==_items.Count)_items.Add(VillageModels.Spawn(_prefab,_width,_parent,_name+_items.Count));
                var go=_items[_used++];if(!go.activeSelf)go.SetActive(true);return go;
            }
            public void End(){for(var i=_used;i<_items.Count;i++)if(_items[i].activeSelf)_items[i].SetActive(false);}
        }
    }
}
