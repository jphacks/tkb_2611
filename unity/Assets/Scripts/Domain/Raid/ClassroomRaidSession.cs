using System;

namespace ProjectPenguin.Domain.Raid
{
    public enum ClassroomRaidPhase { Waiting, Running, Succeeded }

    public sealed class ClassroomRaidQuestion
    {
        public string Id { get; }
        public string Prompt { get; }
        public string[] Options { get; }
        internal int CorrectIndex { get; }
        internal string Explanation { get; }
        internal ClassroomRaidQuestion(string id, string prompt, string[] options, int correctIndex, string explanation)
        { Id=id; Prompt=prompt; Options=options; CorrectIndex=correctIndex; Explanation=explanation; }
    }

    public sealed class ClassroomRaidAnswer
    {
        public string Id { get; }
        public bool Correct { get; }
        public int Points => Correct ? 2 : 1;
        public string Explanation { get; }
        internal ClassroomRaidAnswer(bool correct, string explanation)
        { Id=Guid.NewGuid().ToString("N"); Correct=correct; Explanation=explanation; }
    }

    /// <summary>Scene-only, memory-only class raid. Never implements the production fixed-three API.</summary>
    public sealed class ClassroomRaidSession
    {
        private readonly Random _random = new Random(20261020);
        private int[] _order = Array.Empty<int>();
        private int _cursor;
        private static readonly ClassroomRaidQuestion[] Questions = {
            new ClassroomRaidQuestion("lights", "自分で操作してよい、誰も使っていない部屋の照明。どうする？",
                new[] {"不要なら消す", "明るさを最大にする", "誰もいなくてもつけておく"}, 0,
                "使っていない照明は、必要がなければ消すと省エネにつながります。共有の照明は管理する人に確認しましょう。"),
            new ClassroomRaidQuestion("fridge", "冷蔵庫から物を取り出すとき、省エネにつながるのは？",
                new[] {"扉を開けてからゆっくり考える", "取り出すものを決めて、手早く閉める", "次に使うまで開けておく"}, 1,
                "取り出すものを決めて、扉を開ける時間を短くすると、冷気が逃げるのを抑えられます。"),
            new ClassroomRaidQuestion("cooling", "暑い日に冷房を使っています。体調と室温に無理がない範囲で、省エネにつながる調整は？",
                new[] {"設定温度を下げる", "設定温度を少し上げる", "冷房を強めて窓も開ける"}, 1,
                "冷房は無理のない範囲で設定温度を上げる方向が省エネにつながります。体調と室温を優先しましょう。"),
            new ClassroomRaidQuestion("laundry", "洗濯物が少しだけあります。急ぎでなく、衛生上も問題がないときは？",
                new[] {"1枚ずつ何回も洗う", "洗濯機の容量を超えて詰める", "容量を守って、まとめて洗う"}, 2,
                "洗濯機の容量を守ってまとめ洗いすると、洗う回数を減らせます。汚れや衛生、家族の予定に合わせましょう。"),
            new ClassroomRaidQuestion("filter", "エアコンのフィルターにほこりがたまっています。どうする？",
                new[] {"大人と説明書を確認して掃除する", "そのままずっと使う", "動かしたまま内部を触る"}, 0,
                "フィルターのほこりを取り除くと、空気が通りやすくなります。安全のため、大人と説明書を確認して掃除しましょう。"),
            new ClassroomRaidQuestion("shade", "冷房中、窓から強い日差しが入っています。室内への熱を減らすには？",
                new[] {"窓を開けたまま冷房を強める", "カーテンやすだれで日差しを遮る", "冷蔵庫の扉を開ける"}, 1,
                "カーテンやすだれなどで日差しを遮ると、外から入る熱を減らせます。必要な明るさと換気も大切にしましょう。"),
            new ClassroomRaidQuestion("bag", "買い物に、品物が入る手持ちのバッグを持ってきました。どうする？",
                new[] {"使えるバッグを使い、不要な袋は断る", "使わない袋も多めにもらう", "買うたびにバッグを捨てる"}, 0,
                "手持ちのバッグを使って不要な袋を断ることは、ごみを出す量を減らす工夫です。今ある物を大切に使いましょう。"),
            new ClassroomRaidQuestion("sorting", "飲み終わった容器を分別したいけれど、出し方が分かりません。どうする？",
                new[] {"全部同じごみ箱に入れる", "自分の予想だけで決める", "その地域や学校の分別ルールを確認する"}, 2,
                "分別の出し方は地域や施設によって違います。住んでいる地域や学校のルールを確認することが大切です。"),
            new ClassroomRaidQuestion("packaging", "同じ用途に使えて必要な品質も同じ商品。ごみを減らす選び方は？",
                new[] {"包みが多い方を必ず選ぶ", "必要以上の包装が少ない方を選ぶ", "包装だけのために余分に買う"}, 1,
                "必要以上の包装が少ない商品を選ぶことは、ごみを出す量を減らす工夫です。中身の保護や衛生に必要な包装は大切にしましょう。"),
            new ClassroomRaidQuestion("bottle", "水筒を使える学校で、水筒を持っています。飲み物の容器のごみを減らすには？",
                new[] {"清潔にした水筒を繰り返し使う", "使うたびに水筒を捨てる", "飲まない飲み物も買う"}, 0,
                "マイボトルを繰り返し使うことは、使い捨て容器を減らす取り組みです。清潔に保ち、学校のルールに従いましょう。"),
            new ClassroomRaidQuestion("portion", "食事を注文します。食べ残しを減らすには？",
                new[] {"食べられない量も注文する", "自分が無理なく食べきれる量を選ぶ", "量を確認せず必ず大盛りにする"}, 1,
                "自分が食べきれる量を選ぶことは、食品ロスを減らす工夫です。量が分からないときは、お店の人に相談しましょう。"),
            new ClassroomRaidQuestion("shopping", "食べ物の買いすぎを防ぐため、買い物の前にできることは？",
                new[] {"家にある物を確認せず買う", "食べる予定のない物も多く買う", "家にある食材を確認して、必要な分を考える"}, 2,
                "家にある食材を確認し、必要な分を考えて買うと、買いすぎや使い忘れを減らせます。家族と相談して取り組みましょう。")
        };
        public ClassroomRaidPhase Phase { get; private set; }
        public int Participants { get; private set; }
        public int Target { get; private set; }
        public int TotalPoints { get; private set; }
        public int PersonalPoints { get; private set; }
        public int AnsweredCount { get; private set; }
        public long Revision { get; private set; }
        public bool IsWatching { get; private set; }
        public ClassroomRaidQuestion CurrentQuestion { get; private set; }
        public ClassroomRaidAnswer PendingAnswer { get; private set; }
        public int QuestionCount => Questions.Length;
        public void Reset(int participants, int target)
        {
            if (participants<1 || participants>300 || target<1 || target>100000)
                throw new ArgumentOutOfRangeException("Use 1–300 participants and 1–100000 points.");
            Participants=participants; Target=target; TotalPoints=0; PersonalPoints=0; AnsweredCount=0;
            CurrentQuestion=null; PendingAnswer=null; IsWatching=false;
            Phase=ClassroomRaidPhase.Waiting;
            _order=new int[Questions.Length];
            for(var i=0;i<_order.Length;i++) _order[i]=i;
            _cursor=Questions.Length; Revision++;
        }
        public bool Start()
        {
            if (Phase!=ClassroomRaidPhase.Waiting || Target<1) return false;
            Phase=ClassroomRaidPhase.Running; Revision++; return true;
        }
        public bool BeginQuestion()
        {
            if (Phase!=ClassroomRaidPhase.Running || CurrentQuestion!=null || PendingAnswer!=null) return false;
            if (_cursor==Questions.Length)
            {
                var previous=(int[])_order.Clone();
                for(var i=_order.Length-1;i>0;i--)
                { var j=_random.Next(i+1); var temp=_order[j]; _order[j]=_order[i]; _order[i]=temp; }
                var same=true;
                for(var i=0;i<_order.Length;i++) same&=_order[i]==previous[i];
                if(same) { var first=_order[0]; _order[0]=_order[1]; _order[1]=first; }
                // Do not show the last question of a round again immediately.
                if(AnsweredCount>0 && _order[0]==previous[previous.Length-1])
                { var first=_order[0]; _order[0]=_order[1]; _order[1]=first; }
                _cursor=0;
            }
            CurrentQuestion=Questions[_order[_cursor++]]; IsWatching=false; return true;
        }
        public bool Answer(int option)
        {
            // A question opened before success may still be answered and explained.
            if (CurrentQuestion==null || PendingAnswer!=null || option<0 || option>=CurrentQuestion.Options.Length) return false;
            PendingAnswer=new ClassroomRaidAnswer(option==CurrentQuestion.CorrectIndex, CurrentQuestion.Explanation);
            AnsweredCount++; return true;
        }
        public int Confirm(string answerId)
        {
            if (PendingAnswer==null || PendingAnswer.Id!=answerId) return 0;
            var awarded=AddPoints(PendingAnswer.Points); PersonalPoints+=awarded;
            PendingAnswer=null; CurrentQuestion=null; return awarded;
        }
        public int AddSimulatedPoints(int points) => points<=0 ? 0 : AddPoints(points);
        private int AddPoints(int points)
        {
            if (Phase!=ClassroomRaidPhase.Running) return 0;
            var awarded=Math.Min(points,Target-TotalPoints); TotalPoints+=awarded; Revision++;
            if (TotalPoints==Target) Phase=ClassroomRaidPhase.Succeeded;
            return awarded;
        }
        public void Watch() { IsWatching=true; }
        public void Resume() { IsWatching=false; }
    }
}
