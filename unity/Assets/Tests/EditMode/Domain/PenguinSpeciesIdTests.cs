using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProjectPenguin.Domain.Clock;
using ProjectPenguin.Domain.Game;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Tests.EditMode.Domain
{
    /// <summary>
    /// 種のキーを 17b-zukan.csv の id にした変更 (19 §5、18a C26・D18) のテスト。game_sim_test.py の SpeciesIds と対応。
    /// </summary>
    public sealed class PenguinSpeciesIdTests
    {
        private static readonly Func<int, IReadOnlyList<ReceiptRecord>, double> Bonus0 = (w, rs) => 0.0;

        /// <summary>17b-zukan.csv の id と name_ja (行の順)。CSV を変えたらここも変える。</summary>
        private static readonly string[,] Zukan17b =
        {
            { "emperor", "コウテイペンギン" },
            { "king", "オウサマペンギン" },
            { "adelie", "アデリーペンギン" },
            { "chinstrap", "ヒゲペンギン" },
            { "gentoo", "ジェンツーペンギン" },
            { "little", "コビトペンギン" },
            { "galapagos", "ガラパゴスペンギン" },
            { "humboldt", "フンボルトペンギン" },
            { "magellanic", "マゼランペンギン" },
            { "african", "ケープペンギン" },
            { "yellow_eyed", "キンメペンギン" },
            { "macaroni", "マカロニペンギン" },
            { "royal", "ロイヤルペンギン" },
            { "northern_rockhopper", "キタイワトビペンギン" },
            { "western_rockhopper", "ミナミイワトビペンギン" },
            { "eastern_rockhopper", "ヒガシイワトビペンギン（仮称）" },
            { "fiordland", "キマユペンギン" },
            { "snares", "ハシブトペンギン" },
            { "erect_crested", "マユダチペンギン" },
        };

        private static DateTimeOffset T(int y, int mo, int d, int h, int mi = 0)
        {
            return new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.FromHours(9));
        }

        [Test]
        public void IdsAndDisplayNamesMatch17bZukan()
        {
            Assert.That(PenguinCatalog.AllIds.Count, Is.EqualTo(Zukan17b.GetLength(0)));
            for (var i = 0; i < Zukan17b.GetLength(0); i++)
            {
                Assert.That(PenguinCatalog.AllIds[i], Is.EqualTo(Zukan17b[i, 0]));
                Assert.That(PenguinCatalog.DisplayNameJa[Zukan17b[i, 0]], Is.EqualTo(Zukan17b[i, 1]));
            }

            Assert.That(PenguinCatalog.DisplayNameJa.Count, Is.EqualTo(19));
        }

        [Test]
        public void OrdersUseEveryIdExactlyOnce()
        {
            var order = PenguinCatalog.MilestoneOrder.Concat(PenguinCatalog.WeeklyOrder).ToList();
            Assert.That(order.Count, Is.EqualTo(19));
            Assert.That(new HashSet<string>(order).SetEquals(PenguinCatalog.AllIds), Is.True);
        }

        [Test]
        public void IdsAreAsciiSnakeCase()
        {
            foreach (var id in PenguinCatalog.AllIds)
            {
                Assert.That(Regex.IsMatch(id, "^[a-z][a-z_]*$"), Is.True, id);
                Assert.That(PenguinCatalog.IsKnown(id), Is.True, id);
            }

            Assert.That(PenguinCatalog.IsKnown("アデリーペンギン"), Is.False);
            Assert.That(PenguinCatalog.IsKnown(null), Is.False);
        }

        [Test]
        public void DexCoversEveryIdWithAFactAndABonus()
        {
            var dexIds = PenguinDex.Entries.Select(e => e.Species).ToList();
            Assert.That(dexIds.Count, Is.EqualTo(19));
            Assert.That(new HashSet<string>(dexIds).SetEquals(PenguinCatalog.AllIds), Is.True);
            foreach (var e in PenguinDex.Entries)
            {
                Assert.That(e.Name, Is.EqualTo(PenguinCatalog.DisplayNameJa[e.Species]));
                Assert.That(e.Fact, Is.Not.Empty, e.Species);
                Assert.That(e.BonusText, Is.Not.Empty, e.Species);
            }
        }

        [Test]
        public void ArrivalsHoldIdsOnly()
        {
            var c = new ManualClock(T(2026, 10, 27, 9));
            var s = new GameSession(c, Bonus0);
            Assert.That(s.Arrivals.Count, Is.GreaterThan(0));
            foreach (var a in s.Arrivals)
            {
                Assert.That(PenguinCatalog.IsKnown(a.Species), Is.True, a.Species);
            }
        }
    }
}
