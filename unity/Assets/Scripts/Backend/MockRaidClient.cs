using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Raid;

namespace ProjectPenguin.Backend
{
    /// <summary>Explicit local-only demo. Never substituted for a failed live service.</summary>
    public sealed class MockRaidClient : IRaidClient
    {
        private sealed class Person
        {
            public readonly Dictionary<string, RaidAnswerResult> Answers = new Dictionary<string, RaidAnswerResult>();
            public readonly Dictionary<string, RaidAnswerRequest> Requests = new Dictionary<string, RaidAnswerRequest>();
            public int Points;
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, string> _joins = new Dictionary<string, string>();
        private readonly Dictionary<string, Person> _people = new Dictionary<string, Person>();
        private readonly Func<DateTimeOffset> _now;
        private readonly DateTimeOffset _starts;
        private long _total;
        private long _revision;
        private string _status;
        public DateTimeOffset EndsAt { get; }
        public MockRaidClient(Func<DateTimeOffset> now = null)
        {
            _now = now ?? (() => DateTimeOffset.UtcNow);
            _starts = _now().AddDays(-1); EndsAt = _now().AddDays(1);
        }
        private void Event(string id, bool write = false)
        {
            if (id != "demo") throw Error("not_found", "このレイドは見つかりません。");
            if (write && (_now() < _starts || _now() >= EndsAt)) throw Error("event_closed", "このレイドの受付は終了しました。");
        }
        private Person Auth(string token)
        {
            if (string.IsNullOrEmpty(token) || !_people.TryGetValue(token, out var p)) throw Error("unauthorized", "参加し直してください。");
            return p;
        }
        private RaidProgress Snapshot()
        {
            var status=_now() >= EndsAt ? "ended" : _now() < _starts ? "scheduled" : "active";
            if (_status!=null && _status!=status) _revision++;
            _status=status;
            return new RaidProgress {
            event_id="demo", content_version="1", title="氷の道をつないで、ペンギンを助けよう",
            starts_at=_starts.ToString("O"), ends_at=EndsAt.ToString("O"),
            status=status,
            revision=_revision, total_points=_total, target_points=6, local_demo=true };
        }
        public Task<RaidProgress> GetEventAsync(string eventId, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); lock (_gate) { Event(eventId); return Task.FromResult(Snapshot()); } }
        public Task<RaidParticipant> JoinAsync(string eventId, string joinRequestId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); lock (_gate)
            {
                Event(eventId, true);
                if (string.IsNullOrEmpty(joinRequestId) || joinRequestId.Length > 128) throw Error("invalid_request", "参加の情報が正しくありません。");
                if (!_joins.TryGetValue(joinRequestId, out var token))
                { token=Guid.NewGuid().ToString("N"); _joins[joinRequestId]=token; _people[token]=new Person(); }
                return Task.FromResult(new RaidParticipant { token=token, expires_at=EndsAt.AddDays(7).ToString("O"), personal=Personal(Auth(token)) });
            }
        }
        public Task<RaidPersonalState> GetMeAsync(string eventId, string token, CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); lock (_gate) { Event(eventId); return Task.FromResult(Personal(Auth(token))); } }
        private static RaidPersonalState Personal(Person p)
        {
            var pending=p.Answers.Values.FirstOrDefault(a => !a.acknowledged);
            var index=p.Answers.Count;
            return new RaidPersonalState { event_id="demo", content_version="1", personal_points=p.Points,
                answered_count=index, completed=index==3 && pending==null,
                question=pending==null && index<3 ? Question(index) : null, pending_answer=pending==null ? null : Copy(pending) };
        }
        public Task<RaidAnswerResult> AnswerAsync(string eventId, string token, RaidAnswerRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); lock (_gate)
            {
                Event(eventId,true); var p=Auth(token);
                if (request==null || string.IsNullOrEmpty(request.request_id) || request.request_id.Length>128) throw Error("invalid_request", "回答の情報が正しくありません。");
                if (request.content_version!="1") throw Error("version_conflict", "問題が更新されました。開き直してください。");
                if (p.Requests.TryGetValue(request.request_id,out var old))
                {
                    if (old.question_id!=request.question_id || old.option_id!=request.option_id) throw Error("request_conflict", "送信済みの回答と違います。");
                    return Task.FromResult(Copy(p.Answers[old.question_id]));
                }
                if (p.Answers.ContainsKey(request.question_id ?? "")) throw Error("already_answered", "この問題には回答済みです。");
                var me=Personal(p);
                if (me.question==null || me.question.id!=request.question_id) throw Error("invalid_question", "現在の問題に回答してください。");
                if (!me.question.options.Any(o=>o.id==request.option_id)) throw Error("invalid_option", "選択肢が正しくありません。");
                var index=p.Answers.Count;
                var correct=request.option_id==new[] {"off","short","raise"}[index];
                var answer=new RaidAnswerResult { answer_id=Guid.NewGuid().ToString("N"), question_id=request.question_id,
                    option_id=request.option_id, correct=correct, points_pending=correct?2:1,
                    explanation=new[] {
                        "使っていない照明は、必要がなければ消すことが省エネにつながります。共有の照明は管理する人に確認しましょう。",
                        "取り出すものを決めて、冷蔵庫の扉を開ける時間を短くすると、冷気が逃げるのを抑えられます。",
                        "冷房は無理のない範囲で設定温度を上げる方向が省エネにつながります。体調と室温を優先しましょう。"
                    }[index], source_url=index==0 ? "https://www.enecho.meti.go.jp/category/saving_and_new/saving/government/data/r2fy_touki_shouene.pdf" :
                        index==2 ? "https://www.enecho.meti.go.jp/category/saving_and_new/saving/general/howto/airconditioning/index.html" : "https://www.enecho.meti.go.jp/about/special/johoteikyo/setsuden_tips.html" };
                p.Answers[request.question_id]=answer;
                p.Requests[request.request_id]=new RaidAnswerRequest { request_id=request.request_id, question_id=request.question_id,
                    option_id=request.option_id, content_version=request.content_version };
                return Task.FromResult(Copy(answer));
            }
        }
        public Task<RaidAckResult> AcknowledgeAsync(string eventId, string token, string answerId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); lock (_gate)
            {
                Event(eventId,true); var p=Auth(token);
                var answer=p.Answers.Values.FirstOrDefault(a=>a.answer_id==answerId);
                if (answer==null) throw Error("invalid_answer", "回答を確認できません。");
                if (!answer.acknowledged)
                { answer.acknowledged=true; p.Points+=answer.points_pending; _total+=answer.points_pending; _revision++; }
                return Task.FromResult(new RaidAckResult { answer_id=answerId, awarded_points=answer.points_pending,
                    personal_points=p.Points, progress=Snapshot() });
            }
        }
        private static RaidAnswerResult Copy(RaidAnswerResult a) => new RaidAnswerResult {
            answer_id=a.answer_id, question_id=a.question_id, option_id=a.option_id, correct=a.correct,
            explanation=a.explanation, source_url=a.source_url, points_pending=a.points_pending, acknowledged=a.acknowledged };
        private static RaidQuestion Question(int index)
        {
            var ids=new[] {"lights","fridge","cooling"};
            var prompts=new[] {"自分で操作してよい、誰も使っていない部屋の照明。どうする？", "冷蔵庫から物を取り出すとき、省エネにつながるのは？", "暑い日に冷房を使っています。体調と室温に無理がない範囲で、省エネにつながる調整は？"};
            var options=new[] {
                new[] { new RaidOption{id="off",text="不要なら消す"},new RaidOption{id="on",text="明るさを最大にする"},new RaidOption{id="wait",text="誰もいなくてもつけておく"} },
                new[] { new RaidOption{id="long",text="扉を開けてからゆっくり考える"},new RaidOption{id="short",text="取り出すものを決めて、手早く閉める"},new RaidOption{id="open",text="次に使うまで開けておく"} },
                new[] { new RaidOption{id="lower",text="設定温度を下げる"},new RaidOption{id="raise",text="設定温度を少し上げる"},new RaidOption{id="window",text="冷房を強めて窓も開ける"} }
            };
            return new RaidQuestion { id=ids[index], prompt=prompts[index], options=options[index] };
        }
        private static RaidClientException Error(string code,string message) => new RaidClientException(code,message);
    }
}
