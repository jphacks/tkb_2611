using System;
using System.Collections.Generic;

namespace ProjectPenguin.Domain.Raid
{
    /// <summary>Server progress is authoritative. This class only manages display and milestone delivery.</summary>
    public sealed class RaidSession
    {
        private readonly string _eventId;
        private int _seen;
        private readonly Queue<int> _newMilestones = new Queue<int>();
        public RaidSession(string eventId, int seenMilestones = 0)
        {
            if (!RaidLaunch.ValidEventId(eventId)) throw new ArgumentException(nameof(eventId));
            _eventId = eventId;
            _seen = seenMilestones & 15;
        }
        public RaidProgress Progress { get; private set; }
        public int SeenMilestones => _seen;
        public bool ApplyProgress(RaidProgress progress)
        {
            if (progress == null || progress.schema_version != 1 || progress.event_id != _eventId ||
                string.IsNullOrEmpty(progress.content_version) || progress.target_points <= 0 ||
                progress.total_points < 0 || progress.revision < 0 ||
                (progress.status != "active" && progress.status != "scheduled" && progress.status != "ended")) return false;
            if (Progress != null && (progress.content_version != Progress.content_version ||
                progress.revision <= Progress.revision || progress.total_points < Progress.total_points)) return false;
            var first = Progress == null;
            for (var i = 0; i < 4; i++)
            {
                var bit = 1 << i;
                if (progress.Ratio < (i + 1) / 4.0 || (_seen & bit) != 0) continue;
                _seen |= bit;
                if (!first) _newMilestones.Enqueue((i + 1) * 25);
            }
            Progress = progress;
            return true;
        }
        public int[] TakeNewMilestones()
        {
            var values = _newMilestones.ToArray();
            _newMilestones.Clear();
            return values;
        }
    }

    /// <summary>Local camera geometry shared by the scenery and projection checks.</summary>
    public static class RaidRescueFraming
    {
        public const double CameraY = 5, CameraZ = -13.5, LookY = 1.5, LookZ = 1.5;
        public const double FieldOfView = 44, StrandedZ = 4.9, SafeZ = -4.4;

        public static double LandscapeHeight(double viewport, double overhead, double fraction, double sheet, double maximum)
        {
            if (double.IsNaN(viewport) || double.IsInfinity(viewport) || viewport <= 0 ||
                double.IsNaN(overhead) || double.IsInfinity(overhead)) return 0;
            return Math.Max(0, Math.Min(Math.Min(viewport * fraction, maximum), viewport - Math.Max(0, overhead) - sheet));
        }
    }

    /// <summary>Presentation timing only. Never changes shared points or decides server success.</summary>
    public sealed class RaidRescueVisualState
    {
        private string _eventId, _contentVersion;
        private long _revision = -1, _total, _target;
        private double _targetRatio, _arrivalDelay;
        private bool _reducedMotion;

        public double BridgeRatio { get; private set; }
        public double ReturnProgress { get; private set; }
        public double ContributionPulse { get; private set; }
        public bool ReducedMotion
        {
            get => _reducedMotion;
            set { _reducedMotion = value; if (value) Snap(); }
        }

        public bool Apply(RaidProgress progress, bool animate)
        {
            if (progress == null || progress.schema_version != 1 ||
                string.IsNullOrEmpty(progress.event_id) || string.IsNullOrEmpty(progress.content_version) ||
                progress.revision < 0 || progress.total_points < 0 || progress.target_points <= 0 ||
                (progress.status != "active" && progress.status != "scheduled" && progress.status != "ended")) return false;
            var first = _revision < 0;
            if (!first && (progress.event_id != _eventId || progress.content_version != _contentVersion ||
                progress.revision <= _revision || progress.total_points < _total || progress.target_points != _target)) return false;
            var previous = _targetRatio;
            _eventId = progress.event_id; _contentVersion = progress.content_version;
            _revision = progress.revision; _total = progress.total_points; _target = progress.target_points;
            _targetRatio = progress.Ratio;
            if (first || !animate || ReducedMotion) Snap();
            else if (_targetRatio > previous)
            {
                ContributionPulse = 1;
                if (previous < 1 && _targetRatio >= 1) _arrivalDelay = 0.45;
            }
            return true;
        }

