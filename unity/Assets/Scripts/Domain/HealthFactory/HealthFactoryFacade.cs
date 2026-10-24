using System;
using System.Collections.Generic;
using System.Linq;
namespace ProjectPenguin.Domain.HealthFactory
{
    public sealed class HealthFactoryFacade
    {
        private readonly HealthFactoryState _state;
        public HealthFactoryFacade(bool compact=false)
        {_state=new HealthFactoryState();if(compact)_state.Y=new[]{6,8,10};}
        private static HealthFactoryResult Ok(string reason,long delta=0)=>new HealthFactoryResult(true,reason,delta);
        private static HealthFactoryResult Fail(string reason)=>new HealthFactoryResult(false,reason);

        public HealthFactoryResult AcceptSteps(int day,int steps,int today)
        {
            if(day<0||day!=today||day<_state.LastDay)return Fail("対象日が現在の日と一致しません");
            if(steps<0||steps>172800)return Fail("疑わしい歩数です。付与せず再確認してください");
            var previous=day==_state.LastDay?_state.MaxSteps:0;
            var maximum=Math.Max(previous,steps);
            var delta=(long)(maximum/500-previous/500)*_state.StepMultiplier;
            var balance=checked(_state.Gas+delta);var granted=checked(_state.GrantedGas+delta);
            _state.LastDay=day;_state.MaxSteps=maximum;_state.Gas=balance;_state.GrantedGas=granted;
            return Ok(delta==0?"受領済み、または次の500歩まで待機":$"歩数差分からガス +{delta}",delta);
        }
        public HealthFactoryResult AcceptWater(string id,int day,int today,bool confirmed)
        {
            if(!confirmed||day<0||day!=today||string.IsNullOrWhiteSpace(id))return Fail("水の対象日と記録内容の確認が必要です");
            if(_state.ReceiptIds.Contains(id)||_state.WaterDays.Contains(day))return Fail("この記録または対象日は受領済みです");
            var balance=checked(_state.Water+3);_state.ReceiptIds.Add(id);_state.WaterDays.Add(day);_state.Water=balance;
            return Ok("確認済みの水分補給記録：水 +3（摂取量の実測ではありません）",3);
        }
        public HealthFactoryResult AcceptScreen(string id,int day,int used,int today,bool fullDay,bool confirmed)
        {
            if(!confirmed||!fullDay||today<1||day!=today-1||used<0||used>1440||string.IsNullOrWhiteSpace(id))
                return Fail("前日の確定記録と0〜1440分の使用時間が必要です");
            if(_state.ReceiptIds.Contains(id)||_state.ScreenDays.Contains(day))return Fail("この記録または対象日は受領済みです");
            var delta=(1440-used)/120;var balance=checked(_state.Electricity+delta);
            _state.ReceiptIds.Add(id);_state.ScreenDays.Add(day);_state.Electricity=balance;
            return Ok($"前日使用{used}分の確認記録：電気 +{delta}",delta);
        }
        public HealthFactoryResult AdvanceTo(long ms)
        {
            if(ms<_state.RequestedMs||ms-_state.RequestedMs>120000)return Fail("逆行または1回120秒を超える精算はできません");
            var events=new List<HealthFactoryEvent>();_state.RequestedMs=ms;
            while(_state.AtMs+50<=ms)HealthFactoryFlow.Tick(_state,events);
            return new HealthFactoryResult(true,"進行",0,events);
        }
        public HealthFactoryResult SetRoute(int route,bool enabled)
        {
            if(route<0||route>1)return Fail("経路がありません");_state.Routes[route]=enabled;
            return Ok(enabled?"今後の配送を接続しました":"今後の配送を停止。支払済みの荷物は届きます");
        }
        public HealthFactoryResult Move(int node,int x,int y)
        {
            if(node<0||node>2||x<0||y<0||x+2>_state.BoardSize||y+2>_state.BoardSize)return Fail("敷地の範囲外です");
            if(_state.Deliveries.Any(d=>d.Route+1==node))return Fail("入庫予約のある施設は移動できません");
            for(var i=0;i<3;i++)if(i!=node&&Math.Abs(_state.X[i]-x)<2&&Math.Abs(_state.Y[i]-y)<2)return Fail("施設が重なります");
            _state.X[node]=x;_state.Y[node]=y;return Ok("移動しました。次の出荷から距離と費用を更新します");
        }
        public HealthFactoryResult SetLayout(bool compact)
        {
            if(_state.Deliveries.Count>0)return Fail("配送完了後に配置を比較してください");
            _state.X=new[]{6,6,6};_state.Y=compact?new[]{6,8,10}:new[]{2,8,14};
            return Ok(compact?"効率配置：完成品1個にガス3":"長い配置：完成品1個にガス5");
        }
        public HealthFactoryResult BuildBuffer()
        {
            if(_state.BufferBuilt)return Fail("バッファは建設済みです");if(_state.Electricity<4)return Fail("建設には電気4が必要です");
            _state.Electricity-=4;_state.BufferBuilt=true;return Ok("入力バッファ建設：入力4・各線2便。電気 -4");
        }
        public HealthFactoryResult Upgrade()
        {
            if(_state.Upgraded)return Fail("改修済みです");if(_state.Electricity<4)return Fail("改修には電気4が必要です");
            _state.Electricity-=4;_state.Upgraded=true;return Ok("次の加工から所要時間半分。ガス費用は同じ。電気 -4");
        }
        public HealthFactoryResult Expand()
        {
            if(_state.Expanded)return Fail("この試作の拡張は完了しています");if(_state.Water<3)return Fail("拡張には水3が必要です");
            _state.Water-=3;_state.Expanded=true;return Ok("敷地16×16 → 20×20。水 -3");
        }
        public HealthFactoryResult SetPenguinFixture(bool passive,bool assigned)
        {_state.PassiveFixture=passive;_state.AssignedFixture=assigned;return Ok("テスト個体：今後の歩数差分倍率と加工時間だけ変更。実通信なし");}
        public HealthFactoryQuote Quote(int? today=null)
        {
            var s=_state.Clone();var spent=s.Spent;s.Gas=1000000;
            var events=new List<HealthFactoryEvent>();var deadline=s.AtMs+120000;
            while(s.Stored<4&&s.AtMs<deadline)HealthFactoryFlow.Tick(s,events);
            if(s.Stored<4)return new HealthFactoryQuote{Reachable=false,Reason="未接続または容量・在庫の制約。歩数だけでは解決しません"};
            var future=s.Spent-spent;var shortfall=Math.Max(0,future-_state.Gas);
            var units=(shortfall+_state.StepMultiplier-1)/_state.StepMultiplier;
            return new HealthFactoryQuote{Reachable=true,Reason="支払済みの配送・加工を除く予測",FutureGas=future,GasShortfall=shortfall,
                AdditionalSteps=shortfall==0?0:Math.Max(0,units*500-((!today.HasValue||today.Value==_state.LastDay)?_state.MaxSteps%500:0))};
        }
        public HealthFactoryView CreateView()
        {
            var s=_state;
            return new HealthFactoryView{Gas=s.Gas,Spent=s.Spent,AtMs=s.AtMs,Raw=s.Raw,Input=s.Input,Output=s.Output,Stored=s.Stored,
                Processing=s.ProcessEnd>0,ProcessStart=s.ProcessStart,ProcessEnd=s.ProcessEnd,Water=s.Water,Electricity=s.Electricity,
                BoardSize=s.BoardSize,InputCapacity=s.InputCapacity,Slots=s.Slots,ProcessingDurationMs=s.ProcessingDurationMs,
                MaxSteps=s.MaxSteps,StepDay=s.LastDay,GrantedGas=s.GrantedGas,StepMultiplier=s.StepMultiplier,PassiveFixture=s.PassiveFixture,AssignedFixture=s.AssignedFixture,
                BufferBuilt=s.BufferBuilt,Upgraded=s.Upgraded,
                StopReason=s.Stored==4?"目標達成 · 報酬なし":s.Deliveries.Count>0||s.ProcessEnd>0?"支払済みの配送・加工が進行中":
                    !s.Routes[0]||!s.Routes[1]?"未接続の経路があります":s.Gas==0?"ガス不足 · 在庫と施設を保持して待機":"材料・容量・次の開始を待機",
                Routes=Array.AsReadOnly((bool[])s.Routes.Clone()),RouteCosts=Array.AsReadOnly(new[]{s.RouteCost(0),s.RouteCost(1)}),
                Nodes=Array.AsReadOnly(Enumerable.Range(0,3).Select(i=>new HealthFactoryNodeView{Id=i,X=s.X[i],Y=s.Y[i]}).ToArray()),
                Deliveries=Array.AsReadOnly(s.Deliveries.Select(d=>new HealthFactoryDeliveryView{Route=d.Route,FromX=d.FromX,FromY=d.FromY,ToX=d.ToX,ToY=d.ToY,
                    DepartAt=d.DepartAt,ArriveAt=d.ArriveAt,PaidGas=d.PaidGas}).ToArray())};
        }
    }
}
