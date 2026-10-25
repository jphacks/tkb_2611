using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ProjectPenguin.Domain.Encounter;

namespace ProjectPenguin.Domain.Factory
{
    /// <summary>All changes save a complete candidate before exposing it to the UI.</summary>
    public sealed partial class FactorySession
    {
        private FactoryState _state;
        private readonly FactoryCatalog _catalog;
        private readonly FactoryRules _rules;
        private readonly Action<FactoryState> _save;
        public FactoryState Snapshot => _state.Clone();
        public FactoryCatalog Catalog => _catalog;
        public FactoryRules Rules => _rules;

        public FactorySession(FactoryCatalog catalog, FactoryRules rules, Action<FactoryState> save, FactoryState restored = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            catalog.Validate(); rules.Validate();
            _state = restored?.Clone() ?? new FactoryState
            { gas = rules.startingGas, electric = rules.startingElectric, water = rules.startingWater };
            Validate(_state);
        }

        private FactoryResult Change(Func<FactoryState, FactoryResult> change)
        {
            try
            {
                var candidate = _state.Clone();
                var result = change(candidate);
                if (!result.Success) return result;
                Validate(candidate);
                _save(candidate.Clone());
                _state = candidate;
                return result;
            }
            catch (OverflowException) { return Fail("数量が扱える範囲を超えています。状態を変更していません"); }
            catch (IOException) { return Fail("保存できませんでした。空き容量を確認して再試行してください"); }
            catch (UnauthorizedAccessException) { return Fail("保存先へ書き込めません。状態を変更していません"); }
            catch (InvalidOperationException) { return Fail("状態を保存できません。更新または保存の復旧が必要です"); }
            catch (ArgumentException) { return Fail("在庫・予約・設定の整合性を保てない操作です。状態を変更していません"); }
        }

        private static FactoryResult Fail(string message) => new FactoryResult(false, message);
        private static FactoryResult Ok(string message, long amount = 0, int facilityId = 0) => new FactoryResult(true, message, amount, facilityId);

        public FactoryResult AcceptSteps(int day, int steps, int today) => Change(s =>
        {
            if (day < 0 || day != today || day < s.stepDay || steps < 0 || steps > 172800)
                return Fail("歩数の対象日または値を確認してください");
            var old = day == s.stepDay ? s.maxSteps : 0;
            var maximum = Math.Max(old, steps);
            var basic = maximum / _rules.stepsPerGas - old / _rules.stepsPerGas;
            var amount = Boost(s, "gas", basic);
            s.gas = checked(s.gas + amount); s.stepDay = day; s.maxSteps = maximum;
            return Ok("歩数の未受領分を保存しました", amount);
        });

        public FactoryResult AcceptScreen(string receipt, int day, int minutes, int today, bool confirmed) => Change(s =>
        {
            if (!confirmed || !ValidId(receipt) || day < 0 || day != today - 1 || minutes < 0 || minutes > 1440)
                return Fail("前日の確定スクリーンタイムを確認してください");
            var dayKey = "screen-day:" + day;
            if (s.receipts.Contains("screen:" + receipt) || s.receipts.Contains(dayKey)) return Fail("この記録日は受領済みです");
            var amount = Boost(s, "electric", (1440 - minutes) / _rules.screenMinutesPerElectric);
            s.electric = checked(s.electric + amount);
            s.receipts.Add(dayKey); s.receipts.Add("screen:" + receipt);
            return Ok("確認した前日の記録を保存しました", amount);
        });

        /// <summary>
        /// 飲みきった空のペットボトルの写真 1 枚から決めた飲んだ量 (ml) を受け取る。量は backend の見積もり (ラベルの容量 − 残り)。
        /// 同じ写真 (<paramref name="photo"/> は画像のハッシュ) は 1 回だけ。1 日の合計は <see cref="FactoryRules.waterDailyMaxMl"/> まで数える。
        /// </summary>
        public FactoryResult AcceptWaterPhoto(string photo, int day, int ml, int today, bool confirmed) => Change(s =>
        {
            if (!confirmed || !ValidId(photo) || day < 0 || day != today || day < s.waterDay || ml < 1 || ml > MaxWaterMlPerPhoto)
                return Fail("今日の空のペットボトルの写真を確認してください");
            var key = "water-photo:" + photo;
            if (s.receipts.Contains(key)) return Fail("この写真は受領済みです");
            var old = day == s.waterDay ? s.waterMl : 0;
            if (old >= _rules.waterDailyMaxMl) return Fail($"今日の水は{_rules.waterDailyMaxMl:N0}mlまで受け取り済みです");
            var total = Math.Min(_rules.waterDailyMaxMl, old + ml);
            var amount = Boost(s, "water", total / _rules.waterMlPerUnit - old / _rules.waterMlPerUnit);
            s.water = checked(s.water + amount); s.waterDay = day; s.waterMl = total; s.receipts.Add(key);
            return Ok($"飲んだ水{ml:N0}mlを保存しました", amount);
        });

        /// <summary>写真 1 枚で受け取る量の上限 (契約の <c>amount_ml</c> の上限と同じ)。</summary>
        public const int MaxWaterMlPerPhoto = 2000;

