using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Game;

namespace ProjectPenguin.Domain.Island
{
    /// <summary>施設が今どうしているか。動いていない理由は画面にアイコンで出す。</summary>
    public enum FacilityStatus
    {
        /// <summary>動いている (生産しない施設は効いている)。</summary>
        Running,

        /// <summary>沈んだマスにかかっている。氷が戻れば浮かぶ。</summary>
        Submerged,

        /// <summary>海に面していない (漁場・製塩所・海藻場)。</summary>
        NoCoast,

        /// <summary>ペンギンが足りない。</summary>
        NoWorker,

        /// <summary>ガスもオールマイティも無い。</summary>
        NoGas,

        /// <summary>材料が足りない。</summary>
        WaitingInput,

        /// <summary>置き場がいっぱい。</summary>
        StorageFull,

        /// <summary>働くペンギンのごはん (お魚) が足りない。</summary>
        NoFish,
    }

    /// <summary>操作の結果。失敗の理由は画面にそのまま出す。</summary>
    public enum IslandActionResult
    {
        Ok,
        Locked,
        LimitReached,
        CannotPlace,
        NotEnoughElectricity,
        NotEnoughWater,
        NotEnoughFish,
        NotEnoughPearls,
        NotEnoughItems,
        NotEnoughIce,
        NoFreeWorker,
        AlreadyReceived,
        NotFound,
        NotArrived,
        MaxLevel,
    }

    /// <summary>画面の演出のための出来事。</summary>
    public enum IslandEventKind
    {
        Built,
        Demolished,
        Moved,
        Produced,
        Delivered,
        OrderArrived,
        LevelUp,
        Expanded,
        Sunk,
        Surfaced,
        ResourcesGained,
        HarborStageUp,
        Upgraded,

        /// <summary>お魚の桁が上がり、施設が建てられるようになった (<see cref="IslandEvent.Value"/> は <see cref="FacilityKind"/>)。</summary>
        Unlocked,

        /// <summary>実績を満たして、新しい種のペンギンが来た (<see cref="IslandEvent.Value"/> は <see cref="PenguinCatalog.AllIds"/> の番号)。</summary>
        PenguinArrived,
    }

    public readonly struct IslandEvent
    {
        public IslandEvent(IslandEventKind kind, int facilityId = -1, Item? item = null, int count = 0, Cell? cell = null,
            int value = 0)
        {
            Kind = kind;
            FacilityId = facilityId;
            Item = item;
            Count = count;
            Cell = cell;
            Value = value;
        }

        public IslandEventKind Kind { get; }
        public int FacilityId { get; }
        public Item? Item { get; }
        public int Count { get; }
        public Cell? Cell { get; }

        /// <summary>レベル・港の段階など、出来事ごとの値。</summary>
        public int Value { get; }
    }

    /// <summary>島に建てた施設 1 棟。</summary>
    public sealed class Facility
    {
        internal Facility(int id, FacilityKind kind, Cell origin)
        {
            Id = id;
            Kind = kind;
            Origin = origin;
        }

        public int Id { get; }
        public FacilityKind Kind { get; }
        public FacilityDef Def => IslandCatalog.Get(Kind);

        /// <summary>左下 (手前左) のマス。</summary>
        public Cell Origin { get; internal set; }

        public int RecipeIndex { get; internal set; }
        public int Workers { get; internal set; }
        public int Pearls { get; internal set; }

        /// <summary>施設のレベル (1〜<see cref="IslandParameters.FacilityMaxLevel"/>)。</summary>
        public int Level { get; internal set; } = 1;

        /// <summary>今の 1 回の進み (秒)。</summary>
        public double Progress { get; internal set; }

        /// <summary>材料を入れて作っている途中か。</summary>
        public bool Cycling { get; internal set; }

        public FacilityStatus Status { get; internal set; }

        /// <summary>今の 1 回の進み具合 (0〜1)。</summary>
        public double ProgressRatio => Recipe == null ? 0.0 : Math.Min(1.0, Progress / Recipe.Seconds);

        public Recipe Recipe => Def.Recipes.Count == 0 ? null : Def.Recipes[Math.Min(RecipeIndex, Def.Recipes.Count - 1)];

        public IEnumerable<Cell> Footprint()
        {
            var s = Def.Size;
            for (var dx = 0; dx < s; dx++)
            {
                for (var dy = 0; dy < s; dy++)
                {
                    yield return new Cell(Origin.X + dx, Origin.Y + dy);
                }
            }
        }
    }

    /// <summary>港の舟の注文。</summary>
    public sealed class Order
    {
        internal Order(Item item, int count, long reward, int exp, double arrivesAt)
        {
            Item = item;
            Count = count;
            Reward = reward;
            Exp = exp;
            ArrivesAt = arrivesAt;
        }

        public Item Item { get; }
        public int Count { get; }

        /// <summary>納品で受け取るお魚。</summary>
        public long Reward { get; }

        public int Exp { get; }

        /// <summary>舟が着く時刻 (島の経過秒)。</summary>
        public double ArrivesAt { get; }
    }

    /// <summary>品目ごとの毎時の生産量 (生産量タブ)。</summary>
    public readonly struct ItemRate
    {
        public ItemRate(Item item, double producedPerHour, double consumedPerHour)
        {
            Item = item;
            ProducedPerHour = producedPerHour;
            ConsumedPerHour = consumedPerHour;
        }

        public Item Item { get; }
        public double ProducedPerHour { get; }
        public double ConsumedPerHour { get; }
        public double NetPerHour => ProducedPerHour - ConsumedPerHour;
    }

    /// <summary>閉じていた間にできたもの。</summary>
    public sealed class OfflineReport
    {
        private readonly long[] _items = new long[IslandCatalog.Items.Count];

        public double Seconds { get; internal set; }
        public double GasUsed { get; internal set; }

        /// <summary>働くペンギンが食べたお魚。</summary>
        public long FishEaten { get; internal set; }
        public int Delivered { get; internal set; }
        public int Pearls { get; internal set; }

        public long this[Item item] => _items[(int)item];

        public bool IsEmpty => _items.All(v => v == 0) && Delivered == 0 && Pearls == 0;

        internal void Add(Item item, long count) => _items[(int)item] += count;
    }

    /// <summary>
    /// 生産の島の状態と規則。時刻は引数で受け取り (<see cref="Tick"/>)、UnityEngine に依存しない。
    /// 氷・記録日・来たペンギンは <see cref="GameSession"/> から <see cref="SyncWorld"/> で受け取る。
    /// </summary>
    public sealed class IslandGame
    {
        /// <summary>この秒数を超える Tick は「閉じていた間」とみなし、細かい出来事を出さずに報告にまとめる。</summary>
        public const double OfflineThresholdSeconds = 5.0;

        /// <summary>閉じていた間をまとめて計算するときの刻みの数の上限。</summary>
        private const double MaxOfflineSteps = 2000.0;

