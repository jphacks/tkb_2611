using System;
using System.Linq;
using ProjectPenguin.Domain.Encounter;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    public sealed partial class VillageGame
    {
        private HealthRecordLedger _ledger = new HealthRecordLedger();
        public HealthRecordLedger Records => _ledger.Copy();
        public long RequestedMs => _requested;
        public static bool Finite(double n) => !double.IsNaN(n)&&!double.IsInfinity(n);
        public VillageResult Place(FacilityKind kind,Cell cell)=>Place(kind,cell.X,cell.Y);
        public VillageResult Move(int id,Cell cell)=>Move(id,cell.X,cell.Y);
        public string CanMove(int id,Cell cell)=>CanMove(id,cell.X,cell.Y);
        public VillageResult Inject(int id,Item item,int count)
        {
            var n=_nodes.FirstOrDefault(a=>a.Id==id);
            if(n==null||!Enum.IsDefined(typeof(Item),item)||count<=0)return Result(false,"施設・品目・個数が不正です");
            var capacity=NodeCapacity(n);
            var pending=n.Finish>0&&RecipeOf(n)!=null?RecipeOf(n).Output.Count:0;
            if((long)n.Stock.Values.Sum()+_cargo.Count(c=>c.To==id)+pending+count>capacity)return Result(false,"容量または入庫・加工の予約を超えます");
            n.Stock[item]=n.Held(item)+count;return Result(true,"品物を投入しました");
        }
        /// <summary>1 歩でガスが入る量。審査で遊べるよう、歩いた分がすぐ工場を動かせる量にしている。</summary>
        public const int GasPerStep=2;
        public VillageResult AcceptSteps(int day,int steps,int today)
        {
            if(day<0||day!=today||day<_ledger.LastStepDay||steps<0||steps>172800)return Result(false,"対象日または歩数が不正です");
            var previous=day==_ledger.LastStepDay?_ledger.MaxSteps:0;
            var maximum=Math.Max(steps,previous);var amount=(maximum-previous)*GasPerStep;
            if(!Finite(_gas+amount)||_gas+amount>int.MaxValue)return Result(false,"資源の上限です");
            _ledger.LastStepDay=day;_ledger.MaxSteps=maximum;_gas+=amount;
            if(amount>0)_ledger.Entries.Add(new HealthRecordEntry{Id="steps:"+day+":"+maximum,Kind="歩数 → ガス",Day=day,Amount=amount});
            return Result(true,amount>0?$"ガス +{amount}":"今日の歩数は受け取り済みです");
        }
        public VillageResult AcceptWater(string id,int day,int today,bool confirmed)
        {
            if(!confirmed||day<0||day!=today||string.IsNullOrWhiteSpace(id)||id.Length>128)return Result(false,"確認済みの本日の記録が必要です");
            return Grant(id,day,"水分補給 → 水",3,true);
        }
        public VillageResult AcceptScreen(string id,int day,int used,int today,bool fullDay,bool confirmed)
        {
            if(!confirmed||!fullDay||today<1||day!=today-1||used<0||used>1440||string.IsNullOrWhiteSpace(id)||id.Length>128)return Result(false,"前日の確定記録が必要です");
            return Grant(id,day,"スクリーンタイム → 電気",(1440-used)/120,false);
        }
        private VillageResult Grant(string id,int day,string kind,int amount,bool water)
        {
            if(_ledger.Entries.Any(e=>e.Id==id||e.Kind==kind&&e.Day==day))return Result(false,"この記録または対象日は受領済みです");
            var balance=(water?_water:_electricity)+amount;if(!Finite(balance)||balance>int.MaxValue)return Result(false,"資源の上限です");
            _ledger.Entries.Add(new HealthRecordEntry{Id=id,Kind=kind,Day=day,Amount=amount});
            if(water)_water=balance;else _electricity=balance;
            return Result(true,kind+" +"+amount);
        }
        private int GroupProgressOf(Item item)=>_groupProgress.TryGetValue(item,out var progress)?progress:0;
        public VillageState Export()
        {
            return new VillageState{EffortSchema=1,At=_at,Requested=_requested,NextId=_nextId,NextCargo=_nextCargo,Produced=_produced,
                GroupProgress=new VillageGroupProgressState{ElapsedMs=_groupElapsed,Tick=_groupTick,Chikuwa=GroupProgressOf(Item.Chikuwa),Kamaboko=GroupProgressOf(Item.Kamaboko),DriedFish=GroupProgressOf(Item.DriedFish)},
                DeliveryMs=_deliveryMs,Speed=_speed,Gas=_gas,Electricity=_electricity,Water=_water,Spent=_spent,Unlimited=_unlimited,FreeResources=_freeResources,Ledger=_ledger.Copy(),Encounters=_encounters.Export(),
                Nodes=_nodes.Select(n=>new VillageNodeState{Id=n.Id,Kind=n.Kind,Effort=n.Effort,WarehouseCapacity=n.WarehouseCapacity,X=n.X,Y=n.Y,Started=n.Started,Finish=n.Finish,
                    Stock=Enum.GetValues(typeof(Item)).Cast<Item>().Select(i=>n.Held(i)).ToArray()}).ToList(),
                Cargo=_cargo.Select(c=>new VillageCargoState{Id=c.Id,From=c.From,To=c.To,Item=c.Item,FromX=c.FromX,FromY=c.FromY,ToX=c.ToX,ToY=c.ToY,Start=c.Start,End=c.End}).ToList(),
                History=_history.Select(e=>new VillageHistoryState{At=e.At,Gas=e.Gas,Made=e.Made}).ToList(),
                Trend=_trend.Values.Select(b=>new VillageTrendState{Minute=b.Minute,Gas=b.Gas,Made=(int[])b.Made.Clone(),Used=(int[])b.Used.Clone()}).ToList()};
        }
        public static VillageGame Restore(VillageState s)
        {
            if(s==null||s.EffortSchema<0||s.EffortSchema>1||s.Nodes==null||s.Cargo==null||s.History==null||s.Ledger?.Entries==null||s.Nodes.Count>576||s.Cargo.Count>6912||s.History.Count>100000||s.Ledger.Entries.Count>100000)throw new ArgumentException("保存の構造が不正です");
            var g=new VillageGame(s.Unlimited);g.Configure(s.Unlimited,s.Speed,s.DeliveryMs);g.SetResources(s.Gas,s.Electricity,s.Water);
            if(s.At<0||s.At%100!=0||s.Requested<s.At||s.Requested-s.At>=100||s.Requested>315360000000L||!Finite(s.Spent)||s.Spent<0||s.Produced<0)throw new ArgumentException("保存の時計・集計が不正です");
            var groups=s.GroupProgress??new VillageGroupProgressState();
            if(groups.ElapsedMs<0||groups.ElapsedMs>=VillageGroupRules.IntervalMs||groups.ElapsedMs%100!=0||groups.Tick<0||groups.Tick>s.At/VillageGroupRules.IntervalMs||new[]{groups.Chikuwa,groups.Kamaboko,groups.DriedFish}.Any(p=>p<0||p>=VillageGroupRules.ProgressPerItem))throw new ArgumentException("保存の施設群進捗が不正です");
            g._groupElapsed=groups.ElapsedMs;g._groupTick=groups.Tick;g._groupProgress[Item.Chikuwa]=groups.Chikuwa;g._groupProgress[Item.Kamaboko]=groups.Kamaboko;g._groupProgress[Item.DriedFish]=groups.DriedFish;
            g._at=s.At;g._requested=s.Requested;g._spent=s.Spent;g._produced=s.Produced;
            foreach(var n in s.Nodes)
            {
                if(n==null||n.Id<=0||g._nodes.Any(a=>a.Id==n.Id)||!Supported(n.Kind)||n.WarehouseCapacity<0||n.WarehouseCapacity>MaxWarehouseCapacity||n.Kind!=FacilityKind.Warehouse&&n.WarehouseCapacity!=0||(s.EffortSchema==0?(n.Level<0||n.Level>5):(n.Effort<0||n.Effort>VillageEffortRules.MaxEffort||!VillageEffortRules.Supports(n.Kind)&&n.Effort!=0))||g.Placement(n.Kind,n.X,n.Y,0)!=""||n.Stock==null||n.Stock.Length!=8||n.Stock.Any(i=>i<0||i>SavedCapacityLimit(n.Kind))||n.Started<0||n.Finish<0||n.Finish>0&&(n.Started>s.At||n.Finish<=s.At||n.Finish<=n.Started)||RecipeOf(new Node{Kind=n.Kind})==null&&n.Finish!=0)throw new ArgumentException("保存の施設が不正です");
                var node=new Node{Id=n.Id,Kind=n.Kind,Effort=s.EffortSchema==0?(VillageEffortRules.Supports(n.Kind)?Math.Min(VillageEffortRules.MaxEffort,Math.Max(0,n.Level-1)):0):n.Effort,WarehouseCapacity=n.WarehouseCapacity,X=n.X,Y=n.Y,Started=n.Started,Finish=n.Finish,Status=n.Finish>0?"生産中":"待機"};
                for(var i=0;i<8;i++)node.Stock[(Item)i]=n.Stock[i];g._nodes.Add(node);
            }
            foreach(var c in s.Cargo)
            {
                if(c==null||c.Id<=0||g._cargo.Any(a=>a.Id==c.Id)||!Enum.IsDefined(typeof(Item),c.Item)||c.From==c.To||!g._nodes.Any(n=>n.Id==c.From)||!g._nodes.Any(n=>n.Id==c.To)||c.Start<0||c.Start>s.At||c.End<=s.At||c.End<=c.Start||c.FromX<0||c.FromX>=24||c.FromY<0||c.FromY>=24||c.ToX<0||c.ToX>=24||c.ToY<0||c.ToY>=24)throw new ArgumentException("保存の配送が不正です");
                var dest=g._nodes.First(n=>n.Id==c.To);if(dest.X!=c.ToX||dest.Y!=c.ToY)throw new ArgumentException("配送先の予約が不正です");
                g._cargo.Add(new VillageCargoView{Id=c.Id,From=c.From,To=c.To,Item=c.Item,FromX=c.FromX,FromY=c.FromY,ToX=c.ToX,ToY=c.ToY,Start=c.Start,End=c.End});
            }
            foreach(var n in g._nodes)
                if(n.Stock.Values.Sum()+g._cargo.Count(c=>c.To==n.Id)+(n.Finish>0&&RecipeOf(n)!=null?RecipeOf(n).Output.Count:0)>SavedCapacityLimit(n.Kind))throw new ArgumentException("在庫予約が容量を超えています");
            if(s.NextId<=g._nodes.Select(n=>n.Id).DefaultIfEmpty(0).Max()||s.NextCargo<=g._cargo.Select(c=>c.Id).DefaultIfEmpty(0).Max())throw new ArgumentException("保存のIDが不正です");
            foreach(var h in s.History){if(h==null||h.At<0||h.At>s.At||h.Made<0||!Finite(h.Gas)||h.Gas<0)throw new ArgumentException("実績が不正です");g._history.Add((h.At,h.Gas,h.Made));}
            if(s.Trend!=null)
            {
                var currentMin=s.At/60000;
                if(s.Trend.Count>1000)throw new ArgumentException("保存のトレンド記録数が不正です");
                foreach(var t in s.Trend)
                {
                    if(t==null||t.Minute<0||t.Minute>currentMin||t.Minute<currentMin-60||!Finite(t.Gas)||t.Gas<0)throw new ArgumentException("保存のトレンド分・ガスが不正です");
                    if(t.Made==null||t.Made.Length!=8||t.Made.Any(x=>x<0)||t.Used==null||t.Used.Length!=8||t.Used.Any(x=>x<0))throw new ArgumentException("保存のトレンド品目実績が不正です");
                    var bucket=new TrendBucket{Minute=t.Minute,Gas=t.Gas};
                    Array.Copy(t.Made,bucket.Made,8);
                    Array.Copy(t.Used,bucket.Used,8);
                    g._trend[t.Minute]=bucket;
                }
            }
            if(s.Ledger.LastStepDay< -1||s.Ledger.MaxSteps<0||s.Ledger.MaxSteps>172800||s.Ledger.Entries.Any(e=>e==null||string.IsNullOrWhiteSpace(e.Id)||e.Id.Length>128||string.IsNullOrWhiteSpace(e.Kind)||e.Day<0||!Finite(e.Amount)||e.Amount<0)||s.Ledger.Entries.GroupBy(e=>e.Id).Any(a=>a.Count()>1))throw new ArgumentException("受領台帳が不正です");
            g._encounters=EncounterLedger.Restore(s.Encounters);
            g._nextId=s.NextId;g._nextCargo=s.NextCargo;g._ledger=s.Ledger.Copy();g._freeResources=s.FreeResources;return g;
        }
    }
}
