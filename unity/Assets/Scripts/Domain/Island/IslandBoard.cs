using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectPenguin.Domain.Island
{
    /// <summary>マスの地形。</summary>
    public enum TileKind
    {
        /// <summary>海。陸に接していれば開拓できる。</summary>
        Sea,

        /// <summary>水路。港の舟が通る。開拓できない。</summary>
        Canal,

        /// <summary>基礎の島。沈まない。</summary>
        Base,

        /// <summary>開拓した陸。氷が足りないと、後から開拓したものから沈む。</summary>
        Expanded,
    }

    /// <summary>マスの座標。x は左→右、y は手前→奥 (奥の端が水路)。</summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";

        public IEnumerable<Cell> Neighbors4()
        {
            yield return new Cell(X + 1, Y);
            yield return new Cell(X - 1, Y);
            yield return new Cell(X, Y + 1);
            yield return new Cell(X, Y - 1);
        }
    }

    /// <summary>
    /// 10×10 の盤面。地形と開拓の順番だけを持つ (施設は <see cref="IslandGame"/>)。
    /// 沈むマスは「開拓した順の後ろから」決まるので、残る陸は必ず基礎の島とつながったままになる。
    /// </summary>
    public sealed class IslandBoard
    {
        private readonly IslandParameters _p;
        private readonly TileKind[,] _tiles;
        private readonly List<Cell> _expansions = new List<Cell>();
        private readonly Dictionary<Cell, Item> _quality = new Dictionary<Cell, Item>();

        public IslandBoard(IslandParameters parameters)
        {
            _p = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Size = _p.BoardSize;
            _tiles = new TileKind[Size, Size];
            for (var x = 0; x < Size; x++)
            {
                for (var y = 0; y < Size; y++)
                {
                    _tiles[x, y] = y >= Size - _p.CanalRows ? TileKind.Canal : TileKind.Sea;
                }
            }

            for (var x = _p.BaseX; x < _p.BaseX + _p.BaseSize; x++)
            {
                for (var y = _p.BaseY; y < _p.BaseY + _p.BaseSize; y++)
                {
                    _tiles[x, y] = TileKind.Base;
                }
            }

            // 質の良いマスは海に散らしておき、開拓して建てると効く (豊漁・塩の浜・藻場)。
            AddQuality(1, 3, Item.Fish);
            AddQuality(8, 5, Item.Fish);
            AddQuality(2, 0, Item.Salt);
            AddQuality(7, 0, Item.Salt);
            AddQuality(1, 6, Item.Kelp);
            AddQuality(8, 2, Item.Kelp);
        }

        public int Size { get; }

        /// <summary>開拓した順のマス。</summary>
        public IReadOnlyList<Cell> Expansions => _expansions;

        public IReadOnlyDictionary<Cell, Item> QualityTiles => _quality;

        public bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Size && c.Y < Size;

        public TileKind KindAt(Cell c) => InBounds(c) ? _tiles[c.X, c.Y] : TileKind.Sea;

        public Item? QualityAt(Cell c) => _quality.TryGetValue(c, out var item) ? item : (Item?)null;

        /// <summary>開拓した順番 (0 始まり)。開拓していなければ -1。</summary>
        public int ExpansionIndex(Cell c) => _expansions.IndexOf(c);

        /// <summary>氷 <paramref name="ice"/> で支えられる、開拓したマスの数。</summary>
        public int Capacity(double ice) => Math.Max(0, (int)Math.Floor((ice - _p.IceFloor) / _p.IcePerTile + 1e-9));

        /// <summary>沈んでいるか。基礎の島は沈まない。</summary>
        public bool IsSunk(Cell c, double ice)
        {
            var i = ExpansionIndex(c);
            return i >= 0 && i >= Capacity(ice);
        }

        /// <summary>沈んでいる開拓マスの数。</summary>
        public int SunkCount(double ice) => Math.Max(0, _expansions.Count - Capacity(ice));

        /// <summary>氷がもう少し減ると沈むマス (ひびで予告する)。</summary>
        public bool IsWarned(Cell c, double ice)
        {
            var i = ExpansionIndex(c);
            if (i < 0)
            {
                return false;
            }

            var cap = Capacity(ice);
            return i < cap && i >= cap - _p.SinkWarningTiles;
        }

        /// <summary>建物を置ける陸 (基礎か、沈んでいない開拓マス)。</summary>
        public bool IsSolidLand(Cell c, double ice)
        {
            var k = KindAt(c);
            return k == TileKind.Base || (k == TileKind.Expanded && !IsSunk(c, ice));
        }

        /// <summary>水に面している (海・水路・沈んだマス・盤面の外)。</summary>
        public bool IsWaterAt(Cell c, double ice) => !InBounds(c) || !IsSolidLand(c, ice);

        /// <summary>海で、上下左右のどれかが沈んでいない陸。</summary>
        public bool CanExpandAt(Cell c, double ice)
        {
            if (!InBounds(c) || KindAt(c) != TileKind.Sea)
            {
                return false;
            }

            return c.Neighbors4().Any(n => InBounds(n) && IsSolidLand(n, ice));
        }

        public IEnumerable<Cell> ExpandableCells(double ice)
        {
            for (var x = 0; x < Size; x++)
            {
                for (var y = 0; y < Size; y++)
                {
                    var c = new Cell(x, y);
                    if (CanExpandAt(c, ice))
                    {
                        yield return c;
                    }
                }
            }
        }

        internal void Expand(Cell c)
        {
            _tiles[c.X, c.Y] = TileKind.Expanded;
            _expansions.Add(c);
        }

        internal void RestoreExpansions(IEnumerable<Cell> cells)
        {
            foreach (var c in cells)
            {
                if (InBounds(c) && _tiles[c.X, c.Y] == TileKind.Sea)
                {
                    Expand(c);
                }
            }
        }

        private void AddQuality(int x, int y, Item item)
        {
            var c = new Cell(x, y);
            if (InBounds(c) && KindAt(c) == TileKind.Sea)
            {
                _quality[c] = item;
            }
        }
    }
}
