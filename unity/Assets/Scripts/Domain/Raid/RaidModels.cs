using System;

namespace ProjectPenguin.Domain.Raid
{
    // Wire DTOs deliberately use fields: Unity JsonUtility and the API share this shape.
    [Serializable]
    public sealed class RaidProgress
    {
        public int schema_version = 1;
        public string event_id;
        public string content_version;
        public string title;
        public string status;
        public string starts_at;
        public string ends_at;
        public long revision;
        public long total_points;
        public long target_points;
        public bool local_demo;
        public double Ratio => target_points <= 0 ? 0 : Math.Min(1.0, Math.Max(0, (double)total_points / target_points));
        public bool Active => status == "active";
    }
    [Serializable]
    public sealed class RaidOption { public string id; public string text; }
    [Serializable]
    public sealed class RaidQuestion { public string id; public string prompt; public RaidOption[] options; }
    [Serializable]
    public sealed class RaidAnswerRequest
    {
        public string request_id;
        public string question_id;
        public string content_version;
        public string option_id;
    }
    [Serializable]
    public sealed class RaidAnswerResult
    {
        public string answer_id;
        public string question_id;
        public string option_id;
        public bool correct;
        public string explanation;
        public string source_url;
        public int points_pending;
        public bool acknowledged;
    }
    [Serializable]
    public sealed class RaidPersonalState
    {
        public int schema_version = 1;
        public string event_id;
        public string content_version;
        public int answered_count;
        public int personal_points;
        public bool completed;
        public RaidQuestion question;
        public RaidAnswerResult pending_answer;
    }
    [Serializable]
    public sealed class RaidParticipant
    {
        public int schema_version = 1;
        public string token;
        public string expires_at;
        public RaidPersonalState personal;
    }
    [Serializable]
    public sealed class RaidAckResult
    {
        public int schema_version = 1;
        public string answer_id;
        public int awarded_points;
        public int personal_points;
        public RaidProgress progress;
    }
    [Serializable]
    public sealed class RaidParticipation
    {
        public int version = 1;
        public string event_id;
        public string content_version;
        public string join_request_id;
        public string token;
        public string expires_at;
        public RaidAnswerRequest pending_request;
        public int seen_milestones;
        public bool souvenir_received;
    }
    public interface IRaidParticipationStore
    {
        RaidParticipation Load(string eventId);
        void Save(RaidParticipation state);
        void Delete(string eventId);
    }
    [Serializable] public sealed class ClassroomProgress
    {
        public int schema_version;
        public string event_id, content_version, title, phase, end_reason, starts_at, ends_at, ended_at;
        public long revision, total_points, target_points;
        public bool local_demo;
        public bool Terminal => phase=="succeeded" || phase=="ended";
        public double Ratio => target_points<=0?0:(double)total_points/target_points;
        public RaidProgress ToVisualProgress() => new RaidProgress {event_id=event_id,content_version=content_version,title=title,
            status=phase=="waiting"?"scheduled":Terminal?"ended":"active",revision=revision,total_points=total_points,target_points=target_points,local_demo=local_demo};
    }
    [Serializable] public sealed class ClassroomQuestion
    {
        public string attempt_id, question_id, prompt;
        public long attempt_seq;
        public RaidOption[] options;
    }
    [Serializable] public sealed class ClassroomAnswer
    {
        public string answer_id, attempt_id, explanation, source_url;
        public long attempt_seq;
        public bool correct;
        public int points_pending;
    }
    [Serializable] public sealed class ClassroomPersonalState
    {
        public int schema_version;
        public string event_id, content_version, phase;
        public long personal_version, answered_count, personal_points;
        public ClassroomQuestion question;
        public ClassroomAnswer pending_answer;
    }
    [Serializable] public sealed class ClassroomNextRequest { public string request_id, content_version; }
    [Serializable] public sealed class ClassroomAnswerRequest { public string request_id, attempt_id, content_version, option_id; }
    [Serializable] public class ClassroomStateResult { public ClassroomPersonalState personal; public ClassroomProgress progress; }
    [Serializable] public sealed class ClassroomJoinResult : ClassroomStateResult { public string token, expires_at; }
    [Serializable] public sealed class ClassroomNextResult : ClassroomStateResult { public string issued_attempt_id; }
    [Serializable] public sealed class ClassroomAnswerResponse : ClassroomStateResult { public ClassroomAnswer answer; }
    [Serializable] public sealed class ClassroomReceipt
    {
        public string answer_id, attempt_id;
        public int awarded_points;
        public long personal_points_after, progress_revision;
    }
    [Serializable] public sealed class ClassroomAckResult : ClassroomStateResult { public ClassroomReceipt receipt; }
    [Serializable] public sealed class ClassroomParticipation
    {
        public int version=2;
        public string event_id, content_version, join_request_id, token, expires_at;
        public ClassroomNextRequest pending_next;
        public ClassroomAnswerRequest pending_answer;
    }
    public interface IClassroomParticipationStore
    {
        ClassroomParticipation Load(string eventId);
        void Save(ClassroomParticipation state);
        void Delete(string eventId);
        System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken ct);
    }
}
