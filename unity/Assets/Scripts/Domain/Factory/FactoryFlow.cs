using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectPenguin.Domain.Factory
{
    public sealed partial class FactorySession
    {
        private string _layoutKey;
        private Dictionary<int, int[]> _neighbors;
        private readonly Dictionary<int, Dictionary<int, int>> _towardTarget = new Dictionary<int, Dictionary<int, int>>();
        public FactoryResult AdvanceTo(long milliseconds) => Change(s =>
        {
            if (milliseconds < s.requestedMs || milliseconds - s.requestedMs > _rules.maxAdvanceMs)
                return Fail("時間の逆行、または1回120秒を超える精算はできません");
            s.requestedMs = milliseconds;
            while (milliseconds - s.atMs >= _rules.tickMs)
            {
                s.atMs = checked(s.atMs + _rules.tickMs);
                foreach (var c in s.cargo.Where(c => c.arrivesAt <= s.atMs).ToArray())
                {
                    var to = s.facilities.Single(f => f.id == c.to);
                    FactoryState.Add(to.stock, c.resource, c.amount); s.cargo.Remove(c);
                }
                foreach (var n in s.facilities.OrderBy(n => n.id))
                {
                    if (n.processEnd > 0 && n.processEnd <= s.atMs)
                    {
                        FactoryState.Add(n.stock, n.processOutput, n.processYield);
                        n.processEnd = 0; n.processYield = 0; n.processOutput = null;
                    }
                }
                foreach (var n in s.facilities.OrderBy(n => n.id)) TryStart(s, n);
                Dispatch(s);
            }
            return Ok("生産と配送を保存しました");
        });

        public int ComboCount(FactoryState s, FactoryFacility n) => s.facilities.Count(o =>
            o.id != n.id && !o.retired && o.upper == n.upper && o.recipe == n.recipe
            && Math.Abs((long)o.x - n.x) + Math.Abs((long)o.y - n.y) <= _rules.comboRadius);

        private long BaseYield(FactoryState s, FactoryFacility n)
        {
            var recipe = _catalog.Recipe(n.recipe);
            if (recipe == null) return 0;
            var basis = checked(recipe.yieldAmount * _catalog.Growth(n.level));
            return checked((long)decimal.Floor((decimal)basis * (100 + (long)n.effort * _rules.effortPercent)
                * (100 + (long)ComboCount(s, n) * _rules.comboPercent) / 10000));
        }

        private long PreviewYield(FactoryState s, FactoryFacility n)
        {
            var recipe = _catalog.Recipe(n.recipe);
            return recipe == null ? 0 : CalculateBoost(s, recipe.output, BaseYield(s, n), false);
        }

        public long Capacity(FactoryState s, FactoryFacility n, string resource)
        {
            var baseCapacity = checked(_catalog.storage * _catalog.Growth(n.level));
            if (n.recipe == "warehouse") return checked(baseCapacity * WarehouseMultiplier(s, n));
            var r = _catalog.Recipe(n.recipe);
            var input = r.inputs.FirstOrDefault(i => i.id == resource)?.amount ?? 0;
            return Math.Max(baseCapacity, Math.Max(checked(input * 2), Math.Max(n.processYield,
                r.output == resource ? PreviewYield(s, n) : 0)));
        }

        private long Reserved(FactoryState s, FactoryFacility n, string resource) => checked(
            s.cargo.Where(c => c.to == n.id && c.resource == resource).Sum(c => c.amount)
            + (n.processEnd > 0 && n.processOutput == resource ? n.processYield : 0));

        private void TryStart(FactoryState s, FactoryFacility n)
        {
            var r = _catalog.Recipe(n.recipe);
            if (r == null || !n.enabled || n.retired || n.processEnd > 0) return;
            if (r.inputs.Any(i => FactoryState.Amount(n.stock, i.id) < i.amount)) return;
            var gas = checked(r.energy[0] + (long)n.effort * _rules.effortGas);
            if (s.gas < gas || s.electric < r.energy[1] || s.water < r.energy[2]) return;
            var output = PreviewYield(s, n);
            if ((decimal)FactoryState.Amount(n.stock, r.output) + Reserved(s, n, r.output) + output > Capacity(s, n, r.output)) return;
            s.gas -= gas; s.electric -= r.energy[1]; s.water -= r.energy[2];
            foreach (var input in r.inputs) FactoryState.Add(n.stock, input.id, -input.amount);
            n.processYield = Boost(s, r.output, BaseYield(s, n));
            n.processOutput = r.output; n.processEnd = checked(s.atMs + r.durationMs);
        }

        public string StopReason(FactoryState s, FactoryFacility n)
        {
            if (n.processEnd > 0) return "加工中（支払済み）";
            if (n.retired) return "組み合わせ解除済み。在庫は納品または残った内部施設から回収";
            if (n.recipe == "warehouse") return "自動配送・保管";
            if (!n.enabled) return "生産停止中・中継は継続";
            var r = _catalog.Recipe(n.recipe);
            var missing = r.inputs.FirstOrDefault(i => FactoryState.Amount(n.stock, i.id) < i.amount);
            if (missing != null) return _catalog.Resource(missing.id).label + "不足";
            if (s.gas < r.energy[0] + (long)n.effort * _rules.effortGas) return "ガス不足";
            if (s.electric < r.energy[1]) return "電気不足";
            if (s.water < r.energy[2]) return "水不足";
            if ((decimal)FactoryState.Amount(n.stock, r.output) + Reserved(s, n, r.output) + PreviewYield(s, n) > Capacity(s, n, r.output))
                return "出力在庫が満杯";
            return "次の開始を待機";
        }

        private static bool Adjacent(FactoryFacility a, FactoryFacility b)
        {
            if (!a.upper && !b.upper) return Math.Abs((long)a.x - b.x) + Math.Abs((long)a.y - b.y) == 1;
            if (a.upper && b.upper) return a.x == b.x && a.y == b.y;
            var upper = a.upper ? a : b;
            var lower = a.upper ? b : a;
            return Block(lower.x) == upper.x && Block(lower.y) == upper.y;
        }

        private void PreparePaths(FactoryState s)
        {
            var key = string.Join(";", s.facilities.Select(n => $"{n.id},{n.x},{n.y},{n.upper},{n.retired}"));
            if (key == _layoutKey) return;
            _layoutKey = key; _towardTarget.Clear();
            _neighbors = s.facilities.ToDictionary(n => n.id,
                n => s.facilities.Where(o => o.id != n.id && RoutingAdjacent(s, n, o)).OrderBy(o => o.id).Select(o => o.id).ToArray());
        }

        private static bool RoutingAdjacent(FactoryState s, FactoryFacility a, FactoryFacility b)
        {
            if (a.upper && a.retired || b.upper && b.retired)
            {
                var upper = a.upper && a.retired ? a : b;
                var other = upper == a ? b : a;
                var anchor = s.facilities.Where(n => !n.upper && Block(n.x) == upper.x && Block(n.y) == upper.y)
                    .OrderBy(n => n.id).FirstOrDefault();
                return anchor != null && other.id == anchor.id;
            }
            return Adjacent(a, b);
        }

        private List<int> Path(FactoryFacility from, FactoryFacility to)
        {
            if (!_towardTarget.TryGetValue(to.id, out var previous))
            {
                previous = new Dictionary<int, int> { [to.id] = 0 };
                var queue = new Queue<int>(); queue.Enqueue(to.id);
                while (queue.Count > 0)
                {
                    var id = queue.Dequeue();
                    foreach (var next in _neighbors[id])
                        if (!previous.ContainsKey(next)) { previous[next] = id; queue.Enqueue(next); }
                }
                _towardTarget[to.id] = previous;
            }
            if (!previous.ContainsKey(from.id)) return null;
            var result = new List<int>();
            for (var at = from.id; at != 0; at = previous[at]) result.Add(at);
            return result;
        }

        private void Dispatch(FactoryState s)
        {
            PreparePaths(s);
            var donors = _catalog.resources.ToDictionary(r => r.id,
                r => s.facilities.Where(n => FactoryState.Amount(n.stock, r.id) > 0).OrderBy(n => n.id).ToArray());
            var reservations = new Dictionary<(int, string), long>();
            foreach (var c in s.cargo)
            {
                var key = (c.to, c.resource); reservations.TryGetValue(key, out var current);
                reservations[key] = checked(current + c.amount);
            }
            foreach (var n in s.facilities.Where(n => n.processEnd > 0))
            {
                var key = (n.id, n.processOutput); reservations.TryGetValue(key, out var current);
                reservations[key] = checked(current + n.processYield);
            }
            var budgets = s.facilities.ToDictionary(n => n.id, n => checked(
                (n.recipe == "warehouse" ? _catalog.warehouseRelay : _catalog.relay) * _catalog.Growth(n.level) * WarehouseMultiplier(s, n)));
            var targets = s.facilities.Where(n => n.recipe != "warehouse" && n.enabled && !n.retired).OrderBy(n => n.id).ToArray();
            for (var offset = 0; offset < targets.Length; offset++)
            {
                var target = targets[(s.dispatchCursor + offset) % targets.Length];
                foreach (var input in _catalog.Recipe(target.recipe).inputs)
                {
                    var deficit = input.amount - FactoryState.Amount(target.stock, input.id) - s.cargo
                        .Where(c => c.to == target.id && c.resource == input.id).Sum(c => c.amount);
                    SendAvailable(s, target, input.id, Math.Max(0, deficit), budgets, donors, reservations, false);
                }
            }
            if (targets.Length > 0) s.dispatchCursor = (s.dispatchCursor + 1) % targets.Length;
            // Consumers reserve first. Only remaining surplus moves to warehouses; warehouses never bounce stock to each other.
            foreach (var target in s.facilities.Where(n => n.recipe == "warehouse" && !n.upper).OrderBy(n => n.id))
                foreach (var resource in _catalog.resources)
                {
                    var room = Capacity(s, target, resource.id) - FactoryState.Amount(target.stock, resource.id) - Reserved(s, target, resource.id);
                    SendAvailable(s, target, resource.id, Math.Max(0, room), budgets, donors, reservations, true);
                }
        }

        private void SendAvailable(FactoryState s, FactoryFacility to, string resource, long requested, Dictionary<int, long> budgets,
            Dictionary<string, FactoryFacility[]> donors, Dictionary<(int, string), long> reservations, bool surplusOnly)
        {
            foreach (var from in donors[resource])
            {
                if (requested <= 0) break;
                if (from.id == to.id || surplusOnly && from.recipe == "warehouse") continue;
                var keep = from.enabled && !from.retired ? _catalog.Recipe(from.recipe)?.inputs.FirstOrDefault(i => i.id == resource)?.amount ?? 0 : 0;
                var available = FactoryState.Amount(from.stock, resource) - keep;
                if (available <= 0) continue;
                var path = Path(from, to);
                if (path == null) continue;
                reservations.TryGetValue((to.id, resource), out var reserved);
                var room = Capacity(s, to, resource) - FactoryState.Amount(to.stock, resource) - reserved;
                var amount = Math.Min(Math.Min(available, requested), Math.Min(room, path.Min(id => budgets[id])));
                if (amount <= 0) continue;
                FactoryState.Add(from.stock, resource, -amount);
                foreach (var id in path) budgets[id] -= amount;
                s.cargo.Add(new FactoryCargo { from = from.id, to = to.id, resource = resource, amount = amount,
                    path = path, arrivesAt = checked(s.atMs + (path.Count - 1) * _rules.tickMs) });
                reservations[(to.id, resource)] = checked(reserved + amount);
                requested -= amount;
            }
        }

        private void ReconcileFormations(FactoryState s)
        {
            var active = new HashSet<int>();
            foreach (var block in s.blocks)
            {
                var members = s.facilities.Where(n => !n.upper && Block(n.x) == block.x && Block(n.y) == block.y).ToArray();
                if (members.Length != 4) continue;
                var upper = FactoryCatalog.Formations.FirstOrDefault(f => f.Missing(members.Select(n => n.recipe)).Length == 0)?.Recipe;
                if (upper == null || upper != "warehouse" && _catalog.Recipe(upper).tier > s.stage) continue;
                var existing = s.facilities.FirstOrDefault(n => n.upper && n.x == block.x && n.y == block.y && n.recipe == upper);
                if (existing == null)
                {
                    existing = new FactoryFacility { id = s.nextId, x = block.x, y = block.y, recipe = upper, upper = true, level = s.stage };
                    s.nextId = checked(s.nextId + 1); s.facilities.Add(existing);
                }
                existing.retired = false; active.Add(existing.id);
            }
            foreach (var upper in s.facilities.Where(n => n.upper && !active.Contains(n.id))) upper.retired = true;
        }

        private int WarehouseMultiplier(FactoryState s, FactoryFacility n)
        {
            if (n.recipe != "warehouse") return 1;
            if (n.upper) return 1; // The upper node is a view/relay port, not a second copy of the internal stores.
            return s.facilities.Any(u => u.upper && !u.retired && u.recipe == "warehouse" && u.x == Block(n.x) && u.y == Block(n.y))
                ? _rules.largeWarehouseMultiplier : 1;
        }
    }
}
