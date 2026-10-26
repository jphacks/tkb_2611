using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    public sealed class VillageEffortView
    {
        public int Effort { get; internal set; }
        public bool Supported { get; internal set; }
        public double SpeedMultiplier { get; internal set; }
        public double GasPerCycle { get; internal set; }
        public string Reason { get; internal set; }
    }

    public static class VillageEffortRules
    {
        public const int MaxEffort = 3;
        // Upper bound of stock held by legacy level-based saves, including cooling.
        public const int MaximumCapacity = 160;
        public static bool Supports(FacilityKind kind) => IslandCatalog.Get(kind).Recipes.Count > 0;
        public static double Speed(int effort) => 1 + effort;
        public static double Gas(int effort) => 1 + effort;
        public static string Effect(int effort) => $"生産速度 ×{Speed(effort):0.##} · ガス {Gas(effort):0.##} / 加工";
    }

    public sealed partial class VillageGame
    {
        private static VillageEffortView EffortView(Node node) => new VillageEffortView
        {
            Effort = node.Effort, Supported = VillageEffortRules.Supports(node.Kind),
            SpeedMultiplier = VillageEffortRules.Speed(node.Effort), GasPerCycle = RecipeOf(node) == null ? 0 : VillageEffortRules.Gas(node.Effort),
            Reason = RecipeOf(node) == null ? "生産施設でエフォートを変更できます" : "費用なしで上下でき、次の加工開始から反映します",
        };
        public VillageResult SetEffort(int id, int effort)
        {
            var node = _nodes.Concat(_upperNodes).FirstOrDefault(n => n.Id == id);
            if (node == null) return Result(false, "施設がありません");
            if (!VillageEffortRules.Supports(node.Kind)) return Result(false, "この施設にはエフォートを設定できません");
            if (effort < 0 || effort > VillageEffortRules.MaxEffort) return Result(false, "エフォートは0〜3で指定してください");
            node.Effort = effort;
            return Result(true, $"エフォートを{effort}に変更しました。次の加工開始から反映します", id);
        }
        private double AmplifierBonus(Node target) => _nodes.Any(n => n.Id != target.Id
            && (n.Kind == FacilityKind.ElectricAmplifier || n.Kind == FacilityKind.WaterAmplifier || n.Kind == FacilityKind.GasAmplifier)
            && System.Math.Max(System.Math.Abs(n.X - target.X), System.Math.Abs(n.Y - target.Y)) <= 6) ? .25 : 0;
        private double CoolingMultiplier(Node target) => _nodes.Any(n => n.Id != target.Id && n.Kind == FacilityKind.IceHouse
            && System.Math.Max(System.Math.Abs(n.X - target.X), System.Math.Abs(n.Y - target.Y)) <= 6) ? 2 : 1;
        private static string SupportStatus(Node node)
        {
            switch (node.Kind)
            {
                case FacilityKind.Warehouse: return "受け取り待ち";
                case FacilityKind.IceHouse: return "周囲の保管容量 ×2";
                case FacilityKind.Sorter: return "周囲の配送距離を拡張中";
                case FacilityKind.Dormitory: return "島全体の作業速度 +10%";
                case FacilityKind.Research: return "島全体の研究効率 +15%";
                default: return "周囲の生産速度 +25%";
            }
        }
    }
}
