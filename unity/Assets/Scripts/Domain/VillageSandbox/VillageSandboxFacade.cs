using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.VillageSandbox
{
    public sealed class SandboxResult
    {
        public bool Success {get; internal set;} public string Reason {get; internal set;}
        public int Id {get; internal set;}
    }
    public sealed class SandboxNodeView
    {
        public int Id {get; internal set;} public FacilityKind Kind {get; internal set;}
        public int X {get; internal set;} public int Y {get; internal set;} public int Total {get; internal set;}
        public int Size=>IslandCatalog.Get(Kind).Size;
        public double Progress {get; internal set;} public string Status {get; internal set;}
        public IReadOnlyDictionary<Item,int> Stock {get; internal set;}
    }
    public sealed class SandboxCargoView
    {
        public long Id {get; internal set;} public Item Item {get; internal set;}
        public int From {get; internal set;} public int To {get; internal set;}
        public int FromX {get; internal set;} public int FromY {get; internal set;}
        public int ToX {get; internal set;} public int ToY {get; internal set;}
        public long Start {get; internal set;} public long End {get; internal set;}
    }
    public sealed class SandboxRouteView
    {
        public int From {get; internal set;} public int To {get; internal set;}
    }
    public sealed class SandboxView
    {
        public IReadOnlyList<SandboxNodeView> Nodes {get; internal set;}
        public double Electricity {get; internal set;} public long Produced {get; internal set;}
        public bool Unlimited {get; internal set;} public int BoardSize {get; internal set;}
        public long AtMs {get; internal set;} public double Gas {get; internal set;} public double Water {get; internal set;}
        public double GasPerMinute {get; internal set;} public double GasSpent {get; internal set;}
        public long ProducedLastMinute {get; internal set;}
        public IReadOnlyList<SandboxCargoView> Cargo {get; internal set;}
        public IReadOnlyList<SandboxRouteView> Routes {get; internal set;}
        public IReadOnlyDictionary<Item,int> Totals {get; internal set;}
    }
    public sealed class VillageSandboxFacade
    {
        private sealed class Node
        {
            internal int Id,X,Y;internal FacilityKind Kind;
            internal readonly Dictionary<Item,int> Stock=new Dictionary<Item,int>();
            internal long Started,Finish;internal string Status="待機";
            internal int Held(Item item)=>Stock.TryGetValue(item,out var n)?n:0;
            internal int Size=>IslandCatalog.Get(Kind).Size;
        }
        private readonly List<Node> _nodes=new List<Node>();
        private readonly List<SandboxCargoView> _cargo=new List<SandboxCargoView>();
        private readonly List<(long At,double Gas,int Made)> _history=new List<(long,double,int)>();
        private long _at,_requested,_nextCargo=1,_produced;
        private int _nextId=1,_deliveryMs=1500;private double _speed=1,_gas,_electricity,_water,_spent;
        private bool _unlimited;
        public const int BoardSize=24;
        public VillageSandboxFacade(bool unlimited){_unlimited=unlimited;}
        private static SandboxResult Result(bool ok,string reason,int id=0)=>new SandboxResult{Success=ok,Reason=reason,Id=id};
        public void Configure(bool unlimited,double speed,int deliveryMs)
        {
            if(double.IsNaN(speed)||double.IsInfinity(speed)||speed<=0||deliveryMs<100)throw new ArgumentOutOfRangeException();
            _unlimited=unlimited;_speed=speed;_deliveryMs=deliveryMs;
        }
        public string CanPlace(FacilityKind kind,int x,int y)=>Placement(kind,x,y,0);
        private string Placement(FacilityKind kind,int x,int y,int ignore)
        {
            if(!Enum.IsDefined(typeof(FacilityKind),kind))return "未対応の施設です";
            var size=IslandCatalog.Get(kind).Size;
            if(x<0||y<0||x+size>BoardSize||y+size>BoardSize)return "敷地の外です";
            if(_nodes.Any(n=>n.Id!=ignore&&x<n.X+n.Size&&x+size>n.X&&y<n.Y+n.Size&&y+size>n.Y))return "ほかの施設と重なっています";
            return "";
        }
        public string CanMove(int id,int x,int y)
        {
            var n=_nodes.FirstOrDefault(a=>a.Id==id);if(n==null)return "施設がありません";
            return _cargo.Any(c=>c.To==id)?"入庫する荷物の到着を待ってください":Placement(n.Kind,x,y,id);
        }
        public SandboxResult Place(FacilityKind kind,int x,int y)
        {
            var reason=CanPlace(kind,x,y);if(reason.Length>0)return Result(false,reason);
            var d=IslandCatalog.Get(kind);
            if(!_unlimited&&(_electricity<d.ElectricityCost||_water<d.WaterCost))return Result(false,"建設に使う資源が足りません");
            if(!_unlimited){_electricity-=d.ElectricityCost;_water-=d.WaterCost;}
            var n=new Node{Id=_nextId++,Kind=kind,X=x,Y=y};_nodes.Add(n);return Result(true,d.Name+"を建てました",n.Id);
        }
        public SandboxResult Move(int id,int x,int y)
        {
            var reason=CanMove(id,x,y);if(reason.Length>0)return Result(false,reason);
            var n=_nodes.First(a=>a.Id==id);n.X=x;n.Y=y;return Result(true,"施設を移動しました",id);
        }
        public SandboxResult Remove(int id)
        {
            var n=_nodes.FirstOrDefault(a=>a.Id==id);if(n==null)return Result(false,"施設がありません");
            if(_cargo.Any(c=>c.To==id||c.From==id)||n.Finish>0)return Result(false,"配送・加工の完了を待ってください");
            if(!_unlimited&&n.Stock.Values.Sum()>0)return Result(false,"在庫を先に出荷してください");
            _nodes.Remove(n);return Result(true,"施設を撤去しました");
        }
        public void SetResources(double gas,double electricity,double water)
        {
            if(new[]{gas,electricity,water}.Any(n=>double.IsNaN(n)||double.IsInfinity(n)||n<0))throw new ArgumentOutOfRangeException();
            _gas=gas;_electricity=electricity;_water=water;
        }
        public void AdvanceTo(long milliseconds)
        {
            if(milliseconds<_requested||milliseconds-_requested>86400000)throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _requested=milliseconds;while(_at+100<=milliseconds)Step();
        }
        private static Recipe RecipeOf(Node n)=>IslandCatalog.Get(n.Kind).Recipes.FirstOrDefault();
        private List<SandboxRouteView> Routes()
        {
            var routes=new List<SandboxRouteView>();
            foreach(var a in _nodes)
            {
                var recipe=RecipeOf(a);if(recipe==null)continue;
                foreach(var b in _nodes.Where(n=>n.Id!=a.Id).OrderBy(n=>n.Kind==FacilityKind.Warehouse?1:0).ThenBy(n=>Math.Abs(n.X-a.X)+Math.Abs(n.Y-a.Y)))
                {
                    var need=RecipeOf(b);var distance=Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y);
                    if(distance<=8&&(b.Kind==FacilityKind.Warehouse||need!=null&&need.Inputs.Any(i=>i.Item==recipe.Output.Item)))
                        routes.Add(new SandboxRouteView{From=a.Id,To=b.Id});
                }
            }
            return routes;
        }
        private void Step()
        {
            _at+=100;
            var candidates=new List<(Node From,Node To,Item Item)>();
            var available=_nodes.ToDictionary(n=>n.Id,n=>new Dictionary<Item,int>(n.Stock));
            var reserved=_nodes.ToDictionary(n=>n.Id,n=>_cargo.Count(c=>c.To==n.Id));
            foreach(var route in Routes())
            {
                var a=_nodes.First(n=>n.Id==route.From);var b=_nodes.First(n=>n.Id==route.To);
                var item=RecipeOf(a).Output.Item;
                if(_cargo.Any(c=>c.From==a.Id&&c.To==b.Id)||!available[a.Id].TryGetValue(item,out var count)||count<=0)continue;
                var capacity=b.Kind==FacilityKind.Warehouse?20:12;
                if(b.Stock.Values.Sum()+reserved[b.Id]>=capacity)continue;
                available[a.Id][item]--;reserved[b.Id]++;candidates.Add((a,b,item));
            }
            var starting=_nodes.Where(n=>n.Finish==0).ToArray();
            foreach(var c in _cargo.Where(c=>c.End<=_at).ToArray())
            {var n=_nodes.First(a=>a.Id==c.To);n.Stock[c.Item]=n.Held(c.Item)+1;_cargo.Remove(c);}
            foreach(var n in _nodes.Where(n=>n.Finish>0&&n.Finish<=_at))
            {var r=RecipeOf(n);n.Stock[r.Output.Item]=n.Held(r.Output.Item)+r.Output.Count;n.Finish=0;_produced+=r.Output.Count;_history.Add((_at,0,r.Output.Count));}
            foreach(var n in starting)
            {
                var r=RecipeOf(n);if(r==null){n.Status=n.Kind==FacilityKind.Warehouse?"受け取り待ち":"配置効果の確認用";continue;}
                if(n.Held(r.Output.Item)+r.Output.Count>6){n.Status="出力の置き場が満杯";continue;}
                if(!_unlimited&&r.Inputs.Any(i=>n.Held(i.Item)<i.Count)){n.Status="材料を待っています";continue;}
                if(!Pay(1)){n.Status="ガスを待っています";continue;}
                if(!_unlimited)foreach(var input in r.Inputs)n.Stock[input.Item]=n.Held(input.Item)-input.Count;
                n.Started=_at;n.Finish=_at+(long)Math.Max(100,r.Seconds*1000/_speed);n.Status="生産中";
            }
            foreach(var d in candidates)
            {
                var cost=Math.Max(1,(Math.Abs(d.From.X-d.To.X)+Math.Abs(d.From.Y-d.To.Y)+3)/4);
                if(!Pay(cost))continue;
                d.From.Stock[d.Item]=d.From.Held(d.Item)-1;
                _cargo.Add(new SandboxCargoView{Id=_nextCargo++,From=d.From.Id,To=d.To.Id,Item=d.Item,FromX=d.From.X,FromY=d.From.Y,
                    ToX=d.To.X,ToY=d.To.Y,Start=_at,End=_at+_deliveryMs});
            }
            _history.RemoveAll(e=>e.At<=_at-60000);
        }
        private bool Pay(double gas)
        {if(!_unlimited&&_gas<gas)return false;if(!_unlimited)_gas-=gas;_spent+=gas;_history.Add((_at,gas,0));return true;}
        public SandboxView CreateView()
        {
            var totals=Enum.GetValues(typeof(Item)).Cast<Item>().ToDictionary(i=>i,i=>_nodes.Sum(n=>n.Held(i))+_cargo.Count(c=>c.Item==i));
            return new SandboxView{Unlimited=_unlimited,BoardSize=BoardSize,AtMs=_at,Gas=_gas,Electricity=_electricity,Water=_water,
                Produced=_produced,GasSpent=_spent,GasPerMinute=_history.Sum(e=>e.Gas),ProducedLastMinute=_history.Sum(e=>(long)e.Made),
                Nodes=Array.AsReadOnly(_nodes.Select(n=>new SandboxNodeView{Id=n.Id,Kind=n.Kind,X=n.X,Y=n.Y,Total=n.Stock.Values.Sum(),
                    Status=n.Status,Progress=n.Finish>0?Math.Min(1,(_at-n.Started)/(double)(n.Finish-n.Started)):0,
                    Stock=new ReadOnlyDictionary<Item,int>(new Dictionary<Item,int>(n.Stock))}).ToArray()),
                Routes=Array.AsReadOnly(Routes().ToArray()),Cargo=Array.AsReadOnly(_cargo.ToArray()),Totals=new ReadOnlyDictionary<Item,int>(totals)};
        }
    }
}
