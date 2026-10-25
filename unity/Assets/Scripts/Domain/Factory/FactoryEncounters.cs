using System;
using System.Globalization;
using System.Linq;
using ProjectPenguin.Domain.Encounter;

namespace ProjectPenguin.Domain.Factory
{
    public sealed partial class FactorySession
    {
        public FactoryResult BeginEncounterDay(int day, string newToken) => Change(s =>
        {
            if (day < 0 || day < s.encounterDay) return Fail("すれ違いの日付が戻っています");
            if (day == s.encounterDay) return Ok("今日の台帳を再利用します");
            if (!EncounterToken.IsValid(newToken)) return Fail("すれ違いの準備に失敗しました");
            s.encounterDay = day; s.selfToken = newToken; s.dailyBirds = 0;
            s.peers.Clear(); s.rewardedPairs.Clear();
            return Ok("新しい日のすれ違い台帳を保存しました");
        });

        public FactoryResult ObservePeer(string token, long nowMs) => Change(s =>
        {
            if (s.encounterDay < 0 || nowMs < 0 || !EncounterToken.IsValid(token) || token == s.selfToken)
                return Fail("有効な相手ではありません");
            if (s.peers.Any(p => p.token == token)) return Fail("今日すでに見た相手です");
            if (s.peers.Count >= EncounterLedger.MaxPeersPerDay) return Fail("今日の確認数の上限です");
            s.peers.Add(new FactoryPeer { token = token, retryAt = nowMs });
            return Ok("相手を台帳に保存しました");
        });

        /// <summary>報告が通信の失敗で届かなかった相手を、<paramref name="retryAtMs"/> まで送り直さない。</summary>
        public FactoryResult DeferPeer(string peer, long retryAtMs) => Change(s =>
        {
            var p = s.peers.FirstOrDefault(item => item.token == peer);
            if (p == null || p.matched || p.dropped || retryAtMs < 0) return Fail("今日の確認待ちの相手ではありません");
            p.retryAt = retryAtMs; return Ok("相手の確認を送り直します");
        });

        /// <summary>backend が受け付けなかった相手 (壊れたトークンなど) を、今日の確認から外す。</summary>
        public FactoryResult DropPeer(string peer) => Change(s =>
        {
            var p = s.peers.FirstOrDefault(item => item.token == peer);
            if (p == null || p.matched || p.dropped) return Fail("今日の確認待ちの相手ではありません");
            p.dropped = true; return Ok("受け付けられなかった相手を除きました");
        });

        // The transport adapter (FactoryGameEncounters) is separate; this ledger never sends requests itself.
        public FactoryResult ConfirmEncounter(int day, string peer, string pairId, bool matched, long nowMs) => Change(s =>
        {
            var p = s.peers.FirstOrDefault(item => item.token == peer);
            if (day != s.encounterDay || p == null || p.matched || p.dropped || nowMs < 0
                || pairId != EncounterToken.PairId(s.selfToken, peer)) return Fail("今日の有効な確認結果ではありません");
            if (!matched) { p.retryAt = checked(nowMs + EncounterLedger.RetryMs); return Ok("相手の確認を待っています"); }
            p.matched = true;
            if (s.dailyBirds >= _catalog.dailyBirds) return Ok("すれ違い成立。今日の獲得上限です");
            if (s.rewardedPairs.Contains(pairId)) return Fail("このすれ違いは受領済みです");
            var unlocked = _catalog.penguins.Where(b => b.stage <= s.stage).ToArray();
            var unseen = unlocked.Where(b => FactoryState.Amount(s.penguins, b.id) == 0).ToArray();
            var candidates = unseen.Length > 0 && s.pityMisses >= _catalog.pityAfter ? unseen : unlocked;
            var seed = uint.Parse(pairId.Substring(0, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var point = seed % (uint)candidates.Sum(b => b.weight);
            var selected = candidates[0];
            foreach (var b in candidates)
            { if (point < b.weight) { selected = b; break; } point -= (uint)b.weight; }
            var isNew = FactoryState.Amount(s.penguins, selected.id) == 0;
            FactoryState.Add(s.penguins, selected.id, 1);
            s.pityMisses = isNew || unseen.Length == 0 ? 0 : checked(s.pityMisses + 1);
            s.dailyBirds++; s.rewardedPairs.Add(pairId);
            return Ok(selected.label + "を獲得し、成立台帳と一緒に保存しました", 1);
        });

        public double SpeciesProbability(string id)
        {
            var species = _catalog.Penguin(id);
            if (species == null || species.stage > _state.stage) return 0;
            return (double)species.weight / _catalog.penguins.Where(p => p.stage <= _state.stage).Sum(p => p.weight);
        }
    }
}
