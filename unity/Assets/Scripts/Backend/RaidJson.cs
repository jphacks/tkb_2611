using ProjectPenguin.Domain.Raid;
using UnityEngine;

namespace ProjectPenguin.Backend
{
    /// <summary>JsonUtility creates empty inline objects for absent/null optional DTOs.</summary>
    public static class RaidJson
    {
        public static T Deserialize<T>(string json)
        {
            var value = JsonUtility.FromJson<T>(json);
            if (value is RaidPersonalState personal) Normalize(personal);
            if (value is RaidParticipant participant) Normalize(participant.personal);
            if (value is RaidParticipation participation && Empty(participation.pending_request))
                participation.pending_request = null;
            if (value is ClassroomStateResult state) Normalize(state.personal);
            if (value is ClassroomPersonalState classroom) Normalize(classroom);
            if (value is ClassroomParticipation saved) ClassroomState.NormalizeSaved(saved);
            if(value is ClassroomProgress progress) Normalize(progress);
            if(value is ClassroomStateResult result) Normalize(result.progress);
            return value;
        }

        private static void Normalize(ClassroomProgress p)
        {
            if(p==null) return;
            if(p.end_reason=="") p.end_reason=null;
            if(p.starts_at=="") p.starts_at=null;
            if(p.ended_at=="") p.ended_at=null;
        }
        private static void Normalize(ClassroomPersonalState p)
        {
            if(p==null) return;
            var q=p.question;
            if(q!=null && string.IsNullOrEmpty(q.attempt_id) && string.IsNullOrEmpty(q.question_id) && string.IsNullOrEmpty(q.prompt) &&
                q.attempt_seq==0 && (q.options==null || q.options.Length==0)) p.question=null;
            var a=p.pending_answer;
            if(a!=null && string.IsNullOrEmpty(a.answer_id) && string.IsNullOrEmpty(a.attempt_id) && string.IsNullOrEmpty(a.explanation) &&
                string.IsNullOrEmpty(a.source_url) && a.attempt_seq==0 && !a.correct && a.points_pending==0) p.pending_answer=null;
        }

        private static void Normalize(RaidPersonalState personal)
        {
            if (personal == null) return;
            var question = personal.question;
            if (question != null && string.IsNullOrEmpty(question.id) && string.IsNullOrEmpty(question.prompt) &&
                (question.options == null || question.options.Length == 0)) personal.question = null;
            var answer = personal.pending_answer;
            if (answer != null && string.IsNullOrEmpty(answer.answer_id) && string.IsNullOrEmpty(answer.question_id) &&
                string.IsNullOrEmpty(answer.option_id) && string.IsNullOrEmpty(answer.explanation) &&
                string.IsNullOrEmpty(answer.source_url) && !answer.correct && !answer.acknowledged && answer.points_pending == 0)
                personal.pending_answer = null;
        }

        private static bool Empty(RaidAnswerRequest request) => request != null &&
            string.IsNullOrEmpty(request.request_id) && string.IsNullOrEmpty(request.question_id) &&
            string.IsNullOrEmpty(request.content_version) && string.IsNullOrEmpty(request.option_id);
    }
    public sealed class ClassroomJsonCodec : IClassroomJsonCodec
    {
        public string Serialize<T>(T value) => JsonUtility.ToJson(value);
        public T Deserialize<T>(string json) => RaidJson.Deserialize<T>(json);
    }
}