        private readonly IslandParameters _p;
        private readonly double[] _resources = new double[Enum.GetValues(typeof(Resource)).Length];
        private readonly long[] _stock = new long[IslandCatalog.Items.Count];
        private readonly List<Facility> _facilities = new List<Facility>();
        private readonly Order[] _orders;
        private readonly List<IslandEvent> _events = new List<IslandEvent>();
        private readonly List<string> _species = new List<string>();
        private readonly List<string> _sessionSpecies = new List<string>();
        private readonly List<string> _achievedSpecies = new List<string>();
        private readonly long[] _made = new long[IslandCatalog.Items.Count];
        private readonly HashSet<int> _photoCategories = new HashSet<int>();
        private double _fishDebt;

        private int _nextId = 1;
        private int _orderSerial;
        private bool _recordEvents = true;
        private OfflineReport _offline;

        public IslandGame(IslandParameters parameters, DateTimeOffset now)
        {
            _p = parameters ?? IslandParameters.Default;
            Board = new IslandBoard(_p);
            _orders = new Order[_p.OrderSlots];
            _resources[(int)Resource.Electricity] = _p.StartElectricity;
            _resources[(int)Resource.Water] = _p.StartWater;
            _resources[(int)Resource.Gas] = _p.StartGas;
            _resources[(int)Resource.AllMighty] = _p.StartAllMighty;
            _stock[(int)Item.Fish] = (long)_p.StartFish;
            MaxFishHeld = (long)_p.StartFish;
            Level = 1;
            Ice = 100.0;
            LastTickUtc = now;
            PhotoDay = int.MinValue;
            StepDay = int.MinValue;
            for (var i = 0; i < _orders.Length; i++)
            {
                _orders[i] = NewOrder(0.0);
            }
        }

        public IslandParameters Parameters => _p;
        public IslandBoard Board { get; }
        public IReadOnlyList<Facility> Facilities => _facilities;
        public IReadOnlyList<Order> Orders => _orders;
        public int Level { get; private set; }
        public int Exp { get; private set; }
        public int ExpToNext => _p.ExpToNext(Level);

        /// <summary>島が動いてきた合計の秒数 (注文の到着に使う)。</summary>
        public double PlayTime { get; private set; }

        public DateTimeOffset LastTickUtc { get; private set; }
        public int Delivered { get; private set; }

        /// <summary>今の氷 (<see cref="GameSession.Ice"/>)。</summary>
        public double Ice { get; private set; }

        public int RecordDays { get; private set; }

        /// <summary>来た種 (<see cref="GameSession"/> の節目の来訪と、島の実績で解放した種を合わせたもの)。</summary>
        public IReadOnlyList<string> Species => _species;

        /// <summary>島の実績で解放した種 (<see cref="PenguinUnlocks"/>)。</summary>
        public IReadOnlyList<string> AchievedSpecies => _achievedSpecies;

        /// <summary>氷の段 (T0〜T4。<see cref="GameSession.Tier"/>)。ペンギンのやる気を決める。</summary>
        public int IceTier { get; private set; }

        /// <summary>ペンギンのやる気。氷が大きいほど上がり、施設の効果 (作る速さ・増幅・氷室) に掛かる。</summary>
        public double Motivation => _p.MotivationByTier[Math.Max(0, Math.Min(_p.MotivationByTier.Length - 1, IceTier))];

        /// <summary>これまでに持ったお魚の最大。施設の解放 (お魚の桁) に使う。</summary>
        public long MaxFishHeld { get; private set; }

        /// <summary>これまでに作った品目の数 (実績に使う)。</summary>
        public long MadeOf(Item item) => _made[(int)item];

        public long PearlsMade { get; private set; }

        /// <summary>これまでに写真で撮った分類の数 (実績に使う)。</summary>
        public int PhotoCategoryCount => _photoCategories.Count;

        public int PhotoDay { get; private set; }
        public int PhotosToday { get; private set; }
        private int _quickEcoDay = int.MinValue;
        private readonly HashSet<string> _quickEcoIds = new HashSet<string>(StringComparer.Ordinal);

        public int QuickEcoCount(int appDay) => appDay == _quickEcoDay ? _quickEcoIds.Count : 0;

        public bool CanReceiveQuickEco(string id, int appDay) =>
            QuickEcoActions.Find(id) != null && appDay >= _quickEcoDay &&
            (appDay > _quickEcoDay || (_quickEcoIds.Count < QuickEcoActions.DailyLimit && !_quickEcoIds.Contains(id)));

        public double PreviewQuickEcoReward() => Math.Round(QuickEcoActions.BaseElectricity * GainMultiplier(Resource.Electricity), 1);

        /// <summary>時計の先送りが保留されていない、既存Sessionの有効日だけを渡す。</summary>
        public void ObserveQuickEcoDay(int appDay)
        {
            if (appDay <= _quickEcoDay) return;
            _quickEcoDay = appDay;
            _quickEcoIds.Clear();
        }

        /// <summary>履歴と資源を一緒に更新する。呼出側は現在の有効日を渡す。</summary>
        public ResourceBundle ReceiveQuickEco(string id, int appDay)
        {
            if (!CanReceiveQuickEco(id, appDay)) return null;
            ObserveQuickEcoDay(appDay);

            _quickEcoIds.Add(id);
            return Grant(ResourceBundle.Of(Resource.Electricity, QuickEcoActions.BaseElectricity));
        }

        public int StepDay { get; private set; }
        public double StepGasToday { get; private set; }

        /// <summary>最後に閉じていた間の報告。表示したら <see cref="TakeOfflineReport"/> で消す。</summary>
        public OfflineReport PendingOfflineReport { get; private set; }

        public double Get(Resource r) => _resources[(int)r];
        public long StockOf(Item item) => _stock[(int)item];
        public long Fish => _stock[(int)Item.Fish];

        public int StorageLimit => _p.BaseStorage + Active(FacilityRole.Storage).Sum(f => _p.StoragePerWarehouse * f.Level);
        public int Housing => _p.BaseHousing + Active(FacilityRole.Housing).Sum(HousingOf);

        /// <summary>宿舎 1 棟の定員 (レベル込み)。</summary>
        public int HousingOf(Facility f) => _p.HousingPerDormitory + (f.Level - 1) * _p.UpgradeHousingPerLevel;

        /// <summary>増幅所 1 棟の上乗せ (レベル込み)。</summary>
        public double AmplifierBonusOf(Facility f) => _p.AmplifierBonus + (f.Level - 1) * _p.UpgradeAmplifierPerLevel;

        /// <summary>納品してから次の舟が来るまで (動いている仕分け所のレベルで縮む)。</summary>
        public double OrderCooldown
        {
            get
            {
                var sorter = _facilities.Where(f => f.Def.Role == FacilityRole.AutoDeliver && f.Status == FacilityStatus.Running)
                    .Select(f => f.Level).DefaultIfEmpty(0).Max();
                return Math.Max(5.0, _p.OrderCooldownSeconds - sorter * _p.UpgradeOrderCooldownPerLevel);
            }
        }
        public int Population => Math.Min(Housing, _p.StartWorkers + RecordDays * _p.WorkersPerRecordDay);