        public long NextBuildCost => checked(_rules.buildBase + _state.builtCount * _rules.buildIncrement);
        public FactoryResult Build(string recipe, int x, int y) => Change(s =>
        {
            var r = _catalog.Recipe(recipe);
            if (recipe != "warehouse" && (r == null || r.tier > s.stage)) return Fail("施設が未解放です");
            if (!Fits(s, x, y, 0)) return Fail("開いている空きマスに配置してください");
            var cost = checked(_rules.buildBase + s.builtCount * _rules.buildIncrement);
            if (s.electric < cost) return Fail("建設に必要な電気が不足しています");
            s.electric -= cost; s.builtCount = checked(s.builtCount + 1);
            var id = s.nextId; s.nextId = checked(s.nextId + 1);
            s.facilities.Add(new FactoryFacility { id = id, x = x, y = y, recipe = recipe, level = s.stage });
            ReconcileFormations(s);
            return Ok("施設を建設しました", -cost, id);
        });

        public FactoryResult Move(int id, int x, int y) => Change(s =>
        {
            var n = s.facilities.FirstOrDefault(f => f.id == id && !f.upper);
            if (n == null || !Fits(s, x, y, id)) return Fail("開いている空きマスに移動してください");
            if (s.cargo.Any(c => c.path.Contains(id))) return Fail("この施設を通る配送の到着を待ってください");
            // Upper stock and paid batches stay in a retired node at their original block until drained.
            n.x = x; n.y = y; ReconcileFormations(s);
            return Ok("移設しました。在庫と支払済みの加工を保持します");
        });

        public FactoryResult Expand(int blockX, int blockY) => Change(s =>
        {
            if (Math.Abs((long)blockX) > 1000 || Math.Abs((long)blockY) > 1000 || s.blocks.Count >= _rules.maxBlocks
                || s.blocks.Any(b => b.x == blockX && b.y == blockY)
                || !s.blocks.Any(b => Math.Abs((long)b.x - blockX) + Math.Abs((long)b.y - blockY) == 1))
                return Fail("既存の土地に接する2×2区画を選んでください");
            if (s.water < _rules.expandWater) return Fail("拡張用の水が不足しています");
            s.water -= _rules.expandWater; s.blocks.Add(new FactoryCell(blockX, blockY));
            return Ok("最下層の土地を2×2マス拡張しました");
        });

        public FactoryResult SetEffort(int id, int effort) => Change(s =>
        {
            var n = s.facilities.FirstOrDefault(f => f.id == id && f.recipe != "warehouse" && !f.retired);
            if (n == null || effort < 0 || effort > _rules.maxEffort) return Fail("エフォートの範囲を確認してください");
            n.effort = effort; return Ok("次の加工開始からエフォートを適用します");
        });

        public FactoryResult SetEnabled(int id, bool enabled) => Change(s =>
        {
            var n = s.facilities.FirstOrDefault(f => f.id == id && !f.retired);
            if (n == null) return Fail("施設がありません");
            n.enabled = enabled; return Ok(enabled ? "次の加工を再開します" : "新しい加工を停止します。中継と支払済みの加工は続きます");
        });

        public FactoryResult Deliver(string commandId, string resource, long quantity) => Change(s =>
        {
            var r = _catalog.Resource(resource);
            if (!ValidId(commandId) || r == null || quantity <= 0) return Fail("納品する品目と正の数量を確認してください");
            var key = "delivery:" + commandId;
            if (s.receipts.Contains(key)) return Fail("この納品は確定済みです");
            var available = s.facilities.Aggregate(0L, (total, f) => checked(total + FactoryState.Amount(f.stock, resource)));
            if (available < quantity) return Fail("納品用の在庫が不足しています");
            var score = checked(quantity * r.points);
            s.points = checked(s.points + score);
            var remaining = quantity;
            foreach (var f in s.facilities.OrderBy(n => n.id))
            {
                var take = Math.Min(remaining, FactoryState.Amount(f.stock, resource));
                if (take > 0) FactoryState.Add(f.stock, resource, -take);
                remaining -= take;
            }
            s.receipts.Add(key);
            var stage = 1;
            while (stage < 4 && s.points >= _rules.stagePoints[stage]) stage++;
            if (stage > s.stage)
            {
                var collectionComplete = _catalog.penguins.Where(p => p.stage <= s.stage).All(p => FactoryState.Amount(s.penguins, p.id) > 0);
                s.stage = stage;
                foreach (var f in s.facilities) f.level = Math.Max(f.level, stage);
                if (collectionComplete) s.pityMisses = 0;
                ReconcileFormations(s);
            }
            return Ok("在庫を納品し、累積評価を保存しました", score);
        });

        internal long Boost(FactoryState s, string resource, long basic) => CalculateBoost(s, resource, basic, true);
        private long CalculateBoost(FactoryState s, string resource, long basic, bool commit)
        {
            if (basic <= 0) return 0;
            decimal add = 0, multiply = 0;
            foreach (var p in _catalog.penguins.Where(p => p.target == resource))
            {
                var count = FactoryState.Amount(s.penguins, p.id);
                if (p.multiplicative) multiply += (decimal)count * p.percent; else add += (decimal)count * p.percent;
            }
            var numerator = checked((decimal)basic * (100 + add) * (100 + multiply) + FactoryState.Amount(s.carries, resource));
            var amount = checked((long)decimal.Floor(numerator / 10000));
            var carry = (long)(numerator % 10000);
            var old = FactoryState.Amount(s.carries, resource);
            if (commit) FactoryState.Add(s.carries, resource, carry - old);
            return amount;
        }

        private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128;

        private static int Block(int coordinate) => (int)Math.Floor(coordinate / 2.0);
        private static bool Fits(FactoryState s, int x, int y, int ignore) =>
            s.blocks.Any(b => b.x == Block(x) && b.y == Block(y))
            && !s.facilities.Any(f => !f.upper && f.id != ignore && f.x == x && f.y == y);
    }
}
