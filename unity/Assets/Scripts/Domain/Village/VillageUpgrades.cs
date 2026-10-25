using System;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    public sealed class VillageUpgradeView
    {
        public int Level { get; internal set; }
        public int NextLevel { get; internal set; }
        public double ElectricityCost { get; internal set; }
        public bool CanUpgrade { get; internal set; }
        public bool Free { get; internal set; }
        public string Reason { get; internal set; }
        public string CurrentEffect { get; internal set; }
        public string NextEffect { get; internal set; }
    }

    public static class VillageUpgradeRules
    {
        public const int MaxLevel = 5;
        public const int MaximumCapacity = 160;
        public static double Speed(int level) => 1 + (level - 1) * .25;
        public static double Amplifier(int level) => .25 + (level - 1) * .05;
        public static double Dormitory(int level) => .10 + (level - 1) * .025;
        public static double Research(int level) => .15 + (level - 1) * .05;
        public static double Cooling(int level) => 2 + (level - 1) * .5;
        public static int DeliveryRange(int level) => 12 + (level - 1) * 2;
        public static int BaseCapacity(FacilityKind kind, int level) =>
            (int)((kind == FacilityKind.Warehouse || kind == FacilityKind.Sorter ? 20 : 12) * Speed(level));
        public static double Cost(FacilityKind kind, int level) => Math.Ceiling(IslandCatalog.Get(kind).ElectricityCost * level);
        public static string Effect(FacilityKind kind, int level)
        {
            switch (kind)
            {
                case FacilityKind.Warehouse: return $"基本保管容量 {BaseCapacity(kind, level)}個";
                case FacilityKind.Sorter: return $"周辺施設の配送距離 {DeliveryRange(level)}マス";
                case FacilityKind.IceHouse: return $"周辺の保管容量 ×{Cooling(level):0.##}";
                case FacilityKind.Dormitory: return $"村全体の生産速度 +{Dormitory(level) * 100:0.#}%";
                case FacilityKind.Research: return $"村全体の生産速度 +{Research(level) * 100:0.#}%";
                case FacilityKind.ElectricAmplifier:
                case FacilityKind.WaterAmplifier:
                case FacilityKind.GasAmplifier: return $"周辺の生産速度 +{Amplifier(level) * 100:0.#}%";
                default: return $"施設の生産速度 ×{Speed(level):0.##}\n基本保管容量 {BaseCapacity(kind, level)}個";
            }
        }
    }

    public sealed partial class VillageGame
    {
        public VillageUpgradeView PreviewUpgrade(int id) => UpgradeView(_nodes.FirstOrDefault(n => n.Id == id));
        private VillageUpgradeView UpgradeView(Node node)
        {
            if (node == null) return new VillageUpgradeView { Reason = "施設がありません" };
            var maximum = node.Level == VillageUpgradeRules.MaxLevel;
            var cost = maximum ? 0 : VillageUpgradeRules.Cost(node.Kind, node.Level);
            var affordable = _unlimited || _electricity >= cost;
            return new VillageUpgradeView
            {
                Level = node.Level, NextLevel = maximum ? node.Level : node.Level + 1,
                ElectricityCost = cost, CanUpgrade = !maximum && affordable, Free = _unlimited,
                Reason = maximum ? "最大レベルです" : affordable ? "強化できます" : $"電気があと {cost - _electricity:0.#} 必要です",
                CurrentEffect = VillageUpgradeRules.Effect(node.Kind, node.Level),
                NextEffect = maximum ? "すべての強化が完了しました" : VillageUpgradeRules.Effect(node.Kind, node.Level + 1),
            };
        }
        public VillageResult Upgrade(int id, int expectedLevel)
        {
            var node = _nodes.FirstOrDefault(n => n.Id == id);
            if (node == null) return Result(false, "施設がありません");
            if (node.Level != expectedLevel) return Result(false, "レベルが変わりました。強化内容をもう一度確認してください");
            var preview = UpgradeView(node);
            if (!preview.CanUpgrade) return Result(false, preview.Reason);
            if (!_unlimited) _electricity -= preview.ElectricityCost;
            node.Level++;
            return Result(true, $"{IslandCatalog.Get(node.Kind).Name}を Lv{node.Level} に強化しました", id);
        }
        private double AmplifierBonus(Node target) => _nodes
            .Where(n => n.Id != target.Id && (n.Kind == FacilityKind.ElectricAmplifier || n.Kind == FacilityKind.WaterAmplifier || n.Kind == FacilityKind.GasAmplifier)
                && Math.Max(Math.Abs(n.X - target.X), Math.Abs(n.Y - target.Y)) <= 6)
            .Select(n => VillageUpgradeRules.Amplifier(n.Level)).DefaultIfEmpty(0).Max();
        private double CoolingMultiplier(Node target) => _nodes
            .Where(n => n.Id != target.Id && n.Kind == FacilityKind.IceHouse && Math.Max(Math.Abs(n.X - target.X), Math.Abs(n.Y - target.Y)) <= 6)
            .Select(n => VillageUpgradeRules.Cooling(n.Level)).DefaultIfEmpty(1).Max();
        private int DeliveryRange(Node source) => _nodes
            .Where(n => n.Kind == FacilityKind.Sorter && Math.Abs(n.X - source.X) + Math.Abs(n.Y - source.Y) <= 6)
            .Select(n => VillageUpgradeRules.DeliveryRange(n.Level)).DefaultIfEmpty(8).Max();
        private static string SupportStatus(Node node) => VillageUpgradeRules.Effect(node.Kind, node.Level);
    }
}