        /// <summary>まだ記録が足りず、宿舎があっても住めないペンギン (宿舎が足りないぶんは含まない)。</summary>
        public int HomelessPenguins => Math.Max(0, _p.StartWorkers + RecordDays * _p.WorkersPerRecordDay - Housing);

        public int AssignedWorkers => _facilities.Sum(f => f.Workers);
        public int FreeWorkers => Math.Max(0, Population - AssignedWorkers);

        public int HarborStage
        {
            get
            {
                var s = 0;
                foreach (var need in _p.HarborStageDeliveries)
                {
                    if (Delivered >= need)
                    {
                        s++;
                    }
                }

                return s;
            }
        }

        /// <summary>開拓できる残りのマス数 (氷とレベルの両方の上限)。</summary>
        public int ExpandRoom => Math.Max(0, Math.Min(Board.Capacity(Ice), _p.ExpandLimit(Level)) - Board.Expansions.Count);

        public double ExpandWaterCost => _p.ExpandWaterCost(Board.Expansions.Count);
        public double ExpandFishCost => _p.ExpandFishCost(Board.Expansions.Count);

        // ------------------------------------------------------------------
        // 外の世界 (GameSession と現実の行動)
        // ------------------------------------------------------------------

        /// <summary>
        /// 氷・記録日・来たペンギン・氷の段を受け取る。沈む・浮かぶマスがあれば出来事にする。
        /// 段を渡さなければ今の段のまま (Systems は <see cref="GameSession.Tier"/> を毎回渡す)。
        /// </summary>
        public void SyncWorld(double ice, int recordDays, IEnumerable<string> species, int? tier = null)
        {
            var beforeSunk = Board.Expansions.Where(c => Board.IsSunk(c, Ice)).ToList();
            Ice = ice;
            RecordDays = Math.Max(0, recordDays);
            if (tier.HasValue)
            {
                IceTier = tier.Value;
            }

            _sessionSpecies.Clear();
            if (species != null)
            {
                _sessionSpecies.AddRange(species.Distinct());
            }

            RebuildSpecies();

            var afterSunk = Board.Expansions.Where(c => Board.IsSunk(c, Ice)).ToList();
            foreach (var c in afterSunk.Except(beforeSunk))
            {
                Emit(new IslandEvent(IslandEventKind.Sunk, cell: c));
            }

            foreach (var c in beforeSunk.Except(afterSunk))
            {
                Emit(new IslandEvent(IslandEventKind.Surfaced, cell: c));
            }

            TrimWorkers();
            RefreshStatuses();
            CheckAchievements();
        }

        /// <summary>
        /// 今日の写真を受け取る。1 日 <see cref="IslandParameters.PhotosPerDay"/> 枚まで。
        /// 最後に受け取った日より前の日 (時計を戻した) の写真は資源にしない。
        /// </summary>
        public ResourceBundle ReceivePhoto(EcoPhotoVerdict verdict, int appDay)
        {
            if (!CanReceivePhoto(appDay))
            {
                return null;
            }

            if (PhotoDay != appDay)
            {
                PhotoDay = appDay;
                PhotosToday = 0;
            }

            PhotosToday++;
            if (verdict != null && verdict.Category != EcoCategory.Unknown)
            {
                _photoCategories.Add((int)verdict.Category);
            }

            var got = Grant(EcoGrant.ForPhoto(verdict, _p));
            CheckAchievements();
            return got;
        }

        public bool CanReceivePhoto(int appDay) =>
            appDay > PhotoDay || (appDay == PhotoDay && PhotosToday < _p.PhotosPerDay);

        /// <summary>
        /// 今日の歩数 (累計) を受け取り、まだ渡していない分のガスを渡す。
        /// 最後に受け取った日より前の日 (時計を戻した・前日の問い合わせが遅れて届いた) の歩数は資源にしない。
        /// </summary>
        public double ReceiveSteps(int stepsToday, int appDay)
        {
            if (appDay < StepDay)
            {
                return 0;
            }

            if (StepDay != appDay)
            {
                StepDay = appDay;
                StepGasToday = 0;
            }

            var gas = EcoGrant.GasForSteps(stepsToday, StepGasToday, _p);
            if (gas <= 0)
            {
                return 0;
            }

            StepGasToday += gas;
            var granted = Grant(ResourceBundle.Of(Resource.Gas, gas));
            return granted[Resource.Gas];
        }

        public ResourceBundle ReceiveCheckIn(CheckInAnswer answer) => Grant(EcoGrant.ForCheckIn(answer, _p));

        /// <summary>現実から得た資源に倍率 (増幅施設・ペンギン) を掛けて受け取る。受け取った量を返す。</summary>
        public ResourceBundle Grant(ResourceBundle bundle)
        {
            var got = new ResourceBundle();
            foreach (var kv in bundle.NonZero())
            {
                var v = kv.Value * GainMultiplier(kv.Key);
                v = Math.Round(v, 1);
                _resources[(int)kv.Key] += v;
                got[kv.Key] = v;
            }

            if (!got.IsEmpty)
            {
                Emit(new IslandEvent(IslandEventKind.ResourcesGained));
            }

            return got;
        }

        /// <summary>電気・水・ガスを受け取るときの倍率。</summary>
        public double GainMultiplier(Resource r)
        {
            if (r != Resource.Electricity && r != Resource.Water && r != Resource.Gas)
            {
                return 1.0;
            }

            var amps = _facilities.Where(f => f.Def.Amplifies == r && f.Status == FacilityStatus.Running).Sum(AmplifierBonusOf);
            return (1.0 + amps * Motivation) * PenguinDex.ResourceMultiplier(_species, r);
        }

        /// <summary>氷室 1 棟の効き目 (レベル込み、やる気の前)。</summary>
        public double IceHouseReductionOf(Facility f) => _p.IceHouseReduction + (f.Level - 1) * _p.IceHouseReductionPerLevel;

        /// <summary>
        /// 記録しなかった日に溶ける氷を減らす割合 (動いている氷室 × やる気、上限あり)。
        /// 氷の計算は <see cref="GameSession"/> にあるので、Systems がこの値を <see cref="GameSession.SetMeltReduction"/> に渡す。
        /// </summary>
        public double MeltReduction
        {
            get
            {
                var best = _facilities.Where(f => f.Def.Role == FacilityRole.IceGuard && f.Status == FacilityStatus.Running)
                    .Select(IceHouseReductionOf).DefaultIfEmpty(0.0).Max();
                return Math.Round(Math.Min(_p.MaxMeltReduction, best * Motivation), 4);
            }
        }

        // ------------------------------------------------------------------
        // 操作
        // ------------------------------------------------------------------

        /// <summary>お魚の桁で解放されているか (これまでに持ったお魚の最大で数える)。</summary>
        public bool IsUnlocked(FacilityKind kind) => MaxFishHeld >= IslandCatalog.Get(kind).UnlockFish;