        public void Advance(double seconds, bool allowReturn = true)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0 || ReducedMotion) return;
            var dt = Math.Min(seconds, 0.1); // A suspended tab must not skip the whole return on its first frame.
            BridgeRatio = Math.Min(_targetRatio, BridgeRatio + dt * 0.55);
            ContributionPulse = Math.Max(0, ContributionPulse - dt * 1.5);
            if (BridgeRatio < 1 || !allowReturn) return;
            if (_arrivalDelay > 0) { _arrivalDelay = Math.Max(0, _arrivalDelay - dt); return; }
            ReturnProgress = Math.Min(1, ReturnProgress + dt / 4);
        }

        public double SegmentFill(int index, int count)
        {
            if (count <= 0 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            return Math.Max(0, Math.Min(1, BridgeRatio * count - index));
        }

        public void Reset()
        {
            _eventId = _contentVersion = null; _revision = -1; _total = _target = 0;
            _targetRatio = _arrivalDelay = BridgeRatio = ReturnProgress = ContributionPulse = 0;
        }

        private void Snap()
        {
            BridgeRatio = _targetRatio; ReturnProgress = _targetRatio >= 1 ? 1 : 0;
            ContributionPulse = _arrivalDelay = 0;
        }
    }
    /// <summary>Wire validation shared by recovery, polling and command responses.</summary>
    public static class ClassroomState
    {
        public static void NormalizeSaved(ClassroomParticipation saved)
        {
            if(saved==null) return;
            var next=saved.pending_next;
            if(next!=null && string.IsNullOrEmpty(next.request_id) && string.IsNullOrEmpty(next.content_version)) saved.pending_next=null;
            var answer=saved.pending_answer;
            if(answer!=null && string.IsNullOrEmpty(answer.request_id) && string.IsNullOrEmpty(answer.attempt_id) &&
                string.IsNullOrEmpty(answer.content_version) && string.IsNullOrEmpty(answer.option_id)) saved.pending_answer=null;
        }
        private static bool Utc(string s) => DateTimeOffset.TryParse(s,System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,out var d) && d.Offset==TimeSpan.Zero;
        public static bool Valid(ClassroomProgress p,string eventId)
        {
            if(p==null || p.schema_version!=2 || p.event_id!=eventId || !RaidLaunch.ValidEventId(p.content_version) ||
                string.IsNullOrWhiteSpace(p.title) || p.revision<0 || p.target_points<1 || p.target_points>100000 ||
                p.total_points<0 || p.total_points>p.target_points || !Utc(p.ends_at)) return false;
            if(p.phase=="waiting") return p.starts_at==null && p.ended_at==null && p.end_reason==null && p.total_points==0;
            if(p.phase=="running") return Utc(p.starts_at) && p.ended_at==null && p.end_reason==null && p.total_points<p.target_points;
            if(p.phase=="succeeded") return Utc(p.starts_at) && Utc(p.ended_at) && p.end_reason=="target" && p.total_points==p.target_points;
            return p.phase=="ended" && Utc(p.ended_at) && (p.end_reason=="manual" || p.end_reason=="timeout") && p.total_points<p.target_points;
        }
        public static bool Valid(ClassroomPersonalState p,ClassroomProgress progress)
        {
            if(p==null || p.schema_version!=2 || p.event_id!=progress.event_id || p.content_version!=progress.content_version ||
                p.personal_version<0 || p.answered_count<0 || p.personal_points<0 || p.personal_points>progress.total_points ||
                p.personal_points>p.answered_count*2) return false;
            if(p.phase=="idle" || p.phase=="finished") return p.question==null && p.pending_answer==null &&
                (p.phase=="finished"?progress.Terminal:!progress.Terminal);
            if(p.phase=="answering")
            {
                var q=p.question;
                if(p.pending_answer!=null || q==null || !RaidLaunch.ValidGeneratedId(q.attempt_id) || q.attempt_seq!=p.answered_count+1 ||
                    !RaidLaunch.ValidEventId(q.question_id) || string.IsNullOrWhiteSpace(q.prompt) || q.options==null || q.options.Length!=3) return false;
                var ids=new HashSet<string>();
                foreach(var o in q.options) if(o==null || !RaidLaunch.ValidEventId(o.id) || string.IsNullOrWhiteSpace(o.text) || !ids.Add(o.id)) return false;
                return true;
            }
            var a=p.pending_answer;
            return p.phase=="explanation" && p.question==null && a!=null && RaidLaunch.ValidGeneratedId(a.attempt_id) &&
                a.answer_id==a.attempt_id && a.attempt_seq==p.answered_count && a.attempt_seq>0 &&
                !string.IsNullOrWhiteSpace(a.explanation) && a.points_pending==(a.correct?2:1);
        }
        public static bool CanAdvance(ClassroomProgress old,ClassroomProgress next) => old==null ||
            (next.content_version==old.content_version && next.target_points==old.target_points && next.ends_at==old.ends_at &&
             next.revision>=old.revision && next.total_points>=old.total_points &&
             (old.phase!="running" || next.phase!="waiting") && (!old.Terminal || next.phase==old.phase && next.end_reason==old.end_reason &&
                 next.revision==old.revision && next.total_points==old.total_points && next.ended_at==old.ended_at));
        public static bool SamePersonal(ClassroomPersonalState a,ClassroomPersonalState b)
        {
            if(a.event_id!=b.event_id || a.content_version!=b.content_version || a.personal_version!=b.personal_version ||
                a.answered_count!=b.answered_count || a.personal_points!=b.personal_points) return false;
            var phaseSame=a.phase==b.phase || (a.phase=="idle" || a.phase=="finished") && (b.phase=="idle" || b.phase=="finished");
            if(!phaseSame || (a.question==null)!=(b.question==null) || (a.pending_answer==null)!=(b.pending_answer==null)) return false;
            var q=a.question; var r=b.question;
            if(q!=null)
            {
                if(q.attempt_id!=r.attempt_id || q.attempt_seq!=r.attempt_seq || q.question_id!=r.question_id || q.prompt!=r.prompt || q.options.Length!=r.options.Length) return false;
                for(var i=0;i<q.options.Length;i++) if(q.options[i].id!=r.options[i].id || q.options[i].text!=r.options[i].text) return false;
            }
            var x=a.pending_answer; var y=b.pending_answer;
            return x==null || x.answer_id==y.answer_id && x.attempt_id==y.attempt_id && x.attempt_seq==y.attempt_seq &&
                x.correct==y.correct && x.explanation==y.explanation && x.source_url==y.source_url && x.points_pending==y.points_pending;
        }
        public static bool AllowedSource(string source) => Uri.TryCreate(source,UriKind.Absolute,out var uri) &&
            uri.Scheme=="https" && uri.UserInfo=="" && uri.IsDefaultPort &&
            (uri.Host=="www.enecho.meti.go.jp" || uri.Host=="www.env.go.jp" || uri.Host=="ondankataisaku.env.go.jp");
    }
}
