using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Raid;

namespace ProjectPenguin.Backend
{
    public interface IRaidClient
    {
        Task<RaidProgress> GetEventAsync(string eventId, CancellationToken ct);
        Task<RaidParticipant> JoinAsync(string eventId, string joinRequestId, CancellationToken ct);
        Task<RaidPersonalState> GetMeAsync(string eventId, string token, CancellationToken ct);
        Task<RaidAnswerResult> AnswerAsync(string eventId, string token, RaidAnswerRequest request, CancellationToken ct);
        Task<RaidAckResult> AcknowledgeAsync(string eventId, string token, string answerId, CancellationToken ct);
    }
    public sealed class RaidClientException : Exception
    {
        public RaidClientException(string code, string message, bool retryable = false, string retryAfter = null) : base(message)
        { Code = code; Retryable = retryable; RetryAfter = retryAfter; }
        public string RetryAfter { get; }
        public string Code { get; }
        public bool Retryable { get; }
    }

    public interface IClassroomJsonCodec
    {
        string Serialize<T>(T value);
        T Deserialize<T>(string json);
    }
    public interface IClassroomRaidClient
    {
        Task<ClassroomProgress> GetEventAsync(string eventId, CancellationToken ct);
        Task<ClassroomJoinResult> JoinAsync(string eventId, string joinRequestId, CancellationToken ct);
        Task<ClassroomStateResult> GetMeAsync(string eventId, string token, CancellationToken ct);
        Task<ClassroomNextResult> NextAsync(string eventId, string token, ClassroomNextRequest request, CancellationToken ct);
        Task<ClassroomAnswerResponse> AnswerAsync(string eventId, string token, ClassroomAnswerRequest request, CancellationToken ct);
        Task<ClassroomAckResult> AcknowledgeAsync(string eventId, string token, string answerId, CancellationToken ct);
    }
}
