using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>
    /// 「どうすればよいか」を地面の上に見せる。
    /// 配置中: 届く範囲 (マンハッタン距離のひし形) と、つながる相手への点線。
    /// 施設を選んでいるとき: その施設の届く範囲。
    /// 工房モード: あと 1 棟で施設群が成立する場所 (金色の床と、置くとよい施設の影)。
    /// </summary>
    public sealed class VillageGuideView
    {
        public struct Input
        {
            public bool Industrial;
            public bool Placing;
            public FacilityKind Kind;
            public int X,Y;
            public VillageLinkPreview Preview;
            public int SelectedId,SelectedReach;
            public IReadOnlyList<VillageGroupHint> Hints;
        }

        private static readonly Color ReachFill=new Color(.18f,.78f,.92f,.07f),ReachEdge=new Color(.18f,.78f,.92f,.55f),
            InLink=new Color(.35f,.86f,1f,.9f),OutLink=new Color(1f,.78f,.22f,.95f),Gold=new Color(1f,.8f,.18f,1f);
        private const float Dash=.22f,Gap=.12f,DashWidth=.12f,EdgeWidth=.05f;

        private readonly RectTransform _canvas;
        private readonly Transform _root;
        private readonly VillagePrefabCatalog _catalog;
        private readonly Image _reachFill;
        private readonly Image[] _reachEdges=new Image[4];
        private readonly List<Image> _dashes=new List<Image>();
        private readonly List<Image> _pads=new List<Image>();
        private readonly Dictionary<(FacilityKind,int),GameObject> _ghosts=new Dictionary<(FacilityKind,int),GameObject>();
        private int _dashUsed;

        public VillageGuideView(RectTransform canvas,Transform parent,VillagePrefabCatalog catalog)
        {
            _canvas=canvas;_catalog=catalog;
            _root=new GameObject(ProductionWorldNames.Guide).transform;_root.SetParent(parent,false);
            _reachFill=VillageModels.Rect(ProductionWorldNames.Reach,_canvas,Vector2.zero,Vector2.zero,ReachFill);_reachFill.enabled=false;
            for(var i=0;i<4;i++){_reachEdges[i]=VillageModels.Rect(ProductionWorldNames.Reach+"Edge"+i,_canvas,Vector2.zero,Vector2.zero,ReachEdge);_reachEdges[i].enabled=false;}
        }

        public void Render(VillageView view,Input input)
        {
            _dashUsed=0;
            var nodes=view?.Nodes.ToDictionary(n=>n.Id)??new Dictionary<int,VillageNodeView>();

            // 1. 届く範囲。配置中は置こうとしている場所、そうでなければ選んでいる施設
            var reachShown=false;
            if(input.Placing&&input.Preview!=null&&input.Preview.Reach>0)
                reachShown=Diamond(Center(input.Kind,input.X,input.Y),input.Preview.Reach);
            else if(input.SelectedId!=0&&input.SelectedReach>0&&nodes.TryGetValue(input.SelectedId,out var sel))
                reachShown=Diamond(Center(sel.Kind,sel.X,sel.Y),input.SelectedReach);
            if(!reachShown){_reachFill.enabled=false;foreach(var e in _reachEdges)e.enabled=false;}

            // 2. 配置中に、つながる相手へ点線を引く (送ってくる相手は水色、送る先は黄)
            if(input.Placing&&input.Preview!=null)
            {
                var here=Center(input.Kind,input.X,input.Y);
                foreach(var id in input.Preview.Sources)if(nodes.TryGetValue(id,out var n))Dashed(Center(n.Kind,n.X,n.Y),here,InLink);
                foreach(var id in input.Preview.Targets)if(nodes.TryGetValue(id,out var n))Dashed(here,Center(n.Kind,n.X,n.Y),OutLink);
            }
            for(var i=_dashUsed;i<_dashes.Count;i++)if(_dashes[i].enabled)_dashes[i].enabled=false;

            // 3. あと 1 棟で施設群になる場所。配置中は、置こうとしている施設と同じ種類の案内だけを強く出す
            var hints=input.Industrial&&input.Hints!=null?input.Hints.Take(3).ToArray():new VillageGroupHint[0];
            var pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*3f);
            var shownGhosts=new HashSet<(FacilityKind,int)>();
            for(var i=0;i<hints.Length;i++)
            {
                var h=hints[i];var size=IslandCatalog.Get(h.Kind).Size;
                var match=input.Placing&&input.Kind==h.Kind;
                var hit=match&&input.X==h.X&&input.Y==h.Y;
                var faded=input.Placing&&!match;
                var pad=Pad(i);pad.enabled=true;
                pad.rectTransform.anchoredPosition=VillageWorldRenderer.Flat(VillageWorldRenderer.Position(h.X,h.Y,size));
                pad.rectTransform.sizeDelta=Vector2.one*size*VillageWorldRenderer.Cell*.96f;
                pad.color=new Color(Gold.r,Gold.g,Gold.b,faded?.06f:hit?.55f:.16f+.2f*pulse);
                if(faded||hit)continue;
                var key=(h.Kind,i);shownGhosts.Add(key);
                if(!_ghosts.TryGetValue(key,out var ghost))
                {
                    _ghosts[key]=ghost=VillageModels.Spawn(_catalog?.Get(h.Kind),size*VillageWorldRenderer.Cell*.7f,_root,ProductionWorldNames.HintGhost+h.Kind+i);
                    VillageModels.Tint(ghost,new Color(1f,.93f,.62f,1f));
                }
                ghost.SetActive(true);
                ghost.transform.position=VillageWorldRenderer.Position(h.X,h.Y,size)+Vector3.up*(.12f+.06f*pulse);
                ghost.transform.rotation=Quaternion.Euler(0,Time.unscaledTime*25f,0);
            }
            for(var i=hints.Length;i<_pads.Count;i++)_pads[i].enabled=false;
            foreach(var kv in _ghosts)if(!shownGhosts.Contains(kv.Key)&&kv.Value.activeSelf)kv.Value.SetActive(false);
        }

        private static Vector3 Center(FacilityKind kind,int x,int y)=>VillageWorldRenderer.Position(x,y,IslandCatalog.Get(kind).Size);

        /// <summary>中心から盤面のマス数 reach 以内 (マンハッタン距離) のひし形。配送の距離は施設の左下の角どうしで測るので目安として出す。</summary>
        private bool Diamond(Vector3 center,int reach)
        {
            var c=VillageWorldRenderer.Flat(center);var r=reach*VillageWorldRenderer.Cell;
            var side=r*Mathf.Sqrt(2);
            _reachFill.enabled=true;var rt=_reachFill.rectTransform;rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=c;rt.sizeDelta=Vector2.one*side;rt.localRotation=Quaternion.Euler(0,0,45);
            var pts=new[]{c+Vector2.right*r,c+Vector2.up*r,c+Vector2.left*r,c+Vector2.down*r};
            for(var i=0;i<4;i++){_reachEdges[i].enabled=true;VillageModels.Line(_reachEdges[i],pts[i],pts[(i+1)%4],EdgeWidth);}
            return true;
        }

        private void Dashed(Vector3 from,Vector3 to,Color color)
        {
            var a=VillageWorldRenderer.Flat(from);var b=VillageWorldRenderer.Flat(to);var d=b-a;var len=d.magnitude;if(len<.05f)return;d/=len;
            var offset=Mathf.Repeat(Time.unscaledTime*.4f,Dash+Gap);
            for(var t=offset;t<len;t+=Dash+Gap)
            {
                var dash=DashAt(_dashUsed++);dash.enabled=true;dash.color=color;
                VillageModels.Line(dash,a+d*t,a+d*Mathf.Min(len,t+Dash),DashWidth);
            }
        }

        private Image DashAt(int i)
        {
            while(_dashes.Count<=i)_dashes.Add(VillageModels.Rect(ProductionWorldNames.GuideLink+_dashes.Count,_canvas,Vector2.zero,Vector2.zero,Color.white));
            return _dashes[i];
        }

        private Image Pad(int i)
        {
            while(_pads.Count<=i)_pads.Add(VillageModels.Rect(ProductionWorldNames.HintPad+_pads.Count,_canvas,Vector2.zero,Vector2.zero,Gold));
            return _pads[i];
        }
    }
}
