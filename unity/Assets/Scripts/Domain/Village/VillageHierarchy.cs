using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    /// <summary>MVP tuning. A facility grid uses the existing two-cell production footprint.</summary>
    public static class VillageHierarchyRules
    {
        public const int BaseGridSize = 2;
        public const int SecondTierPerRank = 5;
        public const int ThirdZoomRank = 2;
        public const int MaxRank = 9;
        public static int GridSize(int zoom)
        {
            if (zoom < 1 || zoom > 3) throw new ArgumentOutOfRangeException(nameof(zoom));
            return BaseGridSize << (zoom - 1);
        }
        public static int Rank(long delivered) => (int)Math.Min(MaxRank, 1 + delivered / SecondTierPerRank);
        public static int MaxZoom(long delivered) => Rank(delivered) >= ThirdZoomRank ? 3 : 2;
        public static bool IsSecondTier(Item item) => item == Item.Surimi || item == Item.DriedFish;
        public static int DeliveryPoints(Item item)
        {
            switch (item)
            {
                case Item.Surimi: case Item.DriedFish: return 4;
                case Item.Kamaboko: case Item.Chikuwa: return 20;
                case Item.Oden: return 300;
                default: return 1;
            }
        }
        public static bool TryUpgrade(FacilityKind child, out FacilityKind parent)
        {
            parent = child == FacilityKind.Fishery ? FacilityKind.Preparation : FacilityKind.Kamaboko;
            return child == FacilityKind.Fishery || child == FacilityKind.Preparation;
        }
        internal static int UpperId(int zoom, int x, int y, FacilityKind kind) => -(zoom * 100000 + (int)kind * 1000 + y * VillageGame.BoardSize + x + 1);
    }

    public sealed partial class VillageGame
    {
        private readonly List<Node> _upperNodes = new List<Node>();
        private readonly HashSet<string> _deliveries = new HashSet<string>(StringComparer.Ordinal);
        private long _deliveryPoints, _secondTierDelivered;
        public long DeliveryPoints => _deliveryPoints;
        public int MaximumZoom => VillageHierarchyRules.MaxZoom(_secondTierDelivered);

        private void ReconcileHierarchy()
        {
            foreach (var node in _upperNodes) node.Formed = false;
            for (var zoom = 2; zoom <= MaximumZoom; zoom++)
            {
                var span = VillageHierarchyRules.GridSize(zoom);
                var childSpan = span / 2;
                var children = (zoom == 2 ? _nodes : _upperNodes.Where(n => n.Zoom == zoom - 1 && n.Formed)).ToArray();
                for (var y = 0; y + span <= BoardSize; y += span)
                    for (var x = 0; x + span <= BoardSize; x += span)
                    {
                        var group = new List<Node>();
                        for (var dy = 0; dy < 2; dy++)
                            for (var dx = 0; dx < 2; dx++)
                            {
                                var child = children.FirstOrDefault(n => n.X == x + dx * childSpan && n.Y == y + dy * childSpan && n.Size == childSpan);
                                if (child != null) group.Add(child);
                            }
                        if (group.Count != 4 || group.Any(n => n.Kind != group[0].Kind)
                            || !VillageHierarchyRules.TryUpgrade(group[0].Kind, out var kind)) continue;
                        var id = VillageHierarchyRules.UpperId(zoom, x, y, kind);
                        var upper = _upperNodes.FirstOrDefault(n => n.Id == id);
                        if (upper == null)
                        {
                            upper = new Node { Id = id, Kind = kind, X = x, Y = y, Zoom = zoom };
                            _upperNodes.Add(upper);
                        }
                        upper.Formed = true;
                        upper.Children = group.Select(n => n.Id).ToArray();
                    }
            }
        }
        private VillageComboDirection UpperComboDirections(Node node)
        {
            if (!node.Formed) return VillageComboDirection.None;
            var directions = VillageComboDirection.None;
            foreach (var other in _upperNodes.Where(n => n.Formed && n.Zoom == node.Zoom && n.Kind == node.Kind && n.Id != node.Id))
            {
                if (other.X == node.X && other.Y == node.Y + node.Size) directions |= VillageComboDirection.Up;
                if (other.X == node.X && other.Y == node.Y - node.Size) directions |= VillageComboDirection.Down;
                if (other.Y == node.Y && other.X == node.X + node.Size) directions |= VillageComboDirection.Right;
                if (other.Y == node.Y && other.X == node.X - node.Size) directions |= VillageComboDirection.Left;
            }
            return directions;
        }
        private IEnumerable<Node> ChildrenOf(Node parent) => (parent.Zoom == 2 ? _nodes : _upperNodes)
            .Where(n => parent.Children.Contains(n.Id));
        private double UpperSpeed(Node node) => _speed * VillageEffortRules.Speed(node.Effort);
        private void StepHierarchy()
        {
            // Complete already-paid batches even when an edited layout retires the facility.
            foreach (var node in _upperNodes.Where(n => n.Finish > 0 && n.Finish <= _at))
            {
                var output = RecipeOf(node).Output.Item;
                node.Stock[output] = node.Held(output) + node.ProcessYield;
                _produced += node.ProcessYield; RecordMade(output, node.ProcessYield);
                _history.Add((_at, 0, node.ProcessYield)); node.Finish = 0; node.ProcessYield = 0;
            }
            foreach (var node in _upperNodes.OrderBy(n => n.Zoom).ThenBy(n => n.Id))
            {
                if (!node.Formed) { node.Status = "組み合わせ解除・在庫回収可"; continue; }
                if (node.Finish > 0) continue;
                var recipe = RecipeOf(node);
                var quarters = recipe.Output.Count * (4 + VillageComboRules.Count(UpperComboDirections(node))) + node.ComboRemainder;
                var output = quarters / 4;
                if (node.Stock.Values.Sum() + output > 12) { node.Status = VillageInsight.Full; continue; }
                var children = ChildrenOf(node).OrderBy(n => n.Id).ToArray();
                if (recipe.Inputs.Any(i => children.Sum(n => n.Held(i.Item)) < i.Count)) { node.Status = VillageInsight.MaterialWait; continue; }
                if (!Pay(VillageEffortRules.Gas(node.Effort))) { node.Status = VillageInsight.GasWait; continue; }
                foreach (var input in recipe.Inputs)
                {
                    var remaining = input.Count;
                    foreach (var child in children)
                    {
                        var used = Math.Min(remaining, child.Held(input.Item));
                        child.Stock[input.Item] = child.Held(input.Item) - used; remaining -= used;
                    }
                    RecordUsed(input.Item, input.Count);
                }
                node.ProcessYield = output; node.ComboRemainder = quarters % 4;
                node.Started = _at; node.Finish = _at + (long)Math.Max(100, recipe.Seconds * 1000 / UpperSpeed(node));
                node.Status = "生産中";
            }
        }
        private VillageNodeView UpperView(Node node) => new VillageNodeView {
            Id = node.Id, Kind = node.Kind, Zoom = node.Zoom, FootprintSize = node.Size, Formed = node.Formed,
            X = node.X, Y = node.Y, Effort = node.Effort, Capacity = 12, Total = node.Stock.Values.Sum(),
            EffortSettings = EffortView(node), ProductionSeconds = RecipeOf(node).Seconds / UpperSpeed(node),
            ComboDirections = UpperComboDirections(node), ComboCount = VillageComboRules.Count(UpperComboDirections(node)),
            ComboMultiplier = VillageComboRules.Multiplier(VillageComboRules.Count(UpperComboDirections(node))),
            Status = node.Status, Progress = node.Finish > 0 ? Math.Min(1, (_at - node.Started) / (double)(node.Finish - node.Started)) : 0,
            Stock = new ReadOnlyDictionary<Item, int>(new Dictionary<Item, int>(node.Stock))
        };
        public VillageResult Deliver(int id, Item item, int count, string requestId)
        {
            if (!Enum.IsDefined(typeof(Item), item) || count <= 0 || string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128)
                return Result(false, "納品の品目・個数・操作IDが不正です");
            if (_deliveries.Contains(requestId)) return Result(false, "この納品は受け取り済みです");
            if (_deliveries.Count >= 100000) return Result(false, "納品記録の上限です");
            var node = _nodes.Concat(_upperNodes).FirstOrDefault(n => n.Id == id);
            if (node == null || node.Held(item) < count) return Result(false, "納品できる在庫が足りません");
            var points = (long)count * VillageHierarchyRules.DeliveryPoints(item);
            if (_deliveryPoints > long.MaxValue - points) return Result(false, "納品点の上限です");
            node.Stock[item] = node.Held(item) - count; _deliveryPoints += points;
            if (VillageHierarchyRules.IsSecondTier(item)) _secondTierDelivered += count;
            _deliveries.Add(requestId);
            ReconcileHierarchy();
            return Result(true, $"{IslandCatalog.NameOf(item)}を{count}個納品しました。+{points}点", id);
        }
        private void RestoreHierarchy(VillageState state)
        {
            if (state.DeliveryPoints < 0 || state.SecondTierDelivered < 0 || state.SecondTierDelivered > state.DeliveryPoints / 4 || state.Deliveries == null || state.Deliveries.Count > 100000
                || state.Deliveries.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 128)
                || state.Deliveries.Distinct(StringComparer.Ordinal).Count() != state.Deliveries.Count
                || state.UpperNodes == null || state.UpperNodes.Count > 200)
                throw new ArgumentException("保存の納品・階層が不正です");
            _deliveryPoints = state.DeliveryPoints; _secondTierDelivered = state.SecondTierDelivered; foreach (var id in state.Deliveries) _deliveries.Add(id);
            foreach (var saved in state.UpperNodes)
            {
                if (saved == null || saved.Zoom < 2 || saved.Zoom > 3 || saved.Zoom > MaximumZoom
                    || saved.Kind != FacilityKind.Preparation && saved.Kind != FacilityKind.Kamaboko
                    || saved.X < 0 || saved.Y < 0 || saved.X + VillageHierarchyRules.GridSize(saved.Zoom) > BoardSize
                    || saved.Y + VillageHierarchyRules.GridSize(saved.Zoom) > BoardSize
                    || saved.X % VillageHierarchyRules.GridSize(saved.Zoom) != 0 || saved.Y % VillageHierarchyRules.GridSize(saved.Zoom) != 0
                    || saved.Id != VillageHierarchyRules.UpperId(saved.Zoom, saved.X, saved.Y, saved.Kind) || _upperNodes.Any(n => n.Id == saved.Id)
                    || saved.Effort < 0 || saved.Effort > 3 || saved.ComboRemainder < 0 || saved.ComboRemainder > 3
                    || saved.Stock == null || saved.Stock.Length != 8 || saved.Stock.Any(n => n < 0 || n > 12)
                    || saved.Started < 0 || saved.Started > _at || saved.Finish < 0
                    || saved.Finish > 0 && (saved.Finish <= _at || saved.Finish <= saved.Started || saved.ProcessYield < 1 || saved.ProcessYield > 2)
                    || saved.Finish == 0 && saved.ProcessYield != 0 || saved.Stock.Sum() + saved.ProcessYield > 12)
                    throw new ArgumentException("保存の上位施設が不正です");
                var node = new Node { Id = saved.Id, Zoom = saved.Zoom, Kind = saved.Kind, X = saved.X, Y = saved.Y,
                    Effort = saved.Effort, Started = saved.Started, Finish = saved.Finish, ProcessYield = saved.ProcessYield,
                    ComboRemainder = saved.ComboRemainder, Status = saved.Finish > 0 ? "生産中" : "待機" };
                for (var i = 0; i < 8; i++) node.Stock[(Item)i] = saved.Stock[i];
                _upperNodes.Add(node);
            }
            ReconcileHierarchy();
        }
    }
}
