using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Encounter;

namespace ProjectPenguin.Backend
{
    /// <summary>すれ違いの報告の結果 (api/openapi.yaml の EncounterResult)。</summary>
    public sealed class EncounterReply
    {
        public EncounterReply(bool matched, string pairId)
        {
            Matched = matched;
            PairId = pairId ?? "";
        }

        /// <summary>相手も自分を報告していれば true。</summary>
        public bool Matched { get; }

        /// <summary>2 つのトークンの組の ID。両方の端末で同じ値。</summary>
        public string PairId { get; }
    }

    /// <summary>すれ違いの相互確認の口。失敗は <see cref="BackendException"/> で知らせる。</summary>
    public interface IEncounterReporter
    {
        Task<EncounterReply> ReportAsync(string day, string selfToken, string peerToken, CancellationToken cancellationToken);
    }

    /// <summary>backend の <c>POST /v1/encounters</c>。トークンはログにも例外の文にも入れない。</summary>
    public sealed class HttpEncounterReporter : IEncounterReporter
    {
        private readonly PenguinApiClient _client;

        public HttpEncounterReporter(PenguinApiClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<EncounterReply> ReportAsync(string day, string selfToken, string peerToken, CancellationToken cancellationToken)
        {
            var dto = await _client.ReportEncounterAsync(day, selfToken, peerToken, cancellationToken);
            return new EncounterReply(dto.matched, dto.pair_id);
        }
    }

    /// <summary>
    /// backend を使わないとき (Editor・接続先なし)。backend と同じ規則 (両方の報告がそろったら成立) を端末の中だけで持つ。
    /// 開発用の「合成すれ違い」で両方向を報告すると成立する。
    /// </summary>
    public sealed class MockEncounterReporter : IEncounterReporter
    {
        private readonly Dictionary<string, HashSet<string>> _seen = new Dictionary<string, HashSet<string>>();

        public Task<EncounterReply> ReportAsync(string day, string selfToken, string peerToken, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!EncounterToken.IsValid(selfToken) || !EncounterToken.IsValid(peerToken) || selfToken == peerToken)
            {
                throw new BackendException(BackendErrorCodes.TokenInvalid, null, false, 400);
            }

            var pairId = EncounterToken.PairId(selfToken, peerToken);
            var key = day + "#" + pairId;
            if (!_seen.TryGetValue(key, out var seen))
            {
                seen = new HashSet<string>();
                _seen[key] = seen;
            }

            seen.Add(selfToken);
            return Task.FromResult(new EncounterReply(seen.Count == 2, pairId));
        }
    }
}
