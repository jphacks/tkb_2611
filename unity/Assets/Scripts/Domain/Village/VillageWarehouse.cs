using System;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    public sealed partial class VillageGame
    {
        public const int DefaultWarehouseCapacity = 20;
        public const int MaxWarehouseCapacity = 10000;
        private static int WarehouseBaseCapacity(Node n) => n.WarehouseCapacity == 0 ? DefaultWarehouseCapacity : n.WarehouseCapacity;
        private static int SavedCapacityLimit(FacilityKind kind) => kind == FacilityKind.Warehouse ? MaxWarehouseCapacity * 2 : VillageEffortRules.MaximumCapacity;

        public VillageResult SetWarehouseCapacity(int id, int capacity)
        {
            var node = _nodes.FirstOrDefault(n => n.Id == id);
            if (node == null || node.Kind != FacilityKind.Warehouse) return Result(false, "倉庫を選んでください");
            if (capacity < 1 || capacity > MaxWarehouseCapacity) return Result(false, "基本保管数は1〜10,000の整数で入力してください");
            var occupied = node.Stock.Values.Sum() + _cargo.Count(c => c.To == id);
            var effective = (int)(capacity * CoolingMultiplier(node));
            if (effective < occupied) return Result(false, $"在庫と配送予定が{occupied}個あります。合計容量がこの数以上になる値を入力してください");
            node.WarehouseCapacity = capacity;
            return Result(true, $"倉庫の基本保管数を{capacity:N0}個に変更しました", id);
        }
    }
}