        /// <summary>次に開く段のお魚の数。すべて開いていれば null。</summary>
        public long? NextUnlockFish => IslandCatalog.UnlockSteps.Where(n => n > MaxFishHeld).Select(n => (long?)n).FirstOrDefault();

        public int CountOf(FacilityKind kind) => _facilities.Count(f => f.Kind == kind);

        public Facility Find(int id) => _facilities.FirstOrDefault(f => f.Id == id);

        public Facility FacilityAt(Cell c) => _facilities.FirstOrDefault(f => f.Footprint().Contains(c));

        /// <summary>建てられるか (お金を除く)。</summary>
        public IslandActionResult CanPlace(FacilityKind kind, Cell origin, int ignoreId = -1)
        {
            var def = IslandCatalog.Get(kind);
            for (var dx = 0; dx < def.Size; dx++)
            {
                for (var dy = 0; dy < def.Size; dy++)
                {
                    var c = new Cell(origin.X + dx, origin.Y + dy);
                    if (!Board.InBounds(c) || !Board.IsSolidLand(c, Ice))
                    {
                        return IslandActionResult.CannotPlace;
                    }

                    var other = FacilityAt(c);
                    if (other != null && other.Id != ignoreId)
                    {
                        return IslandActionResult.CannotPlace;
                    }
                }
            }

            return IslandActionResult.Ok;
        }

        public IslandActionResult CanBuild(FacilityKind kind)
        {
            var def = IslandCatalog.Get(kind);
            if (!IsUnlocked(kind))
            {
                return IslandActionResult.Locked;
            }

            if (CountOf(kind) >= def.MaxCount)
            {
                return IslandActionResult.LimitReached;
            }

            if (!CanAfford(Resource.Electricity, def.ElectricityCost))
            {
                return IslandActionResult.NotEnoughElectricity;
            }

            if (def.WaterCost > 0 && !CanAfford(Resource.Water, def.WaterCost))
            {
                return IslandActionResult.NotEnoughWater;
            }

            return Fish < def.FishCost ? IslandActionResult.NotEnoughFish : IslandActionResult.Ok;
        }

        public IslandActionResult Build(FacilityKind kind, Cell origin)
        {
            var can = CanBuild(kind);
            if (can != IslandActionResult.Ok)
            {
                return can;
            }

            var place = CanPlace(kind, origin);
            if (place != IslandActionResult.Ok)
            {
                return place;
            }

            var def = IslandCatalog.Get(kind);
            Spend(Resource.Electricity, def.ElectricityCost);
            if (def.WaterCost > 0)
            {
                Spend(Resource.Water, def.WaterCost);
            }

            _stock[(int)Item.Fish] -= (long)def.FishCost;
            var f = new Facility(_nextId++, kind, origin);
            _facilities.Add(f);
            f.Workers = Math.Min(def.Workers, FreeWorkers);
            AddExp(5 + def.UnlockTier * 3);
            RefreshStatuses();
            Emit(new IslandEvent(IslandEventKind.Built, f.Id));
            CheckAchievements();
            return IslandActionResult.Ok;
        }

        public IslandActionResult Move(int id, Cell origin)
        {
            var f = Find(id);
            if (f == null)
            {
                return IslandActionResult.NotFound;
            }

            var place = CanPlace(f.Kind, origin, id);
            if (place != IslandActionResult.Ok)
            {
                return place;
            }

            f.Origin = origin;
            RefreshStatuses();
            Emit(new IslandEvent(IslandEventKind.Moved, f.Id));
            return IslandActionResult.Ok;
        }

        /// <summary>取り壊す。建てたときの半分が戻る。</summary>
        public IslandActionResult Demolish(int id)
        {
            var f = Find(id);
            if (f == null)
            {
                return IslandActionResult.NotFound;
            }

            _resources[(int)Resource.Electricity] += Math.Floor(f.Def.ElectricityCost / 2);
            _resources[(int)Resource.Water] += Math.Floor(f.Def.WaterCost / 2);
            _stock[(int)Item.Fish] += (long)Math.Floor(f.Def.FishCost / 2);
            NoteFish();
            _resources[(int)Resource.Pearl] += f.Pearls;
            _facilities.Remove(f);
            TrimWorkers();
            RefreshStatuses();
            Emit(new IslandEvent(IslandEventKind.Demolished, id));
            return IslandActionResult.Ok;
        }

        /// <summary>配属するペンギンの数を変える (0〜施設の定員)。</summary>
        public IslandActionResult SetWorkers(int id, int workers)
        {
            var f = Find(id);
            if (f == null)
            {
                return IslandActionResult.NotFound;
            }

            var target = Math.Max(0, Math.Min(f.Def.Workers, workers));
            if (target - f.Workers > FreeWorkers)
            {
                return IslandActionResult.NoFreeWorker;
            }

            f.Workers = target;
            RefreshStatuses();
            return IslandActionResult.Ok;
        }

        public IslandActionResult SetRecipe(int id, int recipeIndex)
        {
            var f = Find(id);
            if (f == null || recipeIndex < 0 || recipeIndex >= f.Def.Recipes.Count)
            {
                return IslandActionResult.NotFound;
            }

            f.RecipeIndex = recipeIndex;
            return IslandActionResult.Ok;
        }

        /// <summary>強化できるか。島のレベルより上と、<see cref="IslandParameters.FacilityMaxLevel"/> より上には上げられない。</summary>
        public IslandActionResult CanUpgrade(int id)
        {
            var f = Find(id);
            if (f == null)
            {
                return IslandActionResult.NotFound;
            }

            if (f.Level >= _p.FacilityMaxLevel)
            {
                return IslandActionResult.MaxLevel;
            }

            if (f.Level >= Level)
            {
                return IslandActionResult.Locked;
            }

            if (!CanAfford(Resource.Electricity, UpgradeElectricityCost(f)))
            {
                return IslandActionResult.NotEnoughElectricity;
            }

            return Fish < UpgradeFishCost(f) ? IslandActionResult.NotEnoughFish : IslandActionResult.Ok;
        }

        public double UpgradeElectricityCost(Facility f) => _p.UpgradeElectricityCost(f.Def, f.Level);

        public double UpgradeFishCost(Facility f) => _p.UpgradeFishCost(f.Def, f.Level);

        /// <summary>施設を 1 レベル上げる。</summary>
        public IslandActionResult Upgrade(int id)
        {
            var can = CanUpgrade(id);
            if (can != IslandActionResult.Ok)
            {
                return can;
            }

            var f = Find(id);
            Spend(Resource.Electricity, UpgradeElectricityCost(f));
            _stock[(int)Item.Fish] -= (long)UpgradeFishCost(f);
            f.Level++;
            AddExp(3 + f.Level * 2);
            RefreshStatuses();
            Emit(new IslandEvent(IslandEventKind.Upgraded, f.Id, value: f.Level));
            CheckAchievements();
            return IslandActionResult.Ok;
        }

