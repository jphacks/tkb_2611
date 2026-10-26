using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Domain.Village
{
    [Flags]
    public enum VillageComboDirection { None = 0, Up = 1, Right = 2, Down = 4, Left = 8 }

    public sealed class VillageComboChange
    {
        public int Id { get; internal set; }
        public int Before { get; internal set; }
        public int After { get; internal set; }
    }

    public sealed class VillageComboPreview
    {
        public VillageNodeView Candidate { get; internal set; }
        public IReadOnlyList<VillageNodeView> Nodes { get; internal set; }
        public IReadOnlyList<VillageComboChange> Changes { get; internal set; }
    }

    public static class VillageComboRules
    {
        public const int PercentPerNeighbor = 25;
        public const int MaxNeighbors = 4;
        public const int ElectricityPerBuild = 1;
        public static bool Supports(FacilityKind kind) => IslandCatalog.Get(kind).Recipes.Any(r => r.Output.Count > 0);
        public static double Multiplier(int neighbors) => 1 + neighbors * PercentPerNeighbor / 100.0;

        public static VillageComboDirection Direction(FacilityKind kind, int x, int y, FacilityKind otherKind, int otherX, int otherY)
        {
            if (kind != otherKind || !Supports(kind)) return VillageComboDirection.None;
            var size = IslandCatalog.Get(kind).Size;
            if (otherX == x)
            {
                if (otherY == y + size) return VillageComboDirection.Up;
                if (otherY == y - size) return VillageComboDirection.Down;
            }
            if (otherY == y)
            {
                if (otherX == x + size) return VillageComboDirection.Right;
                if (otherX == x - size) return VillageComboDirection.Left;
            }
            return VillageComboDirection.None;
        }

        public static int Count(VillageComboDirection directions)
        {
            var count = 0;
            for (var bit = 1; bit <= 8; bit <<= 1)
                if (((int)directions & bit) != 0) count++;
            return count;
        }

        private static VillageComboDirection Directions(IReadOnlyList<VillageNodeView> nodes, VillageNodeView target)
        {
            var directions = VillageComboDirection.None;
            foreach (var other in nodes)
                if (other.Id != target.Id) directions |= Direction(target.Kind, target.X, target.Y, other.Kind, other.X, other.Y);
            return directions;
        }

        /// <summary>配置後のコンボだけを見積もる。元の村と保存データは変えない。</summary>
        public static VillageComboPreview Preview(IReadOnlyList<VillageNodeView> nodes, FacilityKind kind, int x, int y, int movingId = 0)
        {
            if (!Supports(kind)) return null;
            var candidate = new VillageNodeView { Id = movingId, Kind = kind, X = x, Y = y };
            var projected = nodes.Where(n => movingId == 0 || n.Id != movingId).Concat(new[] { candidate }).ToArray();
            candidate.ComboDirections = Directions(projected, candidate);
            candidate.ComboCount = Count(candidate.ComboDirections); candidate.ComboMultiplier = Multiplier(candidate.ComboCount);
            var changes = nodes.Where(n => n.Id != movingId).Select(n => new VillageComboChange {
                Id = n.Id, Before = n.ComboCount, After = Count(Directions(projected, n)) }).Where(c => c.Before != c.After).ToArray();
            return new VillageComboPreview { Candidate = candidate, Nodes = Array.AsReadOnly(projected), Changes = Array.AsReadOnly(changes) };
        }
    }

    public sealed partial class VillageGame
    {
        private readonly int[] _buildCounts = new int[Enum.GetValues(typeof(FacilityKind)).Length];
        public double NextBuildElectricityCost(FacilityKind kind) => IslandCatalog.Get(kind).ElectricityCost
            + (double)_buildCounts[(int)kind] * VillageComboRules.ElectricityPerBuild;

        private VillageComboDirection ComboDirections(Node node)
        {
            var directions = VillageComboDirection.None;
            foreach (var other in _nodes)
                if (other.Id != node.Id) directions |= VillageComboRules.Direction(node.Kind, node.X, node.Y, other.Kind, other.X, other.Y);
            return directions;
        }
        private int ComboCount(Node node) => VillageComboRules.Count(ComboDirections(node));

        // Quarter items carry across successful batches, including saves and layout changes.
        private int OutputQuarters(Node node) => RecipeOf(node).Output.Count * (4 + ComboCount(node)) + node.ComboRemainder;
        private int NextOutput(Node node) => OutputQuarters(node) / 4;
        private static int PendingOutput(Node node) => node.Finish > 0 ? node.ProcessYield : 0;
    }
}
