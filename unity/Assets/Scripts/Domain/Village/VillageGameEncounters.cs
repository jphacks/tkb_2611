using System;
using System.Linq;
using ProjectPenguin.Domain.Encounter;

namespace ProjectPenguin.Domain.Village
{
    /// <summary>
    /// 村のすれ違い。台帳 (<see cref="EncounterLedger"/>) を村の保存に入れ、成立したら引いた種に合わせて資源を渡す。
    /// 通信 (BLE と backend の相互確認) は Systems の VillageEncounters が受け持ち、ここは送らない。
    /// 時刻は再起動で戻らない時計 (端末の UTC のミリ秒) を受け取る。送り直しの予定を保存に入れるため。
    /// </summary>
    public sealed partial class VillageGame
    {
        /// <summary>資源の付いた種 (イワトビ・オウサマ・マゼラン) が 1 回で渡す、その資源の量。審査向けの仮の値 (docs/decisions.md)。</summary>
        public const int EncounterResourceAmount=5;
        /// <summary>資源の無い種 (アデリー・ヒゲ・ガラパゴス) が 1 回で渡す、ガス・電気・水それぞれの量。審査向けの仮の値。</summary>
        public const int EncounterMixedAmount=2;

        private EncounterLedger _encounters=new EncounterLedger();

        public int EncounterDay=>_encounters.Day;
        public string EncounterSelfToken=>_encounters.SelfToken;
        public int EncountersSeen=>_encounters.Seen;
        public int EncountersWaiting=>_encounters.Waiting;
        public int EncountersGranted=>_encounters.GrantedToday;

        /// <summary>アプリの日が替わったら、トークンを作り直して台帳を空にする。同じ日なら失敗 (保存しない)。</summary>
        public VillageResult BeginEncounterDay(int appDay)=>
            _encounters.BeginDay(appDay)?Result(true,"今日のすれ違いを始めました"):Result(false,"今日のすれ違いは始まっています");

        /// <summary>台帳を変えずに、その相手を記録するかを確かめる。</summary>
        public bool WouldObservePeer(string token)=>_encounters.Check(token)==EncounterObservation.Accepted;
        public bool HasEncounterReportDue(long nowMs)=>_encounters.HasReportDue(nowMs);

        /// <summary>BLE で見た相手を台帳に入れる。同じ相手・自分・上限は失敗 (近くにいる間は何度も届くため、知らせない)。</summary>
        public VillageResult ObservePeer(string token,long nowMs)=>
            _encounters.Observe(token,nowMs)==EncounterObservation.Accepted?Result(true,"すれ違った相手を記録しました"):Result(false,"記録しない相手です");

        /// <summary>今送る報告を 1 件選び、送り直しの予定を進める。無ければ失敗で、<paramref name="report"/> は null。</summary>
        public VillageResult TakeEncounterReport(long nowMs,out EncounterReport report)
        {
            report=_encounters.NextReport(nowMs);
            return report!=null?Result(true,"すれ違いの確認を送ります"):Result(false,"送る報告はありません");
        }

        public VillageResult EncounterFailed(EncounterReport report,long nowMs,long retryAfterMs)
        {
            _encounters.ReportFailed(report,nowMs,retryAfterMs);return Result(true,"すれ違いの確認を送り直します");
        }

        public VillageResult DiscardEncounter(EncounterReport report)
        {
            _encounters.Discard(report);return Result(true,"受け付けられなかった相手を除きました");
        }

        /// <summary>backend の報告の結果を入れる。成立して上限に余りがあれば、種に合わせて資源を渡し、記録に残す。</summary>
        public VillageResult ApplyEncounter(EncounterReport report,bool matched,string pairId,long nowMs,out EncounterResultView view)
        {
            view=_encounters.ApplyResult(report,matched,pairId,nowMs);
            switch(view.Outcome)
            {
                case EncounterOutcome.Waiting:return Result(true,"相手の確認を待っています");
                case EncounterOutcome.CapReached:return Result(true,$"すれ違い成立。今日の受け取りは {EncounterLedger.PenguinsPerDay} 回までです");
                case EncounterOutcome.Granted:return GrantEncounter(pairId,_encounters.Day,view.Species);
                default:return Result(false,"今日のすれ違いの結果ではありません");
            }
        }

        private VillageResult GrantEncounter(string pairId,int day,EncounterSpecies species)
        {
            var prefix="encounter:"+pairId+":";
            if(_ledger.Entries.Any(e=>e.Id.StartsWith(prefix,StringComparison.Ordinal)))return Result(false,"このすれ違いは受け取り済みです");
            var gas=species.Resource==EncounterResource.Gas?EncounterResourceAmount:species.Resource==EncounterResource.None?EncounterMixedAmount:0;
            var electricity=species.Resource==EncounterResource.Electricity?EncounterResourceAmount:species.Resource==EncounterResource.None?EncounterMixedAmount:0;
            var water=species.Resource==EncounterResource.Water?EncounterResourceAmount:species.Resource==EncounterResource.None?EncounterMixedAmount:0;
            if(!Finite(_gas+gas)||_gas+gas>int.MaxValue||_electricity+electricity>int.MaxValue||_water+water>int.MaxValue)return Result(false,"資源の上限です");
            _gas+=gas;_electricity+=electricity;_water+=water;
            // 記録は資源ごとに 1 行 (受け取った資源の一覧が資源ごとのアイコンで並ぶため)。
            if(gas>0)_ledger.Entries.Add(new HealthRecordEntry{Id=prefix+"gas",Kind="すれ違い → ガス",Day=day,Amount=gas});
            if(electricity>0)_ledger.Entries.Add(new HealthRecordEntry{Id=prefix+"electricity",Kind="すれ違い → 電気",Day=day,Amount=electricity});
            if(water>0)_ledger.Entries.Add(new HealthRecordEntry{Id=prefix+"water",Kind="すれ違い → 水",Day=day,Amount=water});
            var gains=string.Join("・",new[]{gas>0?$"ガス +{gas}":null,electricity>0?$"電気 +{electricity}":null,water>0?$"水 +{water}":null}.Where(s=>s!=null));
            return Result(true,$"すれ違い成立！{species.Name}ペンギンが来て、{gains}");
        }
    }
}