        public IslandActionResult AddPearl(int id)
        {
            var f = Find(id);
            if (f == null || f.Def.Role != FacilityRole.Producer)
            {
                return IslandActionResult.NotFound;
            }

            if (f.Pearls >= _p.PearlsPerFacility)
            {
                return IslandActionResult.LimitReached;
            }

            if (Get(Resource.Pearl) < 1)
            {
                return IslandActionResult.NotEnoughPearls;
            }

            _resources[(int)Resource.Pearl] -= 1;
            f.Pearls++;
            return IslandActionResult.Ok;
        }

        public IslandActionResult CanExpand(Cell c)
        {
            if (Board.SunkCount(Ice) > 0 || Board.Expansions.Count >= Board.Capacity(Ice))
            {
                return IslandActionResult.NotEnoughIce;
            }

            if (Board.Expansions.Count >= _p.ExpandLimit(Level))
            {
                return IslandActionResult.LimitReached;
            }

            if (!Board.CanExpandAt(c, Ice))
            {
                return IslandActionResult.CannotPlace;
            }

            if (!CanAfford(Resource.Water, ExpandWaterCost))
            {
                return IslandActionResult.NotEnoughWater;
            }

            return Fish < ExpandFishCost ? IslandActionResult.NotEnoughFish : IslandActionResult.Ok;
        }

        /// <summary>海を凍らせて陸にする。</summary>
        public IslandActionResult Expand(Cell c)
        {
            var can = CanExpand(c);
            if (can != IslandActionResult.Ok)
            {
                return can;
            }

            Spend(Resource.Water, ExpandWaterCost);
            _stock[(int)Item.Fish] -= (long)ExpandFishCost;
            Board.Expand(c);
            AddExp(_p.ExpandExp);
            RefreshStatuses();
            Emit(new IslandEvent(IslandEventKind.Expanded, cell: c));
            CheckAchievements();
            return IslandActionResult.Ok;
        }

        public bool IsOrderReady(int slot) => slot >= 0 && slot < _orders.Length && _orders[slot] != null &&
                                              _orders[slot].ArrivesAt <= PlayTime + 1e-9;

        public IslandActionResult Deliver(int slot)
        {
            if (slot < 0 || slot >= _orders.Length || _orders[slot] == null)
            {
                return IslandActionResult.NotFound;
            }

            var o = _orders[slot];
            if (!IsOrderReady(slot))
            {
                return IslandActionResult.NotArrived;
            }

            if (StockOf(o.Item) < o.Count)
            {
                return IslandActionResult.NotEnoughItems;
            }

            var stageBefore = HarborStage;
            _stock[(int)o.Item] -= o.Count;
            _stock[(int)Item.Fish] += o.Reward;
            Delivered++;
            AddExp(o.Exp);
            _orders[slot] = NewOrder(PlayTime + OrderCooldown);
            if (_offline != null)
            {
                _offline.Delivered++;
            }

            Emit(new IslandEvent(IslandEventKind.Delivered, item: o.Item, count: o.Count, value: (int)Math.Min(int.MaxValue, o.Reward)));
            if (HarborStage > stageBefore)
            {
                Emit(new IslandEvent(IslandEventKind.HarborStageUp, value: HarborStage));
            }

            NoteFish();
            CheckAchievements();
            return IslandActionResult.Ok;
        }

        // ------------------------------------------------------------------
        // 時間
        // ------------------------------------------------------------------

        /// <summary>
        /// <paramref name="now"/> まで島を進める。閉じていた時間が長くても <see cref="IslandParameters.OfflineCapSeconds"/> まで。
        /// 時計が戻っていれば何もしない。
        /// </summary>
        public void Tick(DateTimeOffset now)
        {
            var elapsed = (now - LastTickUtc).TotalSeconds;
            if (elapsed <= 0)
            {
                return;
            }

            LastTickUtc = now;
            Simulate(Math.Min(elapsed, _p.OfflineCapSeconds));
        }

        /// <summary>秒数を指定して進める (テストと開発用の早送り)。</summary>
        public void Simulate(double seconds)
        {
            if (seconds <= 0)
            {
                return;
            }

            var offline = seconds > OfflineThresholdSeconds;
            if (offline)
            {
                _offline = new OfflineReport { Seconds = seconds };
                _recordEvents = false;
            }

            // 長い留守は刻みを粗くする (1 回の刻みで何回でも作り終えられるので、量は変わらない)。
            var stepSize = offline ? Math.Max(_p.TickSeconds, seconds / MaxOfflineSteps) : _p.TickSeconds;
            var remaining = seconds;
            while (remaining > 1e-9)
            {
                var dt = Math.Min(stepSize, remaining);
                Step(dt);
                remaining -= dt;
            }

            if (offline)
            {
                _recordEvents = true;
                if (!_offline.IsEmpty || _offline.GasUsed > 0 || _offline.FishEaten > 0)
                {
                    PendingOfflineReport = _offline;
                }

                _offline = null;
            }
        }

        /// <summary>開発用: レベルを少なくとも <paramref name="level"/> にする (テストと開発ビルドのメニュー)。</summary>
        public void DevSetLevel(int level)
        {
            Level = Math.Max(Level, Math.Min(_p.MaxLevel, level));
            RefreshStatuses();
        }

        /// <summary>開発用: 品目を足す。</summary>
        public void DevAddStock(Item item, long count)
        {
            _stock[(int)item] = Math.Max(0, _stock[(int)item] + count);
            NoteFish();
            CheckAchievements();
        }

        /// <summary>開発用: これまでに持ったお魚の最大を少なくとも <paramref name="fish"/> にする (施設の解放)。</summary>
        public void DevSetMaxFish(long fish)
        {
            if (fish > MaxFishHeld)
            {
                UnlockUpTo(fish);
            }
        }

        public OfflineReport TakeOfflineReport()
        {
            var r = PendingOfflineReport;
            PendingOfflineReport = null;
            return r;
        }

        /// <summary>前回呼んでからの出来事を取り出す。</summary>
        public IReadOnlyList<IslandEvent> DrainEvents()
        {
            var list = _events.ToList();
            _events.Clear();
            return list;
        }

        // ------------------------------------------------------------------
        // 見るための値
        // ------------------------------------------------------------------

        /// <summary>施設の速さ (質の良いマス・真珠・ペンギン)。</summary>
        public double SpeedOf(Facility f)
        {
            var s = 1.0;
            if (f.Def.QualityItem.HasValue && f.Footprint().Any(c => Board.QualityAt(c) == f.Def.QualityItem))
            {
                s *= _p.QualityTileSpeed;
            }

            s *= 1.0 + f.Pearls * _p.PearlSpeed;
            s *= 1.0 + (f.Level - 1) * _p.UpgradeSpeedPerLevel;
            s *= PenguinDex.FacilityMultiplier(_species, f.Kind);
            s *= Motivation;
            return s;
        }

