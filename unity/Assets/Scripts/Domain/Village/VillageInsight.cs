using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    public enum VillageNextStepTone { Good, Info, Warn, Bad }

    /// <summary>工房モードとホームに出す「次にやること」1 つ。</summary>
    public sealed class VillageNextStep
    {
        /// <summary>何が起きているか (1 行目)。</summary>
        public string Text {get; internal set;}
        /// <summary>次の一手 (2 行目)。無ければ空。</summary>
        public string Hint {get; internal set;}
        /// <summary>建てるとよい施設。施設一覧で目立たせる。無ければ null。</summary>
        public FacilityKind? Build {get; internal set;}
        public VillageNextStepTone Tone {get; internal set;}
        /// <summary>この案内に関わる施設 (止まっている施設など)。施設の上の札と 3D の強調に使う。</summary>
        public IReadOnlyList<int> NodeIds {get; internal set;}
    }

    /// <summary>
    /// 生産レポートや各種UIで使う計算ロジック (UnityEngine 非依存の純粋関数)。
    /// </summary>
    public static class VillageInsight
    {
        /// <summary>
        /// 本日の供給ペース (日平均ペース)。
        /// 朝方に跳ね上がらないよう経過分は最低 60 分として扱う。
        /// </summary>
        public static double SupplyPerMinute(double receivedToday, double minutesSinceMidnight)
        {
            if (receivedToday <= 0 || double.IsNaN(receivedToday) || double.IsInfinity(receivedToday)) return 0;
            var minutes = Math.Max(60.0, minutesSinceMidnight);
            return receivedToday / minutes;
        }

        /// <summary>
        /// あと何分でガスが空になるか。収支が 0 以上なら null (減っていない)。
        /// </summary>
        public static int? MinutesUntilEmpty(double gas, double net)
        {
            if (net >= -1e-6 || double.IsNaN(net) || double.IsInfinity(net)) return null;
            if (gas <= 0 || double.IsNaN(gas) || double.IsInfinity(gas)) return 0;
            return (int)Math.Floor(gas / -net);
        }

        /// <summary>
        /// タンクの水位割合 (0.0 〜 1.0)。
        /// 今の消費の 1 時間ぶん (usePerMinute * 60) を満タンとする。
        /// 消費が 0 のときは満タン (1.0) 固定。
        /// </summary>
        public static double TankLevel(double gas, double usePerMinute)
        {
            if (gas <= 0 || double.IsNaN(gas) || double.IsInfinity(gas)) return 0;
            if (usePerMinute <= 1e-6 || double.IsNaN(usePerMinute) || double.IsInfinity(usePerMinute)) return 1.0;
            var cap = usePerMinute * 60.0;
            return Math.Min(1.0, Math.Max(0.0, gas / cap));
        }

        /// <summary>
        /// タンクの目盛り上の満タン容量 (今の消費 1 時間ぶん)。
        /// 消費が 0 のときは null (分母非表示)。
        /// </summary>
        public static double? TankCapacity(double usePerMinute)
        {
            if (usePerMinute <= 1e-6 || double.IsNaN(usePerMinute) || double.IsInfinity(usePerMinute)) return null;
            return usePerMinute * 60.0;
        }

        public const string GasWait = "ガスを待っています", Full = "出力の置き場が満杯", MaterialWait = "材料を待っています";

        /// <summary>
        /// 生産レポートと工房モードの操作バーに出す数。稼働 = 生産中の施設、全体 = 施設の数、
        /// 停止 = 作る施設 (倉庫より前の種類) のうち生産中でないもの、配送中 = 運ばれている荷物の数。
        /// </summary>
        public static (int Active, int Total, int Stopped, int Delivering) Counts(VillageView view)
        {
            if (view?.Nodes == null) return (0, 0, 0, 0);
            return (view.Nodes.Count(n => n.Status == "生産中"), view.Nodes.Count,
                view.Nodes.Count(n => n.Status != "生産中" && n.Kind < FacilityKind.Warehouse), view.Cargo?.Count ?? 0);
        }

        /// <summary>止まっている施設か (ガス切れ・満杯・材料待ち)。</summary>
        public static bool IsStopped(string status) => status == GasWait || status == Full || status == MaterialWait;

        /// <summary>
        /// 今の村の状態から「次にやること」を 1 つ選ぶ。上から順に、いちばん困っていることを優先する。
        /// 施設が無い → ガス切れ → 置き場が満杯 → 作った品物の送り先が無い → 材料が届かない →
        /// あと 1 棟で施設群 → 施設群があって詳細表示 → 材料待ち → 順調。
        /// </summary>
        public static VillageNextStep NextStep(VillageView view, IReadOnlyList<VillageGroupHint> hints, int reach = 8)
        {
            if (view == null) return Step("村を読み込んでいるよ", "", null, VillageNextStepTone.Info);
            var nodes = view.Nodes ?? Array.Empty<VillageNodeView>();
            if (nodes.Count == 0)
                return Step("まだ施設がないよ", "まずは漁場を建てて、お魚をとろう", FacilityKind.Fishery, VillageNextStepTone.Info);

            var gasWait = nodes.Where(n => n.Status == GasWait).ToArray();
            if (gasWait.Length > 0 && !view.ResourcesUnlimited)
                return Step($"ガスが切れて {gasWait.Length} 棟が止まっている", "歩くとガスがたまるよ", null, VillageNextStepTone.Bad, gasWait);

            var routes = view.Routes ?? Array.Empty<VillageRouteView>();
            var full = nodes.Where(n => n.Status == Full).ToArray();
            if (full.Length > 0)
            {
                var item = OutputOf(full[0]);
                var build = item.HasValue ? ConsumerOf(item.Value) ?? FacilityKind.Warehouse : FacilityKind.Warehouse;
                if (routes.Any(r => r.From == full[0].Id)) build = FacilityKind.Warehouse;
                return Step($"{IslandCatalog.Get(full[0].Kind).Name}の置き場がいっぱい",
                    $"{IslandCatalog.Get(build).Name}を {reach} マス以内に置くと運び出せるよ", build, VillageNextStepTone.Warn, full);
            }

            foreach (var n in nodes)
            {
                var item = OutputOf(n);
                if (!item.HasValue || routes.Any(r => r.From == n.Id)) continue;
                if (n.Stock == null || !n.Stock.TryGetValue(item.Value, out var held) || held <= 0) continue;
                var build = ConsumerOf(item.Value) ?? FacilityKind.Warehouse;
                return Step($"{IslandCatalog.NameOf(item.Value)}の送り先がないよ",
                    $"{IslandCatalog.Get(build).Name}を{IslandCatalog.Get(n.Kind).Name}から {reach} マス以内に置こう", build, VillageNextStepTone.Warn, new[] { n });
            }

            var waiting = nodes.Where(n => n.Status == MaterialWait).ToArray();
            foreach (var w in waiting)
            {
                if (routes.Any(r => r.To == w.Id)) continue;
                var recipe = IslandCatalog.Get(w.Kind).Recipes.FirstOrDefault();
                var lacking = recipe?.Inputs.Where(i => w.Stock == null || !w.Stock.TryGetValue(i.Item, out var c) || c < i.Count).Select(i => (Item?)i.Item).FirstOrDefault();
                if (!lacking.HasValue) continue;
                var producer = ProducerOf(lacking.Value);
                return Step($"{IslandCatalog.Get(w.Kind).Name}に{IslandCatalog.NameOf(lacking.Value)}が届いていない",
                    producer.HasValue ? $"{IslandCatalog.Get(producer.Value).Name}を {reach} マス以内に置こう" : "",
                    producer, VillageNextStepTone.Warn, new[] { w });
            }

            if (hints != null && hints.Count > 0)
            {
                var h = hints[0];
                return Step($"あと 1 棟で{h.Name}", $"光っている所に{IslandCatalog.Get(h.Kind).Name}を置こう", h.Kind, VillageNextStepTone.Info);
            }

            if (!view.GroupViewActive && view.Synergies != null && view.Synergies.Count > 0)
                return Step($"施設群が {view.Synergies.Count} つ成立しているよ", "全体表示にすると追加生産が動くよ", null, VillageNextStepTone.Info);

            if (waiting.Length > 0)
                return Step($"{waiting.Length} 棟が材料を待っている", "前の工程の施設を増やすと早く届くよ", null, VillageNextStepTone.Info, waiting);

            return Step("すべて順調に動いているよ", view.ProducedLastMinute > 0 ? $"直近 1 分で {view.ProducedLastMinute} 個つくった" : "", null, VillageNextStepTone.Good);
        }

        private static VillageNextStep Step(string text, string hint, FacilityKind? build, VillageNextStepTone tone, IEnumerable<VillageNodeView> nodes = null) =>
            new VillageNextStep { Text = text, Hint = hint ?? "", Build = build, Tone = tone, NodeIds = Array.AsReadOnly((nodes ?? Enumerable.Empty<VillageNodeView>()).Select(n => n.Id).ToArray()) };

        private static Item? OutputOf(VillageNodeView n) => IslandCatalog.Get(n.Kind).Recipes.FirstOrDefault()?.Output.Item;

        /// <summary>その品物を材料に使う施設 (いちばん手前の工程)。</summary>
        private static FacilityKind? ConsumerOf(Item item) =>
            IslandCatalog.Facilities.Where(f => f.Recipes.Any(r => r.Inputs.Any(i => i.Item == item))).Select(f => (FacilityKind?)f.Kind).FirstOrDefault();

        /// <summary>その品物を作る施設。</summary>
        private static FacilityKind? ProducerOf(Item item) =>
            IslandCatalog.Facilities.Where(f => f.Recipes.Any(r => r.Output.Item == item)).Select(f => (FacilityKind?)f.Kind).FirstOrDefault();
    }
}
