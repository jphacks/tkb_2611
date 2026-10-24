using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    /// <summary>生産の島の規則 (docs/production-island/game-design.md)。</summary>
    public sealed class IslandGameTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.FromHours(9));

        // 基礎の島 (3〜6, 2〜5) の手前左の角。左と手前が海なので漁場が建つ。
        private static readonly Cell CoastCorner = new Cell(3, 2);

        // 基礎の島の真ん中。まわりが全部陸。
        private static readonly Cell Inland = new Cell(4, 3);

        private static IslandGame New(IslandParameters p = null)
        {
            var g = new IslandGame(p ?? IslandParameters.Default, Start);
            g.SyncWorld(100, 0, null);
            return g;
        }

        private static IslandGame Rich(IslandParameters p = null)
        {
            p = p ?? IslandParameters.Default;
            p.StartElectricity = 10000;
            p.StartWater = 10000;
            p.StartGas = 10000;
            p.StartFish = 100000;
            return New(p);
        }

        [Test]
        public void 最初は基礎の島だけで注文が3件ある()
        {
            var g = New();
            var land = 0;
            for (var x = 0; x < 10; x++)
            {
                for (var y = 0; y < 10; y++)
                {
                    if (g.Board.KindAt(new Cell(x, y)) == TileKind.Base)
                    {
                        land++;
                    }
                }
            }

            Assert.That(land, Is.EqualTo(16));
            Assert.That(g.Board.KindAt(new Cell(0, 9)), Is.EqualTo(TileKind.Canal));
            Assert.That(g.Orders.Count, Is.EqualTo(3));
            Assert.That(Enumerable.Range(0, 3).All(g.IsOrderReady), Is.True);
            Assert.That(g.Level, Is.EqualTo(1));
        }

        [Test]
        public void 漁場は海に面していないと動かない()
        {
            var g = Rich();
            Assert.That(g.Build(FacilityKind.Fishery, Inland), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.Facilities[0].Status, Is.EqualTo(FacilityStatus.NoCoast));

            g.Move(g.Facilities[0].Id, CoastCorner);
            g.Simulate(1);
            Assert.That(g.Facilities[0].Status, Is.EqualTo(FacilityStatus.Running));
        }

        [Test]
        public void 漁場は4秒ごとにお魚を1匹とってガスを使う()
        {
            var g = New();
            Assert.That(g.Build(FacilityKind.Fishery, CoastCorner), Is.EqualTo(IslandActionResult.Ok));
            var fish = g.Fish;
            var gas = g.Get(Resource.Gas);

            g.Simulate(4.0);

            Assert.That(g.Fish - fish, Is.EqualTo(1));
            Assert.That(gas - g.Get(Resource.Gas), Is.EqualTo(0.25 * 4 / 3600).Within(1e-9));
        }

        [Test]
        public void 長い留守も短い刻みと同じだけ作る()
        {
            var a = New();
            a.Build(FacilityKind.Fishery, CoastCorner);
            var b = New();
            b.Build(FacilityKind.Fishery, CoastCorner);

            for (var i = 0; i < 3600; i++)
            {
                a.Simulate(1.0);
            }

            b.Simulate(3600.0);

            Assert.That(b.Fish, Is.EqualTo(a.Fish));
            Assert.That(b.PendingOfflineReport, Is.Not.Null);
            Assert.That(b.PendingOfflineReport[Item.Fish], Is.EqualTo(900));
        }

        [Test]
        public void 閉じていた時間は12時間までしか数えない()
        {
            var g = New();
            g.Build(FacilityKind.Fishery, CoastCorner);
            var fish = g.Fish;

            g.Tick(Start.AddDays(3));

            Assert.That(g.Fish - fish, Is.EqualTo(12 * 3600 / 4));
        }

        [Test]
        public void 時計が戻っても進まない()
        {
            var g = New();
            g.Build(FacilityKind.Fishery, CoastCorner);
            g.Tick(Start.AddMinutes(1));
            var fish = g.Fish;

            g.Tick(Start);

            Assert.That(g.Fish, Is.EqualTo(fish));
        }

        [Test]
        public void さばき場は魚3匹ですり身を作り_お魚が足りなければ建たない()
        {
            var g = Rich();
            g.SyncWorld(100, 10, null);
            g.DevSetLevel(2);
            Assert.That(g.Build(FacilityKind.Preparation, Inland), Is.EqualTo(IslandActionResult.Ok));
            var fish = g.Fish;

            g.Simulate(6.0);

            Assert.That(g.StockOf(Item.Surimi), Is.EqualTo(1));
            Assert.That(fish - g.Fish, Is.EqualTo(6), "1 回目と、すぐ始めた 2 回目の材料");

            var p = IslandParameters.Default;
            p.StartFish = 0;
            p.StartElectricity = 1000;
            var poor = new IslandGame(p, Start);
            poor.DevSetMaxFish(100);
            Assert.That(poor.Build(FacilityKind.Preparation, Inland), Is.EqualTo(IslandActionResult.NotEnoughFish));
        }

        [Test]
        public void 置き場がいっぱいなら止まり_倉庫で増える()
        {
            var p = IslandParameters.Default;
            p.BaseStorage = 2;
            var g = Rich(p);
            g.SyncWorld(100, 10, null);
            g.DevSetLevel(2);
            g.Build(FacilityKind.Preparation, Inland);

            g.Simulate(60);

            Assert.That(g.StockOf(Item.Surimi), Is.EqualTo(2));
            Assert.That(g.Facilities[0].Status, Is.EqualTo(FacilityStatus.StorageFull));

            g.Build(FacilityKind.Warehouse, new Cell(6, 5));
            g.Simulate(6);
            Assert.That(g.StockOf(Item.Surimi), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void 氷が支えられる土地は氷40で0_100で24()
        {
            var g = New();
            Assert.That(g.Board.Capacity(40), Is.EqualTo(0));
            Assert.That(g.Board.Capacity(100), Is.EqualTo(24));
            Assert.That(g.Board.Capacity(150), Is.EqualTo(44));
            Assert.That(g.Board.Capacity(42.4), Is.EqualTo(0));
            Assert.That(g.Board.Capacity(42.5), Is.EqualTo(1));
        }

        [Test]
        public void 開拓は陸に接する海だけで水とお魚を使う()
        {
            var g = Rich();
            var water = g.Get(Resource.Water);
            var fish = g.Fish;

            Assert.That(g.Expand(new Cell(0, 0)), Is.EqualTo(IslandActionResult.CannotPlace));
            Assert.That(g.Expand(new Cell(2, 2)), Is.EqualTo(IslandActionResult.Ok));

            Assert.That(water - g.Get(Resource.Water), Is.EqualTo(5));
            Assert.That(fish - g.Fish, Is.EqualTo(10));
            Assert.That(g.Board.KindAt(new Cell(2, 2)), Is.EqualTo(TileKind.Expanded));
            Assert.That(g.ExpandWaterCost, Is.EqualTo(Math.Ceiling(5 * 1.15)));
            Assert.That(g.Expand(new Cell(1, 2)), Is.EqualTo(IslandActionResult.Ok), "開拓した陸の隣も広げられる");
            Assert.That(g.Expand(new Cell(5, 8)), Is.EqualTo(IslandActionResult.CannotPlace), "水路は広げられない");
        }

        [Test]
        public void レベル1で開拓できるのは4マスまで()
        {
            var g = Rich();
            var cells = new[] { new Cell(2, 2), new Cell(2, 3), new Cell(2, 4), new Cell(2, 5), new Cell(2, 6) };
            var results = cells.Select(g.Expand).ToList();

            // 開拓の経験値でレベルが上がることがあるので、上がっていなければ 5 マス目は断られる。
            if (g.Level == 1)
            {
                Assert.That(results[4], Is.EqualTo(IslandActionResult.LimitReached));
            }

            Assert.That(results.Take(4).All(r => r == IslandActionResult.Ok), Is.True);
        }

        [Test]
        public void 氷が足りないと後から開拓したマスから沈み_施設は止まり_戻れば浮かぶ()
        {
            var g = Rich();
            g.SyncWorld(100, 10, null);
            g.Expand(new Cell(2, 2));
            g.Expand(new Cell(2, 3));
            g.Expand(new Cell(1, 2));
            g.Expand(new Cell(1, 3));
            g.DrainEvents();

            // 1×1 の倉庫を最後に広げたマスに建てる。
            Assert.That(g.Build(FacilityKind.Warehouse, new Cell(1, 3)), Is.EqualTo(IslandActionResult.Ok));

            g.SyncWorld(40 + 2.5 * 2, 10, null); // 2 マスしか支えられない

            Assert.That(g.Board.IsSunk(new Cell(2, 2), g.Ice), Is.False);
            Assert.That(g.Board.IsSunk(new Cell(2, 3), g.Ice), Is.False);
            Assert.That(g.Board.IsSunk(new Cell(1, 2), g.Ice), Is.True);
            Assert.That(g.Board.IsSunk(new Cell(1, 3), g.Ice), Is.True);
            Assert.That(g.Facilities.Last().Status, Is.EqualTo(FacilityStatus.Submerged));
            Assert.That(g.StorageLimit, Is.EqualTo(g.Parameters.BaseStorage), "沈んだ倉庫は効かない");
            Assert.That(g.DrainEvents().Count(e => e.Kind == IslandEventKind.Sunk), Is.EqualTo(2));
            Assert.That(g.Expand(new Cell(3, 1)), Is.EqualTo(IslandActionResult.NotEnoughIce));

            g.SyncWorld(100, 10, null);

            Assert.That(g.Board.SunkCount(g.Ice), Is.EqualTo(0));
            Assert.That(g.Facilities.Last().Status, Is.EqualTo(FacilityStatus.Running));
            Assert.That(g.DrainEvents().Count(e => e.Kind == IslandEventKind.Surfaced), Is.EqualTo(2));
        }

        [Test]
        public void 次に沈むマスをひびで予告する()
        {
            var g = Rich();
            var cells = new[] { new Cell(2, 2), new Cell(2, 3), new Cell(2, 4), new Cell(2, 5) };
            foreach (var c in cells)
            {
                g.Expand(c);
            }

            g.SyncWorld(40 + 2.5 * 4, 0, null); // 4 マスちょうど

            Assert.That(cells.Select(c => g.Board.IsWarned(c, g.Ice)), Is.EqualTo(new[] { false, true, true, true }));
        }

        [Test]
        public void 基礎の島は氷が下限でも沈まない()
        {
            var g = Rich();
            g.Build(FacilityKind.Fishery, CoastCorner);
            g.SyncWorld(40, 0, null);
            g.Simulate(4);
            Assert.That(g.Facilities[0].Status, Is.EqualTo(FacilityStatus.Running));
        }

        [Test]
        public void 写真の資源は1日1枚で_判定したカテゴリに多く入る()
        {
            var g = New();
            var e = g.Get(Resource.Electricity);
            var w = g.Get(Resource.Water);
            var verdict = new EcoPhotoVerdict(EcoCategory.SaveElectricity, "消灯", "", 0.9);

            var got = g.ReceivePhoto(verdict, 10);

            Assert.That(got, Is.Not.Null);
            Assert.That(g.Get(Resource.Electricity) - e, Is.EqualTo(20));
            Assert.That(g.Get(Resource.Water) - w, Is.EqualTo(5));
            Assert.That(g.ReceivePhoto(verdict, 10), Is.Null);
            Assert.That(g.CanReceivePhoto(11), Is.True);
            Assert.That(g.ReceivePhoto(verdict, 11), Is.Not.Null);
        }

        [Test]
        public void 判定できない写真でも少しもらえる()
        {
            var g = New();
            var got = g.ReceivePhoto(new EcoPhotoVerdict(EcoCategory.Unknown, "", "", 0.1), 1);
            Assert.That(got[Resource.AllMighty], Is.EqualTo(0));
            Assert.That(got[Resource.Electricity], Is.EqualTo(3));
            Assert.That(got[Resource.Water], Is.EqualTo(3));
            Assert.That(got[Resource.Gas], Is.EqualTo(3));
        }

        [Test]
        public void 写真の表のものは表の量だけもらえる()
        {
            var g = New();
            var bottle = EcoSubjects.Find(EcoSubject.MyBottle);
            var got = g.ReceivePhoto(new EcoPhotoVerdict(EcoSubject.MyBottle, "", 0.9), 1);

            Assert.That(got[Resource.Water], Is.EqualTo(bottle.Bundle()[Resource.Water]));
            Assert.That(got[Resource.AllMighty], Is.EqualTo(bottle.Bundle()[Resource.AllMighty]));
            Assert.That(got[Resource.Electricity], Is.EqualTo(0), "表に無い資源は入らない");
        }

        [Test]
        public void 写真の表は名前とカテゴリを決め_IDで引ける()
        {
            var verdict = new EcoPhotoVerdict(EcoSubject.EcoBag, "えらい", 0.9);
            Assert.That(verdict.Label, Is.EqualTo("エコバッグ"));
            Assert.That(verdict.Category, Is.EqualTo(EcoCategory.Goods));
            Assert.That(verdict.IsEco, Is.True);
            Assert.That(EcoSubjects.FindById("eco_bag").Subject, Is.EqualTo(EcoSubject.EcoBag));
            Assert.That(EcoSubjects.FindById("unknown_thing"), Is.Null);
        }

        [Test]
        public void 写真の表はIDが重ならず_どれも何かがもらえる()
        {
            Assert.That(EcoSubjects.All.Select(d => d.Id).Distinct().Count(), Is.EqualTo(EcoSubjects.All.Count));
            Assert.That(EcoSubjects.All.Select(d => d.Subject).Distinct().Count(), Is.EqualTo(EcoSubjects.All.Count));
            foreach (var d in EcoSubjects.All)
            {
                Assert.That(d.Bundle().IsEmpty, Is.False, d.Name);
                Assert.That(d.Category, Is.Not.EqualTo(EcoCategory.Unknown), d.Name);
            }

            var all = Enum.GetValues(typeof(EcoSubject)).Cast<EcoSubject>().Where(x => x != EcoSubject.None);
            Assert.That(all.All(x => EcoSubjects.Find(x) != null), Is.True, "enum に足したら表にも足す");
        }

        [Test]
        public void 増幅所とペンギンで受け取る量が増える()
        {
            var g = Rich();
            g.DevSetLevel(3);
            Assert.That(g.Build(FacilityKind.ElectricAmplifier, Inland), Is.EqualTo(IslandActionResult.Ok));
            g.SyncWorld(100, 0, new[] { PenguinCatalog.Total14 });

            Assert.That(g.GainMultiplier(Resource.Electricity), Is.EqualTo(1.25 * 1.1).Within(1e-9));
            Assert.That(g.GainMultiplier(Resource.Water), Is.EqualTo(PenguinDex.ResourceMultiplier(g.Species, Resource.Water)).Within(1e-9),
                "水は増幅所なし (お魚 1,000 匹の実績で来たケープペンギンのぶんだけ)");
        }

        [Test]
        public void 歩数のガスは500歩で1_1日20まで()
        {
            var g = New();
            var gas = g.Get(Resource.Gas);

            Assert.That(g.ReceiveSteps(2600, 5), Is.EqualTo(5));
            Assert.That(g.ReceiveSteps(3000, 5), Is.EqualTo(1));
            Assert.That(g.ReceiveSteps(3000, 5), Is.EqualTo(0));
            Assert.That(g.ReceiveSteps(50000, 5), Is.EqualTo(14));
            Assert.That(g.ReceiveSteps(1000, 6), Is.EqualTo(2), "日が変われば数え直す");
            Assert.That(g.Get(Resource.Gas) - gas, Is.EqualTo(22));
        }

        [Test]
        public void 振り返りはできなかったでも少しもらえる()
        {
            var g = New();
            Assert.That(g.ReceiveCheckIn(CheckInAnswer.Done)[Resource.AllMighty], Is.EqualTo(5));
            Assert.That(g.ReceiveCheckIn(CheckInAnswer.Failed)[Resource.AllMighty], Is.EqualTo(2));
        }

        [Test]
        public void 電気が足りなければオールマイティで払う()
        {
            var p = IslandParameters.Default;
            p.StartElectricity = 4;
            p.StartAllMighty = 10;
            var g = New(p);

            Assert.That(g.Build(FacilityKind.Fishery, CoastCorner), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.Get(Resource.Electricity), Is.EqualTo(0));
            Assert.That(g.Get(Resource.AllMighty), Is.EqualTo(4));
            Assert.That(g.Build(FacilityKind.Fishery, new Cell(5, 2)), Is.EqualTo(IslandActionResult.NotEnoughElectricity));
        }

        [Test]
        public void ガスもオールマイティも尽きたら止まる()
        {
            var p = IslandParameters.Default;
            p.StartGas = 0.001;
            var g = New(p);
            g.Build(FacilityKind.Fishery, CoastCorner);

            g.Simulate(60);

            Assert.That(g.Facilities[0].Status, Is.EqualTo(FacilityStatus.NoGas));
            var fish = g.Fish;
            g.Simulate(60);
            Assert.That(g.Fish, Is.EqualTo(fish));

            g.Grant(ResourceBundle.Of(Resource.Gas, 1));
            g.Simulate(4);
            Assert.That(g.Fish, Is.GreaterThan(fish));
        }

        [Test]
        public void 働き手は記録日で増え_宿舎の定員まで()
        {
            var g = Rich();
            Assert.That(g.Population, Is.EqualTo(1), "看板の 1 羽から始まる");
            g.SyncWorld(100, 1, null);
            Assert.That(g.Population, Is.EqualTo(2), "最初の記録で 2 羽 (氷室と漁場を動かせる)");
            g.SyncWorld(100, 10, null);
            Assert.That(g.Population, Is.EqualTo(4), "宿舎が無いと 4 羽まで");
            g.Build(FacilityKind.Dormitory, Inland);
            Assert.That(g.Population, Is.EqualTo(8));
        }

        [Test]
        public void 働き手がいなければ止まり_配属すると動く()
        {
            var g = Rich();
            g.SyncWorld(100, 2, null); // 1 + 記録 2 日 = 3 羽
            g.Build(FacilityKind.Fishery, CoastCorner);
            g.Build(FacilityKind.Fishery, new Cell(5, 2));
            g.Build(FacilityKind.Fishery, new Cell(3, 4));
            g.Build(FacilityKind.Fishery, new Cell(5, 4));

            Assert.That(g.Facilities.Count(f => f.Status == FacilityStatus.NoWorker), Is.EqualTo(1));
            Assert.That(g.SetWorkers(g.Facilities[3].Id, 1), Is.EqualTo(IslandActionResult.NoFreeWorker));

            g.SetWorkers(g.Facilities[0].Id, 0);
            Assert.That(g.SetWorkers(g.Facilities[3].Id, 1), Is.EqualTo(IslandActionResult.Ok));
            g.Simulate(1);
            Assert.That(g.Facilities[3].Status, Is.EqualTo(FacilityStatus.Running));
            Assert.That(g.Facilities[0].Status, Is.EqualTo(FacilityStatus.NoWorker));
        }

        [Test]
        public void 納品でお魚と経験値が入り_次の舟は少し後に来る()
        {
            var g = Rich();
            const int slot = 0;
            var order = g.Orders[slot];
            g.DevAddStock(order.Item, order.Count);
            var fish = g.Fish;

            Assert.That(g.Deliver(slot), Is.EqualTo(IslandActionResult.Ok));

            var expectedFish = order.Item == Item.Fish ? order.Reward - order.Count : order.Reward;
            Assert.That(g.Fish - fish, Is.EqualTo(expectedFish));
            Assert.That(g.Delivered, Is.EqualTo(1));
            Assert.That(g.HarborStage, Is.EqualTo(1));
            Assert.That(g.IsOrderReady(slot), Is.False);
            Assert.That(g.Deliver(slot), Is.EqualTo(IslandActionResult.NotArrived));
            g.Simulate(45);
            Assert.That(g.IsOrderReady(slot), Is.True);
        }

        [Test]
        public void 品目が足りなければ納品できない()
        {
            var p = IslandParameters.Default;
            p.StartFish = 0;
            var g = New(p);
            Assert.That(g.Deliver(0), Is.EqualTo(IslandActionResult.NotEnoughItems));
        }

        [Test]
        public void 施設はお魚の桁で開き_お魚を使っても閉じない()
        {
            var g = New(); // お魚 30
            Assert.That(g.CanBuild(FacilityKind.IceHouse), Is.EqualTo(IslandActionResult.Ok), "氷室は最初から");
            Assert.That(g.CanBuild(FacilityKind.Fishery), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.CanBuild(FacilityKind.Dormitory), Is.EqualTo(IslandActionResult.Locked));
            Assert.That(g.NextUnlockFish, Is.EqualTo(100));

            g.DevAddStock(Item.Fish, 70); // 100 匹
            var events = g.DrainEvents();
            Assert.That(events.Where(e => e.Kind == IslandEventKind.Unlocked).Select(e => (FacilityKind)e.Value),
                Is.EquivalentTo(new[] { FacilityKind.Dormitory, FacilityKind.Preparation }));
            Assert.That(g.IsUnlocked(FacilityKind.Dormitory), Is.True);

            g.DevAddStock(Item.Fish, -100);
            Assert.That(g.Fish, Is.EqualTo(0));
            Assert.That(g.IsUnlocked(FacilityKind.Dormitory), Is.True, "これまでの最大で数える");
            Assert.That(g.IsUnlocked(FacilityKind.Saltworks), Is.False);
            Assert.That(g.DrainEvents().Any(e => e.Kind == IslandEventKind.Unlocked), Is.False, "一度開いた施設は二度知らせない");
        }

        [Test]
        public void 港の注文は開いた施設が作れる品目だけ()
        {
            var g = New();
            Assert.That(g.Orders.All(o => o.Item == Item.Fish), Is.True, "最初はお魚だけ");
        }

        [Test]
        public void 研究所は真珠を作り_真珠で施設が速くなる()
        {
            var g = Rich();
            g.SyncWorld(100, 10, null);
            g.DevSetLevel(6);
            g.DevAddStock(Item.Kelp, 20);
            g.Build(FacilityKind.Research, new Cell(5, 4));
            g.Build(FacilityKind.Fishery, CoastCorner);

            g.Simulate(60);

            Assert.That(g.Get(Resource.Pearl), Is.EqualTo(1));
            var fishery = g.Facilities.First(f => f.Kind == FacilityKind.Fishery);
            var before = g.SpeedOf(fishery);
            Assert.That(g.AddPearl(fishery.Id), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.SpeedOf(fishery), Is.EqualTo(before * 1.1).Within(1e-9));
        }

        [Test]
        public void 質の良いマスに建てると速い()
        {
            var g = Rich();
            g.DevSetLevel(10);

            // (1,3) は豊漁のマス。基礎の島から左へ 2 マス広げて 2×2 を置く。
            foreach (var c in new[] { new Cell(2, 3), new Cell(2, 2), new Cell(1, 3), new Cell(1, 2) })
            {
                Assert.That(g.Expand(c), Is.EqualTo(IslandActionResult.Ok), c.ToString());
            }

            g.Build(FacilityKind.Fishery, new Cell(1, 2));
            Assert.That(g.SpeedOf(g.Facilities[0]), Is.EqualTo(1.5).Within(1e-9));
        }

        [Test]
        public void 生産量タブは毎時の作る量と使う量を出す()
        {
            var g = Rich();
            g.SyncWorld(100, 10, null);
            g.DevSetLevel(2);
            g.Build(FacilityKind.Fishery, CoastCorner);
            g.Build(FacilityKind.Preparation, new Cell(5, 4));

            var fish = g.Rates().First(r => r.Item == Item.Fish);
            Assert.That(fish.ProducedPerHour, Is.EqualTo(900).Within(1e-6));
            Assert.That(fish.ConsumedPerHour, Is.EqualTo(1800 + IslandCatalog.Get(FacilityKind.Preparation).FishPerHour).Within(1e-6),
                "材料と、働くペンギンのごはん");
            Assert.That(fish.NetPerHour, Is.LessThan(0));
        }

        [Test]
        public void 保存して読み込むと同じ状態に戻る()
        {
            var g = Rich();
            g.SyncWorld(100, 10, null);
            g.DevSetLevel(5);
            g.Expand(new Cell(2, 2));
            g.Build(FacilityKind.Fishery, CoastCorner);
            g.Build(FacilityKind.Chikuwa, new Cell(5, 4));
            g.SetRecipe(g.Facilities[1].Id, 1);
            g.Simulate(37.5);
            g.Tick(Start.AddSeconds(10));

            var save = g.ToSave();
            var json = UnityEngine.JsonUtility.ToJson(save);
            var loaded = IslandGame.FromSave(UnityEngine.JsonUtility.FromJson<IslandSave>(json), g.Parameters, Start);
            loaded.SyncWorld(100, 10, null);

            Assert.That(loaded.Fish, Is.EqualTo(g.Fish));
            Assert.That(loaded.Level, Is.EqualTo(g.Level));
            Assert.That(loaded.Exp, Is.EqualTo(g.Exp));
            Assert.That(loaded.Board.Expansions, Is.EqualTo(g.Board.Expansions));
            Assert.That(loaded.Facilities.Select(f => (f.Kind, f.Origin, f.RecipeIndex, f.Workers, f.Cycling)),
                Is.EqualTo(g.Facilities.Select(f => (f.Kind, f.Origin, f.RecipeIndex, f.Workers, f.Cycling))));
            for (var i = 0; i < g.Facilities.Count; i++)
            {
                Assert.That(loaded.Facilities[i].Progress, Is.EqualTo(g.Facilities[i].Progress).Within(1e-6));
            }

            Assert.That(loaded.Orders.Select(o => (o.Item, o.Count, o.Reward)), Is.EqualTo(g.Orders.Select(o => (o.Item, o.Count, o.Reward))));
            Assert.That(loaded.LastTickUtc, Is.EqualTo(g.LastTickUtc));
            Assert.That(loaded.Get(Resource.Gas), Is.EqualTo(g.Get(Resource.Gas)).Within(1e-9));

            loaded.Simulate(100);
            g.Simulate(100);
            Assert.That(loaded.Fish, Is.EqualTo(g.Fish));
        }

        [Test]
        public void 図鑑は19種そろっていてボーナスがある()
        {
            var all = PenguinCatalog.MilestoneOrder.Concat(PenguinCatalog.WeeklyOrder).ToList();
            Assert.That(all.Count, Is.EqualTo(19));
            foreach (var s in all)
            {
                var e = PenguinDex.Find(s);
                Assert.That(e, Is.Not.Null, s);
                Assert.That(e.BonusText, Is.Not.Empty, s);
                Assert.That(e.Multiplier, Is.GreaterThan(1.0), s);
            }

            Assert.That(PenguinDex.ResourceMultiplier(new[] { PenguinCatalog.Total30 }, Resource.Gas), Is.EqualTo(1.2).Within(1e-9));
        }

        [Test]
        public void 今日のミッションは日付で決まる3件()
        {
            var a = MissionCatalog.DailyFor(100);
            var b = MissionCatalog.DailyFor(100);
            Assert.That(a.Select(m => m.Id), Is.EqualTo(b.Select(m => m.Id)));
            Assert.That(a.Select(m => m.Id).Distinct().Count(), Is.EqualTo(3));
            var days = Enumerable.Range(0, 30).Select(d => string.Join(",", MissionCatalog.DailyFor(d).Select(m => m.Id))).Distinct();
            Assert.That(days.Count(), Is.GreaterThan(20), "日によって変わる");
        }

        [Test]
        public void 施設の定義はレシピと素材の品目がそろっている()
        {
            foreach (var f in IslandCatalog.Facilities)
            {
                Assert.That(f.Size, Is.EqualTo(1).Or.EqualTo(2), f.Name);
                if (f.Role == FacilityRole.Producer)
                {
                    Assert.That(f.Recipes.Count, Is.GreaterThan(0), f.Name);
                    Assert.That(f.Workers, Is.GreaterThan(0), f.Name);
                }
            }

            foreach (var item in IslandCatalog.Items)
            {
                Assert.That(IslandGame.UnlockFishOf(item), Is.LessThan(long.MaxValue), item.ToString());
            }
        }

        /// <summary>
        /// 8 週の通し: <see cref="GameSession"/> の氷で島を動かす。開拓して 30 日休むと縁が沈み、戻って記録を続けると浮かぶ。
        /// </summary>
        [Test]
        public void 休むと縁から沈み_戻って記録すると浮かぶ()
        {
            var clock = new ManualClock(Start);
            var session = new GameSession(clock, (w, rs) => 0.0);
            var g = Rich();
            var maxSunk = 0;
            var sunkLastRestDay = -1;
            var sunkOnReturn = -1;
            for (var day = 0; day < 56; day++)
            {
                clock.Set(Start.AddDays(day));
                session.Advance();
                var resting = day >= 14 && day < 44;
                if (!resting)
                {
                    session.ScanReceipt(new ReceiptRecord($"photo-{day}", session.Today));
                }

                g.SyncWorld(session.Ice, session.TotalRecordDays, session.Arrivals.Select(a => a.Species));
                if (day == 0)
                {
                    // 氷が支えられるだけ開拓する (24 マス)。
                    g.DevSetLevel(10);
                    while (g.ExpandRoom > 0 && g.Expand(g.Board.ExpandableCells(g.Ice).First()) == IslandActionResult.Ok)
                    {
                    }
                }

                var sunk = g.Board.SunkCount(g.Ice);
                maxSunk = Math.Max(maxSunk, sunk);
                if (day == 43)
                {
                    sunkLastRestDay = sunk;
                }

                if (day == 44)
                {
                    sunkOnReturn = sunk;
                }
            }

            Assert.That(g.Board.Expansions.Count, Is.EqualTo(g.Board.Capacity(102)));
            Assert.That(maxSunk, Is.GreaterThan(0), "30 日休むと沈む");
            Assert.That(sunkLastRestDay, Is.GreaterThan(0), "休みの最後の日も沈んだまま");
            Assert.That(sunkOnReturn, Is.LessThanOrEqualTo(sunkLastRestDay), "戻った日の記録で氷が戻りはじめる (沈みは増えない)");
            Assert.That(g.Board.SunkCount(g.Ice), Is.EqualTo(0), "戻って 12 日記録すれば浮かぶ");
        }

        // ------------------------------------------------------------------
        // 施設の強化
        // ------------------------------------------------------------------

        [Test]
        public void 施設は島のレベルより上には強化できない()
        {
            var g = Rich();
            g.Build(FacilityKind.Warehouse, Inland);
            var id = g.Facilities.Single().Id;

            Assert.That(g.CanUpgrade(id), Is.EqualTo(IslandActionResult.Locked), "島が Lv1 なら施設も Lv1 まで");
            g.DevSetLevel(2);
            Assert.That(g.Upgrade(id), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.Find(id).Level, Is.EqualTo(2));
            Assert.That(g.CanUpgrade(id), Is.EqualTo(IslandActionResult.Locked), "島が Lv2 なら施設も Lv2 まで");
        }

        [Test]
        public void 強化は電気とお魚を使い上限で止まる()
        {
            var g = Rich();
            g.DevSetLevel(10);
            g.Build(FacilityKind.Warehouse, Inland);
            var f = g.Facilities.Single();
            var def = f.Def;
            var p = g.Parameters;

            var electricity = g.Get(Resource.Electricity);
            var fish = g.Fish;
            Assert.That(g.Upgrade(f.Id), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.Get(Resource.Electricity), Is.EqualTo(electricity - p.UpgradeElectricityCost(def, 1)));
            Assert.That(g.Fish, Is.EqualTo(fish - (long)p.UpgradeFishCost(def, 1)));

            while (g.CanUpgrade(f.Id) == IslandActionResult.Ok)
            {
                g.Upgrade(f.Id);
            }

            Assert.That(f.Level, Is.EqualTo(p.FacilityMaxLevel));
            Assert.That(g.CanUpgrade(f.Id), Is.EqualTo(IslandActionResult.MaxLevel));
        }

        [Test]
        public void 強化にお魚が足りなければ上げられない()
        {
            var p = IslandParameters.Default;
            p.StartElectricity = 10000;
            p.StartFish = 30;
            var g = New(p);
            g.DevSetLevel(3);
            g.Build(FacilityKind.Warehouse, Inland);
            var id = g.Facilities.Single().Id;

            Assert.That(g.CanUpgrade(id), Is.EqualTo(IslandActionResult.NotEnoughFish));
            Assert.That(g.Upgrade(id), Is.EqualTo(IslandActionResult.NotEnoughFish));
            Assert.That(g.Find(id).Level, Is.EqualTo(1));
        }

        [Test]
        public void 強化すると作る速さ置き場定員増幅が上がる()
        {
            var g = Rich();
            g.DevSetLevel(10);
            var p = g.Parameters;
            Assert.That(g.Build(FacilityKind.Fishery, CoastCorner), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.Build(FacilityKind.Warehouse, new Cell(6, 5)), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.Build(FacilityKind.Dormitory, new Cell(5, 3)), Is.EqualTo(IslandActionResult.Ok));
            var fishery = g.Facilities.First(f => f.Kind == FacilityKind.Fishery);
            var warehouse = g.Facilities.First(f => f.Kind == FacilityKind.Warehouse);
            var dorm = g.Facilities.First(f => f.Kind == FacilityKind.Dormitory);

            var speed = g.SpeedOf(fishery);
            var storage = g.StorageLimit;
            var housing = g.Housing;
            g.Upgrade(fishery.Id);
            g.Upgrade(warehouse.Id);
            g.Upgrade(dorm.Id);

            Assert.That(g.SpeedOf(fishery), Is.EqualTo(speed * (1 + p.UpgradeSpeedPerLevel)).Within(1e-9));
            Assert.That(g.StorageLimit, Is.EqualTo(storage + p.StoragePerWarehouse));
            Assert.That(g.Housing, Is.EqualTo(housing + p.UpgradeHousingPerLevel));
        }

        [Test]
        public void 施設のレベルは保存して戻せる()
        {
            var g = Rich();
            g.DevSetLevel(5);
            g.Build(FacilityKind.Warehouse, Inland);
            var id = g.Facilities.Single().Id;
            g.Upgrade(id);
            g.Upgrade(id);

            var loaded = IslandGame.FromSave(g.ToSave(), g.Parameters, Start);
            Assert.That(loaded.Find(id).Level, Is.EqualTo(3));
        }

        [Test]
        public void 強化を入れる前の保存はレベル1として読む()
        {
            var g = Rich();
            g.Build(FacilityKind.Warehouse, Inland);
            var save = g.ToSave();
            foreach (var f in save.facilities)
            {
                f.level = 0;
            }

            var loaded = IslandGame.FromSave(save, g.Parameters, Start);
            Assert.That(loaded.Facilities.Single().Level, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------
        // 2026-10-17 のコア: お魚のごはん・氷室・やる気・実績・日付の逆行
        // ------------------------------------------------------------------

        [Test]
        public void 働くペンギンはお魚を食べ_尽きると止まり_漁場は食べない()
        {
            var p = IslandParameters.Default;
            p.StartFish = 3;
            var g = New(p);
            g.SyncWorld(100, 10, null);
            g.DevSetMaxFish(100);
            g.DevAddStock(Item.Fish, 100);
            Assert.That(g.Build(FacilityKind.Dormitory, new Cell(6, 5)), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(g.Build(FacilityKind.IceHouse, new Cell(5, 5)), Is.EqualTo(IslandActionResult.Ok));
            var ice = g.Facilities.Single(f => f.Kind == FacilityKind.IceHouse);
            Assert.That(ice.Status, Is.EqualTo(FacilityStatus.Running));
            var perHour = ice.Def.FishPerHour;
            var fish = g.Fish;

            g.Simulate(3600);

            Assert.That(fish - g.Fish, Is.EqualTo((long)perHour), "1 時間でごはん 1 時間ぶん");
            Assert.That(g.PendingOfflineReport.FishEaten, Is.EqualTo((long)perHour));

            g.DevAddStock(Item.Fish, -g.Fish);
            g.Simulate(10);
            Assert.That(ice.Status, Is.EqualTo(FacilityStatus.NoFish));
            Assert.That(g.MeltReduction, Is.EqualTo(0), "ごはんが無い氷室は効かない");

            // 漁場はとった魚を食べるので、お魚 0 からでも動いて立ち直れる。
            Assert.That(g.Build(FacilityKind.Fishery, CoastCorner), Is.EqualTo(IslandActionResult.Ok));
            g.Simulate(60);
            Assert.That(g.Facilities.Single(f => f.Kind == FacilityKind.Fishery).Status, Is.EqualTo(FacilityStatus.Running));
            Assert.That(ice.Status, Is.EqualTo(FacilityStatus.Running), "お魚がとれたら氷室も動き出す");
        }

        [Test]
        public void 氷室は水と電気で建ち_溶ける氷を減らし_強化で増える()
        {
            var g = New();
            var water = g.Get(Resource.Water);
            Assert.That(g.MeltReduction, Is.EqualTo(0));
            Assert.That(g.Build(FacilityKind.IceHouse, Inland), Is.EqualTo(IslandActionResult.Ok));
            Assert.That(water - g.Get(Resource.Water), Is.EqualTo(IslandCatalog.Get(FacilityKind.IceHouse).WaterCost));
            Assert.That(g.MeltReduction, Is.EqualTo(0.3).Within(1e-9));
            Assert.That(g.CanBuild(FacilityKind.IceHouse), Is.EqualTo(IslandActionResult.LimitReached), "1 棟まで");

            var rich = Rich();
            rich.SyncWorld(100, 10, null);
            rich.DevSetLevel(10);
            rich.Build(FacilityKind.IceHouse, Inland);
            var id = rich.Facilities.Single().Id;
            while (rich.CanUpgrade(id) == IslandActionResult.Ok)
            {
                rich.Upgrade(id);
            }

            Assert.That(rich.MeltReduction, Is.EqualTo(0.7).Within(1e-9));
            rich.SyncWorld(150, 10, null, 4);
            Assert.That(rich.MeltReduction, Is.EqualTo(0.8).Within(1e-9), "やる気を掛けても上限まで");
        }

        [Test]
        public void 氷が大きいとやる気が上がり_施設がよく働き_小さくても下がらない()
        {
            var g = Rich();
            g.Build(FacilityKind.Fishery, CoastCorner);
            var f = g.Facilities.Single();
            var baseSpeed = g.SpeedOf(f);

            g.SyncWorld(40, 0, null, 0);
            Assert.That(g.Motivation, Is.EqualTo(1.0));
            Assert.That(g.SpeedOf(f), Is.EqualTo(baseSpeed).Within(1e-9));

            g.SyncWorld(110, 0, null, 3);
            Assert.That(g.SpeedOf(f), Is.EqualTo(baseSpeed * 1.15).Within(1e-9));

            g.SyncWorld(140, 0, null, 4);
            Assert.That(g.Motivation, Is.EqualTo(1.3));
        }

        [Test]
        public void 島の実績を満たすと新しい種が来て_ボーナスが効き_保存で残る()
        {
            var g = Rich();
            g.SyncWorld(100, 10, new[] { PenguinCatalog.Onboarding });
            g.DrainEvents();
            Assert.That(g.Species, Does.Not.Contain("gentoo"));

            for (var i = 0; i < 10; i++)
            {
                var slot = Enumerable.Range(0, g.Orders.Count).First(g.IsOrderReady);
                g.DevAddStock(g.Orders[slot].Item, g.Orders[slot].Count);
                Assert.That(g.Deliver(slot), Is.EqualTo(IslandActionResult.Ok));
                g.Simulate(45);
            }

            Assert.That(g.AchievedSpecies, Does.Contain("gentoo"));
            Assert.That(g.Species, Does.Contain("gentoo").And.Contain(PenguinCatalog.Onboarding));
            var arrived = g.DrainEvents().Where(e => e.Kind == IslandEventKind.PenguinArrived).Select(e => PenguinCatalog.AllIds[e.Value]);
            Assert.That(arrived, Does.Contain("gentoo"));
            Assert.That(PenguinDex.FacilityMultiplier(g.Species, FacilityKind.Fishery), Is.GreaterThan(1.0), "ジェンツーは漁場を速くする");

            var loaded = IslandGame.FromSave(g.ToSave(), g.Parameters, Start);
            Assert.That(loaded.AchievedSpecies, Does.Contain("gentoo"));
            loaded.SyncWorld(100, 10, null);
            Assert.That(loaded.Species, Does.Contain("gentoo"), "記録の節目の種と合わせても消えない");
        }

        [Test]
        public void 図鑑の19種すべてに解放の条件がある()
        {
            Assert.That(PenguinUnlocks.All.Select(u => u.Species), Is.EquivalentTo(PenguinCatalog.AllIds));
            foreach (var u in PenguinUnlocks.All)
            {
                Assert.That(u.Condition, Is.Not.Empty, u.Species);
                Assert.That(u.Goal, Is.GreaterThan(0), u.Species);
            }

            Assert.That(PenguinUnlocks.All.Count(u => u.Source == UnlockSource.Island), Is.EqualTo(12));
        }

        [Test]
        public void 写真と歩数は時計を戻した日には受け取れない()
        {
            var g = New();
            var verdict = new EcoPhotoVerdict(EcoSubject.EcoBag, "", 0.9);
            Assert.That(g.ReceivePhoto(verdict, 10), Is.Not.Null);
            Assert.That(g.ReceivePhoto(verdict, 11), Is.Not.Null);
            Assert.That(g.CanReceivePhoto(10), Is.False);
            Assert.That(g.ReceivePhoto(verdict, 10), Is.Null, "11 日に受け取った後で 10 日に戻しても受け取れない");

            Assert.That(g.ReceiveSteps(5000, 10), Is.EqualTo(10));
            Assert.That(g.ReceiveSteps(5000, 11), Is.EqualTo(10));
            Assert.That(g.ReceiveSteps(10000, 10), Is.EqualTo(0), "前の日の歩数は資源にしない");
            Assert.That(g.ReceiveSteps(6000, 11), Is.EqualTo(2), "今日の分は続けて受け取れる");
        }

        [Test]
        public void お魚の桁を入れる前の保存は_持っているお魚と建てた施設まで開いている()
        {
            var g = Rich();
            g.Build(FacilityKind.Kamaboko, Inland);
            var save = g.ToSave();
            save.version = 1;
            save.maxFishHeld = 0;
            save.stock[(int)Item.Fish] = 50;

            var loaded = IslandGame.FromSave(save, g.Parameters, Start);
            Assert.That(loaded.IsUnlocked(FacilityKind.Kamaboko), Is.True);
            Assert.That(loaded.IsUnlocked(FacilityKind.Oden), Is.False);
        }
    }
}
