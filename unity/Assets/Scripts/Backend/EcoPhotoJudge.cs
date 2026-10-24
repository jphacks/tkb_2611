using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectPenguin.Domain.Island;
using UnityEngine;

namespace ProjectPenguin.Backend
{
    /// <summary>
    /// エコ活動の写真を判定する口。本物は backend を呼ぶ <see cref="HttpEcoPhotoJudge"/>
    /// (API の形は api/openapi.yaml の <c>POST /v1/eco-photo/judge</c>)。
    /// 画像はメモリ上の JPEG のまま渡し、アプリと backend は保存しない。判定に使う Gemini API 側の入力の保持は、
    /// Google の利用規約に従う (docs/privacy-policy.md)。
    /// </summary>
    public interface IEcoPhotoJudge
    {
        Task<EcoPhotoVerdict> JudgeAsync(byte[] jpeg, CancellationToken cancellationToken);
    }

    /// <summary>
    /// 判定できなかったとき (通信の失敗など) に投げる。画面は「もう一度」と <see cref="UserMessage"/> を出す。
    /// </summary>
    public sealed class EcoPhotoJudgeException : Exception
    {
        public EcoPhotoJudgeException(string message, Exception inner = null)
            : base(message, inner)
        {
            UserMessage = (inner as BackendException)?.UserMessage ?? "うまく見分けられなかったよ。もう一度撮ってみてね";
        }

        /// <summary>画面にそのまま出せる日本語。</summary>
        public string UserMessage { get; }
    }

    /// <summary>
    /// 端末の中の判定 (backend を使わないとき・会場のデモで backend が落ちたとき)。写真のバイト列から写真の表 (<see cref="EcoSubjects"/>) のどれかを選ぶ。
    /// 同じ写真なら同じ結果になる。待ち時間を入れて、解析中の画面の流れを本番と同じにする。
    /// </summary>
    public sealed class MockEcoPhotoJudge : IEcoPhotoJudge
    {
        private static readonly (EcoSubject Subject, string Comment)[] Samples =
        {
            (EcoSubject.LightsOff, "電気を大事にしてくれたんだね。工場がよろこんでるよ"),
            (EcoSubject.AirConditioner, "ちょうどいい温度だね。ぼくらは寒いほうが好きだけど"),
            (EcoSubject.ShortShower, "お湯を沸かすガスも、お水も節約できたね"),
            (EcoSubject.LidCooking, "ふたをすると早く煮えるんだって。ガスがたまったよ"),
            (EcoSubject.WarmClothes, "もこもこだね。ぼくらの羽毛みたい"),
            (EcoSubject.SaveWater, "お水を大事にしてくれてありがとう。島が広げられるね"),
            (EcoSubject.Walk, "いっぱい歩いたね。ガスがたまったよ"),
            (EcoSubject.Bicycle, "風を切って走るの、気持ちよさそう"),
            (EcoSubject.Train, "みんなで乗ると、ガスが少しで済むんだって"),
            (EcoSubject.CleanPlate, "ぴかぴかのお皿だ！ぼくもお魚は残さないよ"),
            (EcoSubject.LocalFood, "近くでとれたものっていいね"),
            (EcoSubject.MyBottle, "マイボトル、かっこいいね"),
            (EcoSubject.EcoBag, "エコバッグで買い物、えらい！"),
            (EcoSubject.Recycle, "分けてくれたから、また使えるね"),
        };

        private readonly TimeSpan _delay;

        public MockEcoPhotoJudge(TimeSpan? delay = null)
        {
            _delay = delay ?? TimeSpan.FromSeconds(2.4);
        }

        public async Task<EcoPhotoVerdict> JudgeAsync(byte[] jpeg, CancellationToken cancellationToken)
        {
            if (jpeg == null || jpeg.Length == 0)
            {
                throw new EcoPhotoJudgeException("写真が空でした");
            }

            if (_delay > TimeSpan.Zero)
            {
                // Task.Delay は Web ビルドでは戻ってこない (タイマーのスレッドが無い、#85)。Unity の Awaitable で待つ。
                await Awaitable.WaitForSecondsAsync((float)_delay.TotalSeconds, cancellationToken);
            }

            return Pick(jpeg);
        }

        /// <summary>写真のバイト列から決まる結果 (テスト用に公開)。</summary>
        public static EcoPhotoVerdict Pick(byte[] jpeg)
        {
            // JPEG の中身をざっと混ぜた値で選ぶ。同じ写真なら同じ結果。
            unchecked
            {
                ulong h = 1469598103934665603UL;
                for (var i = 0; i < jpeg.Length; i += Math.Max(1, jpeg.Length / 4096))
                {
                    h = (h ^ jpeg[i]) * 1099511628211UL;
                }

                var s = Samples[(int)(h % (ulong)Samples.Length)];
                return new EcoPhotoVerdict(s.Subject, s.Comment, 0.8);
            }
        }
    }
}