        public bool HasCoast(Facility f)
        {
            var cells = new HashSet<Cell>(f.Footprint());
            return cells.SelectMany(c => c.Neighbors4()).Where(n => !cells.Contains(n)).Any(n => Board.IsWaterAt(n, Ice));
        }

        /// <summary>品目ごとの毎時の生産量と消費量 (動ける施設が材料も足りていると仮定した値)。</summary>
        public IReadOnlyList<ItemRate> Rates()
        {
            var made = new double[IslandCatalog.Items.Count];
            var used = new double[IslandCatalog.Items.Count];
            foreach (var f in _facilities)
            {
                var r = f.Recipe;
                if (r == null || IslandCatalog.ProducesPearl(f.Kind) || IsStructurallyStopped(f))
                {
                    continue;
                }

                var perHour = 3600.0 / (r.Seconds / SpeedOf(f));
                made[(int)r.Output.Item] += perHour * r.Output.Count;
                foreach (var input in r.Inputs)
                {
                    used[(int)input.Item] += perHour * input.Count;
                }
            }

            used[(int)Item.Fish] += FishUpkeepPerHour;
            return IslandCatalog.Items.Select(i => new ItemRate(i, made[(int)i], used[(int)i])).ToList();
        }

        /// <summary>動ける施設の働くペンギンが食べるお魚 (毎時)。</summary>
        public double FishUpkeepPerHour => _facilities.Where(f => !IsStructurallyStopped(f)).Sum(f => f.Def.FishPerHour);

        /// <summary>動いている施設の毎時のガス。</summary>
        public double GasPerHour => _facilities.Where(f => f.Recipe != null && !IsStructurallyStopped(f)).Sum(f => f.Recipe.GasPerHour);

        /// <summary>今のガス (オールマイティを含む) で何時間動けるか。使っていなければ無限大。</summary>
        public double GasHoursLeft => GasPerHour <= 0 ? double.PositiveInfinity : (Get(Resource.Gas) + Get(Resource.AllMighty)) / GasPerHour;

        // ------------------------------------------------------------------
        // 保存
        // ------------------------------------------------------------------

        public IslandSave ToSave()
        {
            return new IslandSave
            {
                version = IslandSave.CurrentVersion,
                resources = _resources.ToArray(),
                stock = _stock.ToArray(),
                level = Level,
                exp = Exp,
                nextId = _nextId,
                orderSerial = _orderSerial,
                delivered = Delivered,
                playTime = PlayTime,
                lastTickUnixMs = LastTickUtc.ToUnixTimeMilliseconds(),
                photoDay = PhotoDay,
                photosToday = PhotosToday,
                quickEcoDay = _quickEcoDay,
                quickEcoIds = _quickEcoIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                stepDay = StepDay,
                stepGasToday = StepGasToday,
                maxFishHeld = MaxFishHeld,
                fishDebt = _fishDebt,
                made = _made.ToArray(),
                pearlsMade = PearlsMade,
                photoCategories = _photoCategories.OrderBy(c => c).ToArray(),
                achievedSpecies = _achievedSpecies.ToArray(),
                expansionsX = Board.Expansions.Select(c => c.X).ToArray(),
                expansionsY = Board.Expansions.Select(c => c.Y).ToArray(),
                facilities = _facilities.Select(f => new FacilitySave
                {
                    id = f.Id,
                    kind = (int)f.Kind,
                    x = f.Origin.X,
                    y = f.Origin.Y,
                    recipe = f.RecipeIndex,
                    workers = f.Workers,
                    pearls = f.Pearls,
                    level = f.Level,
                    progress = f.Progress,
                    cycling = f.Cycling,
                }).ToArray(),
                orders = _orders.Select(o => o == null
                    ? new OrderSave { item = -1 }
                    : new OrderSave { item = (int)o.Item, count = o.Count, reward = o.Reward, exp = o.Exp, arrivesAt = o.ArrivesAt }).ToArray(),
            };
        }

        public static IslandGame FromSave(IslandSave save, IslandParameters parameters, DateTimeOffset fallbackNow)
        {
            var p = parameters ?? IslandParameters.Default;
            var lastTick = save != null && save.lastTickUnixMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(save.lastTickUnixMs)
                : fallbackNow;
            var g = new IslandGame(p, lastTick);
            if (save == null)
            {
                return g;
            }

            CopyInto(save.resources, g._resources);
            if (save.stock != null)
            {
                for (var i = 0; i < Math.Min(save.stock.Length, g._stock.Length); i++)
                {
                    g._stock[i] = save.stock[i];
                }
            }

            g.Level = Math.Max(1, save.level);
            g.Exp = Math.Max(0, save.exp);
            g._nextId = Math.Max(1, save.nextId);
            g._orderSerial = save.orderSerial;
            g.Delivered = save.delivered;
            g.PlayTime = save.playTime;
            g.PhotoDay = save.photoDay;
            g.PhotosToday = save.photosToday;
            // 古い保存では配列が無い。日付の既定値 0 を受取日とは解釈しない。
            if (save.quickEcoIds != null)
            {
                g._quickEcoDay = save.quickEcoDay;
                g._quickEcoIds.UnionWith(save.quickEcoIds.Where(id => QuickEcoActions.Find(id) != null));
            }
            g.StepDay = save.stepDay;
            g.StepGasToday = save.stepGasToday;
            g._fishDebt = Math.Max(0.0, save.fishDebt);
            g.PearlsMade = save.pearlsMade;
            if (save.made != null)
            {
                for (var i = 0; i < Math.Min(save.made.Length, g._made.Length); i++)
                {
                    g._made[i] = save.made[i];
                }
            }

            if (save.photoCategories != null)
            {
                g._photoCategories.UnionWith(save.photoCategories);
            }

            if (save.achievedSpecies != null)
            {
                g._achievedSpecies.AddRange(save.achievedSpecies.Where(PenguinCatalog.IsKnown).Distinct());
            }
            if (save.expansionsX != null && save.expansionsY != null)
            {
                g.Board.RestoreExpansions(save.expansionsX.Zip(save.expansionsY, (x, y) => new Cell(x, y)));
            }

            if (save.facilities != null)
            {
                foreach (var fs in save.facilities)
                {
                    if (!Enum.IsDefined(typeof(FacilityKind), fs.kind))
                    {
                        continue;
                    }

                    var f = new Facility(fs.id, (FacilityKind)fs.kind, new Cell(fs.x, fs.y))
                    {
                        RecipeIndex = fs.recipe,
                        Workers = fs.workers,
                        Pearls = fs.pearls,
                        Level = Math.Max(1, fs.level),
                        Progress = fs.progress,
                        Cycling = fs.cycling,
                    };
                    g._facilities.Add(f);
                    g._nextId = Math.Max(g._nextId, f.Id + 1);
                }
            }

            if (save.orders != null)
            {
                for (var i = 0; i < Math.Min(save.orders.Length, g._orders.Length); i++)
                {
                    var o = save.orders[i];
                    g._orders[i] = o.item < 0 || !Enum.IsDefined(typeof(Item), o.item)
                        ? g.NewOrder(g.PlayTime)
                        : new Order((Item)o.item, o.count, o.reward, o.exp, o.arrivesAt);
                }
            }

            // お魚の桁の解放を入れる前 (版 1) の保存は、持っているお魚と建てた施設が開いている所までを解放済みにする。
            var built = g._facilities.Select(f => f.Def.UnlockFish).DefaultIfEmpty(0).Max();
            g.MaxFishHeld = Math.Max(save.maxFishHeld, Math.Max(g.Fish, built));
            g.RebuildSpecies();
            g.RefreshStatuses();
            return g;
        }

