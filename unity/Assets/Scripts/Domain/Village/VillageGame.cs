using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    public sealed class VillageResult
    {
        public bool Success {get; internal set;} public string Reason {get; internal set;}
        public int Id {get; internal set;}
    }
    public sealed class VillageNodeView
    {
        public int Id {get; internal set;} public FacilityKind Kind {get; internal set;}
        public int Effort {get; internal set;} public int WarehouseBaseCapacity {get; internal set;} public int Capacity {get; internal set;}
        public double ProductionSeconds {get; internal set;}
        public int ComboCount {get; internal set;}
        public VillageComboDirection ComboDirections {get; internal set;}
        public double ComboMultiplier {get; internal set;}
        public VillageEffortView EffortSettings {get; internal set;}
        public int X {get; internal set;} public int Y {get; internal set;} public int Total {get; internal set;}
        public int Zoom {get; internal set;} = 1;
        public int FootprintSize {get; internal set;}
        public bool Formed {get; internal set;} = true;
        public int Size=>FootprintSize > 0 ? FootprintSize : IslandCatalog.Get(Kind).Size;
        public double Progress {get; internal set;} public string Status {get; internal set;}
        public bool Amplified {get; internal set;} public bool InSynergy {get; internal set;}
        public int CoverageRadius {get; internal set;}
        public IReadOnlyDictionary<Item,int> Stock {get; internal set;}
    }
    public sealed class VillageSynergyView
    {
        public string Name {get; internal set;} public Item BonusItem {get; internal set;}
        public int CoreNodeId {get; internal set;} public IReadOnlyList<int> NodeIds {get; internal set;}
        public int MinX {get; internal set;} public int MinY {get; internal set;}
        public int Width {get; internal set;} public int Height {get; internal set;}
        public bool Active {get; internal set;} public double BonusPerMinute {get; internal set;}
        public string Status {get; internal set;}
    }
    public sealed class VillageItemRateView
    {
        public Item Item {get; internal set;} public int Stock {get; internal set;}
        public double ActualMakePerMinute {get; internal set;} public double ActualUsePerMinute {get; internal set;}
        public double TheoreticalMakePerMinute {get; internal set;} public double TheoreticalUsePerMinute {get; internal set;}
        public bool IsDeficit=>TheoreticalUsePerMinute>TheoreticalMakePerMinute+0.05;
    }
    public sealed class VillageItemTrend
    {
        public int[] Made {get; internal set;}
        public int[] Used {get; internal set;}
    }
    public sealed class VillageCargoView
    {
        public long Id {get; internal set;} public Item Item {get; internal set;}
        public int From {get; internal set;} public int To {get; internal set;}
        public int FromX {get; internal set;} public int FromY {get; internal set;}
        public int ToX {get; internal set;} public int ToY {get; internal set;}
        public long Start {get; internal set;} public long End {get; internal set;}
    }
    public sealed class VillageRouteView
    {
        public int From {get; internal set;} public int To {get; internal set;}
    }
    public sealed class VillageView
    {
        public IReadOnlyList<VillageNodeView> Nodes {get; internal set;}
        public IReadOnlyList<VillageNodeView> UpperNodes {get; internal set;} = Array.Empty<VillageNodeView>();
        public long DeliveryPoints {get; internal set;}
        public long SecondTierDelivered {get; internal set;}
        public int Rank => VillageHierarchyRules.Rank(SecondTierDelivered);
        public int MaximumZoom => VillageHierarchyRules.MaxZoom(SecondTierDelivered);
        public float RankProgress => Rank >= VillageHierarchyRules.MaxRank ? 1 : (SecondTierDelivered % VillageHierarchyRules.SecondTierPerRank) / (float)VillageHierarchyRules.SecondTierPerRank;
        public VillageNodeView Hit(int x,int y) => Nodes.LastOrDefault(n=>x>=n.X&&x<n.X+n.Size&&y>=n.Y&&y<n.Y+n.Size);
        public VillageView AtZoom(int zoom)
        {
            VillageHierarchyRules.GridSize(zoom);
            var view = (VillageView)MemberwiseClone();
            view.Nodes = zoom == 1 ? Nodes : Array.AsReadOnly(UpperNodes.Where(n => n.Zoom == zoom && n.Formed).ToArray());
            if (zoom > 1) { view.Cargo = Array.Empty<VillageCargoView>(); view.Routes = Array.Empty<VillageRouteView>(); view.Synergies = Array.Empty<VillageSynergyView>(); }
            return view;
        }
        public IReadOnlyDictionary<FacilityKind,double> BuildElectricityCosts {get; internal set;}
        public double BuildElectricityCost(FacilityKind kind) => BuildElectricityCosts != null && BuildElectricityCosts.TryGetValue(kind,out var cost)
            ? cost : IslandCatalog.Get(kind).ElectricityCost;
        public double Electricity {get; internal set;} public long Produced {get; internal set;}
        public bool GroupViewActive {get; internal set;}
        public bool Unlimited {get; internal set;} public int BoardSize {get; internal set;}
        /// <summary>ガス・電気・水を減らさないデバッグのモード。材料と物流は通常どおり。</summary>
        public bool FreeResources {get; internal set;}
        /// <summary>ガス・電気・水が足りなくならない (開発用の無制限か、資源無限のモード)。</summary>
        public bool ResourcesUnlimited=>Unlimited||FreeResources;
        public long AtMs {get; internal set;} public double Gas {get; internal set;} public double Water {get; internal set;}
        public double GasPerMinute {get; internal set;} public double GasSpent {get; internal set;}
        public double TheoreticalGasPerMinute {get; internal set;}
        public double GasReceivedToday {get; internal set;}
        public long ProducedLastMinute {get; internal set;}
        public string BottleneckSummary {get; internal set;} public bool HasBottleneck {get; internal set;}
        public IReadOnlyList<VillageCargoView> Cargo {get; internal set;}
        public IReadOnlyList<VillageRouteView> Routes {get; internal set;}
        public IReadOnlyList<VillageSynergyView> Synergies {get; internal set;}
        public IReadOnlyDictionary<Item,int> Totals {get; internal set;}
        public IReadOnlyDictionary<Item,VillageItemRateView> ItemRates {get; internal set;}
        public IReadOnlyDictionary<Item,VillageItemTrend> Trend10 {get; internal set;}
        public IReadOnlyDictionary<Item,VillageItemTrend> Trend60 {get; internal set;}
    }
    public sealed partial class VillageGame
    {
        private sealed class Node
        {
            internal int Id,X,Y,Effort,WarehouseCapacity,ProcessYield,ComboRemainder;
            internal int Zoom=1; internal bool Formed; internal int[] Children=Array.Empty<int>();internal FacilityKind Kind;
            internal readonly Dictionary<Item,int> Stock=new Dictionary<Item,int>();
            internal long Started,Finish;internal string Status="待機";
            internal int Held(Item item)=>Stock.TryGetValue(item,out var n)?n:0;
            internal int Size=>Zoom == 1 ? IslandCatalog.Get(Kind).Size : VillageHierarchyRules.GridSize(Zoom);
        }
        private readonly List<Node> _nodes=new List<Node>();
        private readonly List<VillageCargoView> _cargo=new List<VillageCargoView>();
        private readonly List<(long At,double Gas,int Made)> _history=new List<(long,double,int)>();
        internal sealed class TrendBucket
        {
            internal long Minute;
            internal double Gas;
            internal readonly int[] Made=new int[8];
            internal readonly int[] Used=new int[8];
        }
        internal readonly Dictionary<long,TrendBucket> _trend=new Dictionary<long,TrendBucket>();
        private TrendBucket GetOrCreateTrendBucket(long at)
        {
            var m=at/60000;
            if(!_trend.TryGetValue(m,out var b)){b=new TrendBucket{Minute=m};_trend[m]=b;}
            return b;
        }
        internal void RecordMade(Item item,int count){if(count>0)GetOrCreateTrendBucket(_at).Made[(int)item]+=count;}
        internal void RecordUsed(Item item,int count){if(count>0)GetOrCreateTrendBucket(_at).Used[(int)item]+=count;}
        internal void RecordGas(double gas){if(gas>0)GetOrCreateTrendBucket(_at).Gas+=gas;}
        private long _at,_requested,_nextCargo=1,_produced;
        private int _nextId=1,_deliveryMs=1500;private double _speed=1,_gas,_electricity,_water,_spent;
        private bool _unlimited,_freeResources;
        public const int BoardSize=24;
        public bool FreeResources=>_freeResources;
        /// <summary>ガス・電気・水を減らさないデバッグのモード (審査で遊べるようにする)。材料・在庫・物流の規則は変えない。</summary>
        public void SetFreeResources(bool on){_freeResources=on;}
        private bool PaysResources=>!_unlimited&&!_freeResources;
        public VillageGame(bool unlimited){_unlimited=unlimited;}
        private static VillageResult Result(bool ok,string reason,int id=0)=>new VillageResult{Success=ok,Reason=reason,Id=id};
        public void Configure(bool unlimited,double speed,int deliveryMs)
        {
            if(!Finite(speed)||speed<.01||speed>100||deliveryMs<100||deliveryMs>3600000)throw new ArgumentOutOfRangeException();
            _unlimited=unlimited;_speed=speed;_deliveryMs=deliveryMs;
        }
        public static bool Supported(FacilityKind kind)=>Enum.IsDefined(typeof(FacilityKind),kind);
        public string CanPlace(FacilityKind kind,int x,int y)=>!Supported(kind)?"未対応の施設です":Placement(kind,x,y,0);
        internal static int CoverageRadiusOf(FacilityKind kind)=>
            kind==FacilityKind.ElectricAmplifier||kind==FacilityKind.WaterAmplifier||kind==FacilityKind.GasAmplifier||kind==FacilityKind.IceHouse||kind==FacilityKind.Sorter?6:0;
        private bool IsAmplified(Node target)=>AmplifierBonus(target)>0;
        private int NodeCapacity(Node n)=>(int)((n.Kind==FacilityKind.Warehouse?WarehouseBaseCapacity(n):n.Kind==FacilityKind.Sorter?20:12)*CoolingMultiplier(n));
        private int OutputCap(Node n)=>(int)(6*CoolingMultiplier(n));
        private double NodeSpeedMultiplier(Node n)
        {
            var mult=1.0;
            mult+=AmplifierBonus(n);
            mult+=.10*_nodes.Count(a=>a.Kind==FacilityKind.Dormitory);
            mult+=.15*_nodes.Count(a=>a.Kind==FacilityKind.Research);
            return mult*VillageEffortRules.Speed(n.Effort);
        }
        private string Placement(FacilityKind kind,int x,int y,int ignore)
        {
            if(!Enum.IsDefined(typeof(FacilityKind),kind))return "未対応の施設です";
            var size=IslandCatalog.Get(kind).Size;
            if(x<0||y<0||x>BoardSize-size||y>BoardSize-size)return "敷地の外です";
            if(_nodes.Any(n=>n.Id!=ignore&&x<n.X+n.Size&&x+size>n.X&&y<n.Y+n.Size&&y+size>n.Y))return "ほかの施設と重なっています";
            return "";
        }
        public string CanMove(int id,int x,int y)
        {
            var n=_nodes.FirstOrDefault(a=>a.Id==id);if(n==null)return "施設がありません";
            return _cargo.Any(c=>c.To==id)?"入庫する荷物の到着を待ってください":Placement(n.Kind,x,y,id);
        }
        public VillageResult Place(FacilityKind kind,int x,int y)
        {
            var reason=CanPlace(kind,x,y);if(reason.Length>0)return Result(false,reason);
            var d=IslandCatalog.Get(kind);
            var cost=NextBuildElectricityCost(kind);
            if(_buildCounts[(int)kind]==int.MaxValue)return Result(false,"建設数の上限です");
            if(PaysResources&&(_electricity<cost||_water<d.WaterCost))return Result(false,"建設に使う資源が足りません");
            if(PaysResources){_electricity-=cost;_water-=d.WaterCost;}
            _buildCounts[(int)kind]++;
            var n=new Node{Id=_nextId++,Kind=kind,X=x,Y=y};_nodes.Add(n);ReconcileHierarchy();return Result(true,d.Name+"を建てました",n.Id);
        }
        public VillageResult Move(int id,int x,int y)
        {
            var reason=CanMove(id,x,y);if(reason.Length>0)return Result(false,reason);
            var n=_nodes.First(a=>a.Id==id);n.X=x;n.Y=y;ReconcileHierarchy();return Result(true,"施設を移動しました",id);
        }
        public VillageResult Remove(int id)
        {
            var n=_nodes.FirstOrDefault(a=>a.Id==id);if(n==null)return Result(false,"施設がありません");
            if(_cargo.Any(c=>c.To==id||c.From==id)||n.Finish>0)return Result(false,"配送・加工の完了を待ってください");
            if(!_unlimited&&n.Stock.Values.Sum()>0)return Result(false,"在庫を先に出荷してください");
            _nodes.Remove(n);ReconcileHierarchy();return Result(true,"施設を撤去しました");
        }
        public void SetResources(double gas,double electricity,double water)
        {
            if(new[]{gas,electricity,water}.Any(n=>!Finite(n)||n<0||n>int.MaxValue))throw new ArgumentOutOfRangeException();
            _gas=gas;_electricity=electricity;_water=water;
        }
        public void AdvanceTo(long milliseconds)
        {
            if(milliseconds<_requested||milliseconds>315360000000L)throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _requested=milliseconds;
            while(_at+100<=milliseconds)
            {
                var before=_produced;var spent=_spent;var cargo=_nextCargo;var arriving=_cargo.Count;
                Step();
                // If nothing started, completed or departed, no state can change until the next paid job.
                if(before==_produced&&spent==_spent&&cargo==_nextCargo&&arriving==_cargo.Count)
                {
                    var next=_nodes.Where(n=>n.Finish>_at).Select(n=>n.Finish).Concat(_upperNodes.Where(n=>n.Finish>_at).Select(n=>n.Finish)).Concat(_cargo.Select(c=>c.End)).DefaultIfEmpty(milliseconds+100).Min();
                    if(_groupViewActive)next=Math.Min(next,_at+VillageGroupRules.IntervalMs-_groupElapsed);
                    var jump=Math.Min(milliseconds/100*100,((next+99)/100-1)*100);
                    if(jump>_at){AdvanceGroupClock(jump-_at);_at=jump;}
                }
            }
            _history.RemoveAll(e=>e.At<=_at-60000);
            var minMinute=_at/60000-59;var expired=_trend.Keys.Where(m=>m<minMinute).ToArray();foreach(var k in expired)_trend.Remove(k);
        }
        private static Recipe RecipeOf(Node n)=>IslandCatalog.Get(n.Kind).Recipes.FirstOrDefault();
        private List<VillageRouteView> Routes()
        {
            var routes=new List<VillageRouteView>();
            foreach(var a in _nodes)
            {
                if(RecipeOf(a)==null)continue;
                foreach(var b in _nodes.Where(n=>n.Id!=a.Id).OrderBy(n=>n.Kind==FacilityKind.Warehouse?1:0).ThenBy(n=>Math.Abs(n.X-a.X)+Math.Abs(n.Y-a.Y)))
                    if(Links(a,b,_nodes))routes.Add(new VillageRouteView{From=a.Id,To=b.Id});
            }
            return routes;
        }
        /// <summary>送り元 a から自動配送が届くマンハッタン距離。近く (6 以内) に仕分け所があれば、配送距離 12、なければ 8。</summary>
        private static int ReachOf(Node a,IReadOnlyCollection<Node> all)=>all
            .Any(s=>s.Kind==FacilityKind.Sorter&&Math.Abs(s.X-a.X)+Math.Abs(s.Y-a.Y)<=6)?12:8;
        /// <summary>a から b へ自動配送の道ができるか (配送の規則の唯一の判定)。</summary>
        private static bool Links(Node a,Node b,IReadOnlyCollection<Node> all)=>
            a.Id!=b.Id&&RecipeOf(a)!=null&&Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)<=ReachOf(a,all)&&SendableItems(a).Any(i=>Accepts(b,i));
        private void Step()
        {
            _at+=100;
            var candidates=new List<(Node From,Node To,Item Item)>();
            var available=_nodes.ToDictionary(n=>n.Id,n=>new Dictionary<Item,int>(n.Stock));
            var reserved=_nodes.ToDictionary(n=>n.Id,n=>_cargo.Count(c=>c.To==n.Id));
            foreach(var route in Routes())
            {
                var a=_nodes.First(n=>n.Id==route.From);var b=_nodes.First(n=>n.Id==route.To);
                foreach(var item in SendableItems(a).Distinct().Where(i=>Accepts(b,i)))
                {
                    if(_cargo.Any(c=>c.From==a.Id&&c.To==b.Id)||candidates.Any(c=>c.From.Id==a.Id&&c.To.Id==b.Id)||!available[a.Id].TryGetValue(item,out var count)||count<=0)continue;
                    var capacity=NodeCapacity(b);
                    if(b.Stock.Values.Sum()+reserved[b.Id]+PendingOutput(b)>=capacity)continue;
                    available[a.Id][item]--;reserved[b.Id]++;candidates.Add((a,b,item));
                }
            }
            var starting=_nodes.Where(n=>n.Finish==0).ToArray();
            foreach(var c in _cargo.Where(c=>c.End<=_at).ToArray())
            {var n=_nodes.First(a=>a.Id==c.To);n.Stock[c.Item]=n.Held(c.Item)+1;_cargo.Remove(c);}
            foreach(var n in _nodes.Where(n=>n.Finish>0&&n.Finish<=_at))
            {var r=RecipeOf(n);var output=n.ProcessYield;n.Stock[r.Output.Item]=n.Held(r.Output.Item)+output;n.Finish=0;n.ProcessYield=0;_produced+=output;_history.Add((_at,0,output));RecordMade(r.Output.Item,output);}
            foreach(var n in starting)
            {
                var r=RecipeOf(n);
                if(r==null)
                {
                    n.Status=n.Kind==FacilityKind.Warehouse?"受け取り待ち"
                        :SupportStatus(n);
                    continue;
                }
                var arrivals=reserved[n.Id];
                var consumed=_unlimited?0:r.Inputs.Sum(i=>i.Count);
                var quarters=OutputQuarters(n);var output=quarters/4;
                if(n.Held(r.Output.Item)+output>OutputCap(n)||n.Stock.Values.Sum()-consumed+arrivals+output>NodeCapacity(n)){n.Status="出力の置き場が満杯";continue;}
                if(!_unlimited&&r.Inputs.Any(i=>n.Held(i.Item)<i.Count)){n.Status="材料を待っています";continue;}
                if(!Pay(VillageEffortRules.Gas(n.Effort))){n.Status="ガスを待っています";continue;}
                if(!_unlimited)foreach(var input in r.Inputs){n.Stock[input.Item]=n.Held(input.Item)-input.Count;RecordUsed(input.Item,input.Count);}
                n.ProcessYield=output;n.ComboRemainder=quarters%4;
                var effectiveSpeed=_speed*NodeSpeedMultiplier(n);
                n.Started=_at;n.Finish=_at+(long)Math.Max(100,r.Seconds*1000/effectiveSpeed);n.Status="生産中";
            }
            foreach(var d in candidates)
            {
                var cost=Math.Max(1,(Math.Abs(d.From.X-d.To.X)+Math.Abs(d.From.Y-d.To.Y)+3)/4);
                if(!Pay(cost))continue;
                d.From.Stock[d.Item]=d.From.Held(d.Item)-1;
                _cargo.Add(new VillageCargoView{Id=_nextCargo++,From=d.From.Id,To=d.To.Id,Item=d.Item,FromX=d.From.X,FromY=d.From.Y,
                    ToX=d.To.X,ToY=d.To.Y,Start=_at,End=_at+_deliveryMs});
            }
            StepHierarchy();
            AdvanceGroupClock(100);
            _history.RemoveAll(e=>e.At<=_at-60000);
            var minMinute=_at/60000-59;var expired=_trend.Keys.Where(m=>m<minMinute).ToArray();foreach(var k in expired)_trend.Remove(k);
        }
        private bool Pay(double gas)
        {
            if(PaysResources&&_gas<gas)return false;
            if(PaysResources)_gas-=gas;
            // 資源無限でも消費の記録は残し、生産レポートの消費量を通常と同じに見せる。
            if(!_unlimited)RecordGas(gas);
            _spent+=gas;_history.Add((_at,gas,0));return true;
        }
        public VillageView CreateView(int today=-1)
        {
            var totals=Enum.GetValues(typeof(Item)).Cast<Item>().ToDictionary(i=>i,i=>_nodes.Concat(_upperNodes).Sum(n=>n.Held(i))+_cargo.Count(c=>c.Item==i));
            var synergies=FindSynergies();
            var synergyNodeIds=new HashSet<int>(synergies.SelectMany(s=>s.NodeIds));
            var rates=new Dictionary<Item,VillageItemRateView>();
            var theoGasPerMin=0.0;
            var curMin=_at/60000;
            _trend.TryGetValue(curMin,out var curBucket);
            foreach(var item in Enum.GetValues(typeof(Item)).Cast<Item>())
            {
                rates[item]=new VillageItemRateView{
                    Item=item,Stock=totals[item],
                    ActualMakePerMinute=curBucket!=null?curBucket.Made[(int)item]:0,
                    ActualUsePerMinute=curBucket!=null?curBucket.Used[(int)item]:0
                };
            }
            foreach(var n in _nodes)
            {
                var r=RecipeOf(n);if(r==null)continue;
                var cyclesPerMin=60.0/Math.Max(0.1,r.Seconds)*_speed*NodeSpeedMultiplier(n);
                rates[r.Output.Item].TheoreticalMakePerMinute+=r.Output.Count*VillageComboRules.Multiplier(ComboCount(n))*cyclesPerMin;
                theoGasPerMin+=cyclesPerMin*VillageEffortRules.Gas(n.Effort);
                foreach(var inp in r.Inputs)
                    rates[inp.Item].TheoreticalUsePerMinute+=inp.Count*cyclesPerMin;
            }
            foreach(var n in _upperNodes.Where(n=>n.Formed))
            {
                var recipe=RecipeOf(n);var cycles=60/recipe.Seconds*UpperSpeed(n);
                rates[recipe.Output.Item].TheoreticalMakePerMinute+=recipe.Output.Count*VillageComboRules.Multiplier(VillageComboRules.Count(UpperComboDirections(n)))*cycles;
                foreach(var input in recipe.Inputs)rates[input.Item].TheoreticalUsePerMinute+=input.Count*cycles;
                theoGasPerMin+=cycles*VillageEffortRules.Gas(n.Effort);
            }
            foreach(var syn in synergies)
            {
                if(_groupViewActive)rates[syn.BonusItem].TheoreticalMakePerMinute+=VillageGroupRules.ItemsPerMinute;
            }
            var trend10=new Dictionary<Item,VillageItemTrend>();
            var trend60=new Dictionary<Item,VillageItemTrend>();
            foreach(var item in Enum.GetValues(typeof(Item)).Cast<Item>())
            {
                var m10=new int[10];var u10=new int[10];
                for(var i=0;i<10;i++)
                {
                    var mIdx=curMin-(9-i);
                    if(_trend.TryGetValue(mIdx,out var b)){m10[i]=b.Made[(int)item];u10[i]=b.Used[(int)item];}
                }
                trend10[item]=new VillageItemTrend{Made=m10,Used=u10};

                var m60=new int[60];var u60=new int[60];
                for(var i=0;i<60;i++)
                {
                    var mIdx=curMin-(59-i);
                    if(_trend.TryGetValue(mIdx,out var b)){m60[i]=b.Made[(int)item];u60[i]=b.Used[(int)item];}
                }
                trend60[item]=new VillageItemTrend{Made=m60,Used=u60};
            }
            var targetDay=today<0?_ledger.LastStepDay:today;
            var gasReceivedToday=_ledger.Entries.Where(e=>e.Day==targetDay&&e.Kind=="歩数 → ガス").Sum(e=>e.Amount);
            var activeNodes=_nodes.Concat(_upperNodes.Where(n=>n.Formed)).ToArray();
            var gasWait=activeNodes.Count(n=>n.Status=="ガスを待っています");
            var matWait=activeNodes.Count(n=>n.Status=="材料を待っています");
            var fullWait=activeNodes.Count(n=>n.Status=="出力の置き場が満杯");
            var deficitItem=rates.Values.FirstOrDefault(v=>v.IsDeficit);
            var hasBottleneck=gasWait>0||matWait>0||fullWait>0||deficitItem!=null;
            var bottleneckSummary=!_nodes.Any()?"施設を配置して生産ラインを作りましょう"
                :gasWait>0?$"ガス不足で {gasWait} 施設が待機中です（歩数記録またはガス補充が必要）"
                :deficitItem!=null?$"「{IslandCatalog.NameOf(deficitItem.Item)}」の理論消費（{deficitItem.TheoreticalUsePerMinute:0.#}/分）に対し供給（{deficitItem.TheoreticalMakePerMinute:0.#}/分）が不足しています"
                :matWait>0?$"材料待ちの施設が {matWait} 棟あります（上流施設または仕分け機を近くに配置）"
                :fullWait>0?$"出力満杯の施設が {fullWait} 棟あります（倉庫または氷室を近くに配置）"
                :"すべての生産・物流ラインが順調に稼働しています";
            return new VillageView{DeliveryPoints=_deliveryPoints,SecondTierDelivered=_secondTierDelivered,UpperNodes=Array.AsReadOnly(_upperNodes.Select(UpperView).ToArray()),GroupViewActive=_groupViewActive,Unlimited=_unlimited,FreeResources=_freeResources,BoardSize=BoardSize,AtMs=_at,Gas=_gas,Electricity=_electricity,Water=_water,
                BuildElectricityCosts=new ReadOnlyDictionary<FacilityKind,double>(IslandCatalog.Facilities.ToDictionary(d=>d.Kind,d=>NextBuildElectricityCost(d.Kind))),
                Produced=_produced,GasSpent=_spent,GasPerMinute=_history.Sum(e=>e.Gas),TheoreticalGasPerMinute=theoGasPerMin,
                GasReceivedToday=gasReceivedToday,
                ProducedLastMinute=_history.Sum(e=>(long)e.Made),
                BottleneckSummary=bottleneckSummary,HasBottleneck=hasBottleneck,
                Nodes=Array.AsReadOnly(_nodes.Select(n=>new VillageNodeView{Id=n.Id,Kind=n.Kind,X=n.X,Y=n.Y,Total=n.Stock.Values.Sum(),
                    Effort=n.Effort,WarehouseBaseCapacity=n.Kind==FacilityKind.Warehouse?WarehouseBaseCapacity(n):0,Capacity=NodeCapacity(n),EffortSettings=EffortView(n),
                    ProductionSeconds=RecipeOf(n)==null?0:Math.Max(.1,RecipeOf(n).Seconds/(_speed*NodeSpeedMultiplier(n))),
                    ComboCount=ComboCount(n),ComboDirections=ComboDirections(n),ComboMultiplier=VillageComboRules.Multiplier(ComboCount(n)),
                    Status=n.Status,Amplified=IsAmplified(n),InSynergy=synergyNodeIds.Contains(n.Id),CoverageRadius=CoverageRadiusOf(n.Kind),
                    Progress=n.Finish>0?Math.Min(1,(_at-n.Started)/(double)(n.Finish-n.Started)):0,
                    Stock=new ReadOnlyDictionary<Item,int>(new Dictionary<Item,int>(n.Stock))}).ToArray()),
                Routes=Array.AsReadOnly(Routes().ToArray()),Cargo=Array.AsReadOnly(_cargo.ToArray()),
                Synergies=Array.AsReadOnly(synergies.ToArray()),
                Totals=new ReadOnlyDictionary<Item,int>(totals),
                ItemRates=new ReadOnlyDictionary<Item,VillageItemRateView>(rates),
                Trend10=new ReadOnlyDictionary<Item,VillageItemTrend>(trend10),
                Trend60=new ReadOnlyDictionary<Item,VillageItemTrend>(trend60)};
        }
    }
}