        private static void CopyInto(double[] from, double[] to)
        {
            if (from == null)
            {
                return;
            }

            for (var i = 0; i < Math.Min(from.Length, to.Length); i++)
            {
                to[i] = from[i];
            }
        }

        // ------------------------------------------------------------------
        // 内部
        // ------------------------------------------------------------------

        private void Step(double dt)
        {
            PlayTime += dt;
            RefreshStatuses();
            foreach (var f in _facilities)
            {
                if (f.Def.Role == FacilityRole.Producer)
                {
                    StepProducer(f, dt);
                }
                else if (f.Status == FacilityStatus.Running && !PayFish(f.Def.FishPerHour * dt / 3600.0))
                {
                    // 生産しない施設も、働くペンギンのごはんが無ければ効かない。
                    f.Status = FacilityStatus.NoFish;
                }
            }

            if (_facilities.Any(f => f.Def.Role == FacilityRole.AutoDeliver && f.Status == FacilityStatus.Running))
            {
                for (var i = 0; i < _orders.Length; i++)
                {
                    if (IsOrderReady(i) && StockOf(_orders[i].Item) >= _orders[i].Count)
                    {
                        Deliver(i);
                    }
                }
            }

            for (var i = 0; i < _orders.Length; i++)
            {
                var o = _orders[i];
                if (o != null && o.ArrivesAt > PlayTime - dt + 1e-9 && o.ArrivesAt <= PlayTime + 1e-9)
                {
                    Emit(new IslandEvent(IslandEventKind.OrderArrived, value: i));
                }
            }

            NoteFish();
            CheckAchievements();
        }

        private void StepProducer(Facility f, double dt)
        {
            if (IsStructurallyStopped(f))
            {
                return;
            }

            var r = f.Recipe;
            var speed = SpeedOf(f);
            var budget = dt;

            // 1 回の刻みで作り終えたら、残りの時間で次を始める (長い刻みでも量が減らないように)。
            while (true)
            {
                if (!f.Cycling)
                {
                    if (r.Inputs.Any(i => StockOf(i.Item) < i.Count))
                    {
                        f.Status = FacilityStatus.WaitingInput;
                        return;
                    }

                    foreach (var i in r.Inputs)
                    {
                        _stock[(int)i.Item] -= i.Count;
                    }

                    f.Cycling = true;
                    f.Progress = 0;
                }

                if (f.Progress + 1e-9 < r.Seconds)
                {
                    if (budget <= 1e-9)
                    {
                        return;
                    }

                    var use = Math.Min(budget, (r.Seconds - f.Progress) / speed);
                    var gas = r.GasPerHour * use / 3600.0;
                    if (!CanAfford(Resource.Gas, gas))
                    {
                        f.Status = FacilityStatus.NoGas;
                        return;
                    }

                    if (!PayFish(f.Def.FishPerHour * use / 3600.0))
                    {
                        f.Status = FacilityStatus.NoFish;
                        return;
                    }

                    Spend(Resource.Gas, gas);
                    if (_offline != null)
                    {
                        _offline.GasUsed += gas;
                    }

                    f.Progress += use * speed;
                    budget -= use;
                    f.Status = FacilityStatus.Running;
                    if (f.Progress + 1e-9 < r.Seconds)
                    {
                        return;
                    }
                }

                if (!Complete(f, r))
                {
                    return;
                }
            }
        }

        /// <summary>1 回ぶんを出す。置き場がいっぱいなら出さずに待つ。</summary>
        private bool Complete(Facility f, Recipe r)
        {
            if (IslandCatalog.ProducesPearl(f.Kind))
            {
                _resources[(int)Resource.Pearl] += 1;
                PearlsMade++;
                if (_offline != null)
                {
                    _offline.Pearls++;
                }

                Emit(new IslandEvent(IslandEventKind.Produced, f.Id, null, 1));
            }
            else
            {
                var output = r.Output;
                if (output.Item != Item.Fish && StockOf(output.Item) + output.Count > StorageLimit)
                {
                    f.Status = FacilityStatus.StorageFull;
                    return false;
                }

                _stock[(int)output.Item] += output.Count;
                _made[(int)output.Item] += output.Count;
                _offline?.Add(output.Item, output.Count);
                Emit(new IslandEvent(IslandEventKind.Produced, f.Id, output.Item, output.Count));
            }

            f.Cycling = false;
            f.Progress = 0;
            return true;
        }

        /// <summary>材料やガスと関係なく止まっている (沈んでいる・海に面していない・人手がない)。</summary>
        private bool IsStructurallyStopped(Facility f) =>
            f.Status == FacilityStatus.Submerged || f.Status == FacilityStatus.NoCoast || f.Status == FacilityStatus.NoWorker;

        private void RefreshStatuses()
        {
            foreach (var f in _facilities)
            {
                var structural = StructuralStatus(f);
                if (structural.HasValue)
                {
                    f.Status = structural.Value;
                }
                else if (f.Def.Role != FacilityRole.Producer || IsStructurallyStopped(f))
                {
                    // 生産しない施設は効いているだけ。生産する施設はここで止まりの理由を解き、次の Step で決め直す。
                    // お魚が 1 匹も無ければごはんを払えないので、操作の直後に一瞬だけ効く (氷室の効き目が揺れる) ことのないよう先に止める。
                    f.Status = f.Def.FishPerHour > 0 && Fish < 1 ? FacilityStatus.NoFish : FacilityStatus.Running;
                }
            }
        }

        private FacilityStatus? StructuralStatus(Facility f)
        {
            if (f.Footprint().Any(c => Board.IsSunk(c, Ice)))
            {
                return FacilityStatus.Submerged;
            }

            if (f.Def.NeedsCoast && !HasCoast(f))
            {
                return FacilityStatus.NoCoast;
            }

            if (f.Workers < f.Def.Workers)
            {
                return FacilityStatus.NoWorker;
            }

            return null;
        }

        private IEnumerable<Facility> Active(FacilityRole role) =>
            _facilities.Where(f => f.Def.Role == role && !f.Footprint().Any(c => Board.IsSunk(c, Ice)));

        /// <summary>働き手が減ったら、後から建てた施設から外す。</summary>
        private void TrimWorkers()
        {
            var over = AssignedWorkers - Population;
            for (var i = _facilities.Count - 1; i >= 0 && over > 0; i--)
            {
                var f = _facilities[i];
                var take = Math.Min(f.Workers, over);
                f.Workers -= take;
                over -= take;
            }
        }

        private bool CanAfford(Resource r, double amount) => Get(r) + Get(Resource.AllMighty) + 1e-9 >= amount;

        /// <summary>
        /// 働くペンギンのごはんを払う。お魚は整数なので、端数はためておき 1 匹になったら引く。払えなければ何も引かずに false。
        /// </summary>
        private bool PayFish(double amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            var owed = _fishDebt + amount;
            var whole = (long)Math.Floor(owed + 1e-9);
            if (Fish < Math.Max(1, (long)Math.Ceiling(owed - 1e-9)))
            {
                return false;
            }

            _stock[(int)Item.Fish] -= whole;
            _fishDebt = Math.Max(0.0, owed - whole);
            if (_offline != null)
            {
                _offline.FishEaten += whole;
            }

            return true;
        }

        /// <summary>お魚が増えたら、これまでの最大を更新し、新しく開いた施設を知らせる。</summary>
        private void NoteFish()
        {
            if (Fish > MaxFishHeld)
            {
                UnlockUpTo(Fish);
            }
        }

        private void UnlockUpTo(long fish)
        {
            var before = MaxFishHeld;
            MaxFishHeld = fish;
            foreach (var def in IslandCatalog.Facilities)
            {
                if (def.UnlockFish > before && def.UnlockFish <= fish)
                {
                    Emit(new IslandEvent(IslandEventKind.Unlocked, value: (int)def.Kind));
                }
            }

            CheckAchievements();
        }

        /// <summary>島の実績を確かめ、満たした種を来させる。一度来た種は消えない。</summary>
        private void CheckAchievements()
        {
            var added = false;
            foreach (var u in PenguinUnlocks.All)
            {
                if (u.Source != UnlockSource.Island || _achievedSpecies.Contains(u.Species) || !u.IsMet(null, this))
                {
                    continue;
                }

                _achievedSpecies.Add(u.Species);
                added = true;
                if (!_sessionSpecies.Contains(u.Species))
                {
                    Emit(new IslandEvent(IslandEventKind.PenguinArrived, value: PenguinCatalog.IndexOf(u.Species)));
                }
            }

            if (added)
            {
                RebuildSpecies();
            }
        }

        private void RebuildSpecies()
        {
            _species.Clear();
            _species.AddRange(_sessionSpecies.Concat(_achievedSpecies).Distinct());
        }

        /// <summary>足りない分はオールマイティから出す。</summary>
        private void Spend(Resource r, double amount)
        {
            var own = Math.Min(Get(r), amount);
            _resources[(int)r] -= own;
            var rest = amount - own;
            if (rest > 0)
            {
                _resources[(int)Resource.AllMighty] = Math.Max(0.0, Get(Resource.AllMighty) - rest);
            }
        }

        private void AddExp(int exp)
        {
            Exp += exp;
            while (Level < _p.MaxLevel && Exp >= _p.ExpToNext(Level))
            {
                Exp -= _p.ExpToNext(Level);
                Level++;
                Emit(new IslandEvent(IslandEventKind.LevelUp, value: Level));
            }
        }

        /// <summary>今のレベルで作れる品目から、決まった順の乱数で注文を作る。</summary>
        private Order NewOrder(double arrivesAt)
        {
            var rng = new SplitMix64((ulong)(++_orderSerial * 104729L + 3));
            var pool = IslandCatalog.Items.Where(i => MaxFishHeld >= UnlockFishOf(i)).ToList();

            // 新しい品目ほど出やすくして、作ったものの出番を作る。
            var weighted = new List<Item>();
            for (var k = 0; k < pool.Count; k++)
            {
                for (var w = 0; w <= k; w++)
                {
                    weighted.Add(pool[k]);
                }
            }

            var item = weighted[rng.NextInt(weighted.Count)];
            var value = IslandCatalog.ValueOf(item);
            var budget = 10 + Level * 4 + rng.NextInt(6);
            var count = Math.Max(1, (int)Math.Round(budget / (double)value));
            var reward = (long)Math.Ceiling(value * count * _p.OrderRewardMultiplier * PenguinDex.OrderMultiplier(_species));
            var exp = (int)Math.Ceiling(value * count * 0.5) + 2;
            return new Order(item, count, reward, exp, arrivesAt);
        }

        /// <summary>その品目を作る施設が開く、お魚の数 (これまでの最大)。作る施設が無ければ long.MaxValue。</summary>
        public static long UnlockFishOf(Item item)
        {
            var steps = IslandCatalog.Facilities
                .Where(f => f.Recipes.Any(r => !IslandCatalog.ProducesPearl(f.Kind) && r.Output.Item == item))
                .Select(f => f.UnlockFish)
                .ToList();
            return steps.Count == 0 ? long.MaxValue : steps.Min();
        }

        private void Emit(IslandEvent e)
        {
            if (_recordEvents || e.Kind == IslandEventKind.LevelUp || e.Kind == IslandEventKind.HarborStageUp ||
                e.Kind == IslandEventKind.Unlocked || e.Kind == IslandEventKind.PenguinArrived)
            {
                _events.Add(e);
            }
        }
    }

    /// <summary>保存用 (JsonUtility で読み書きできる形。フィールド名を変えると古い保存が読めなくなる)。</summary>
    [Serializable]
    public sealed class IslandSave
    {
        /// <summary>2: お魚の桁の解放・お魚のごはん・島の実績 (2026-10-17)。</summary>
        public const int CurrentVersion = 2;

        public int version;
        public double[] resources;
        public long[] stock;
        public int level;
        public int exp;
        public int nextId;
        public int orderSerial;
        public int delivered;
        public double playTime;
        public long lastTickUnixMs;
        public int photoDay;
        public int photosToday;
        public int quickEcoDay;
        public string[] quickEcoIds;
        public int stepDay;
        public double stepGasToday;

        /// <summary>これまでに持ったお魚の最大。版 1 では 0 なので、読むときに持っているお魚と建てた施設から決める。</summary>
        public long maxFishHeld;

        /// <summary>まだ引いていない、ごはんのお魚の端数。</summary>
        public double fishDebt;

        public long[] made;
        public long pearlsMade;
        public int[] photoCategories;
        public string[] achievedSpecies;
        public int[] expansionsX;
        public int[] expansionsY;
        public FacilitySave[] facilities;
        public OrderSave[] orders;
    }

    [Serializable]
    public sealed class FacilitySave
    {
        public int id;
        public int kind;
        public int x;
        public int y;
        public int recipe;
        public int workers;
        public int pearls;

        /// <summary>施設のレベル。強化を入れる前の保存では 0 なので 1 として読む。</summary>
        public int level;

        public double progress;
        public bool cycling;
    }

    [Serializable]
    public sealed class OrderSave
    {
        public int item;
        public int count;
        public long reward;
        public int exp;
        public double arrivesAt;
    }
}
