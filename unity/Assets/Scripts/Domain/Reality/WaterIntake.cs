using System;

namespace ProjectPenguin.Domain.Reality
{
    /// <summary>
    /// 飲んだ水の 1 件。飲みきった空のペットボトルの写真 (<see cref="WaterEmptyBottle"/>) からだけ作る。
    /// 手入力はしない (docs/decisions.md の工場ゲームの水)。資源への換算と 1 日の上限は工場の規則 (FactorySession.AcceptWaterPhoto) が決める。
    /// </summary>
    public sealed class WaterIntake
    {
        /// <summary>1 件として受け付ける上限 (ml)。これを超える値は壊れた入力とみなす。</summary>
        public const int MaxMlPerEntry = 2000;

        private WaterIntake(DateTimeOffset at, int ml)
        {
            At = at;
            Ml = ml;
        }

        /// <summary>飲み終えた時刻 (空のボトルを撮った時刻)。</summary>
        public DateTimeOffset At { get; }

        public int Ml { get; }

        /// <summary>値を確かめて作る。1〜<see cref="MaxMlPerEntry"/> ml の外なら <see cref="ArgumentOutOfRangeException"/>。</summary>
        public static WaterIntake Create(DateTimeOffset at, int ml)
        {
            if (ml < 1 || ml > MaxMlPerEntry)
            {
                throw new ArgumentOutOfRangeException(nameof(ml));
            }

            return new WaterIntake(at, ml);
        }
    }

    /// <summary>
    /// ペットボトルの写真 1 枚を backend が見積もった結果 (<c>POST /v1/water/estimate</c>) のうち、飲んだ量を決めるのに要る分。
    /// 写真そのものは持たない。同じ写真の使い回しを見分けるハッシュは呼ぶ側が端末に持つ (.claude/rules/privacy.md)。
    /// </summary>
    public sealed class WaterPhoto
    {
        public WaterPhoto(DateTimeOffset at, int capacityMl, int amountMl, bool accepted)
        {
            At = at;
            CapacityMl = Math.Max(0, capacityMl);
            AmountMl = Math.Max(0, Math.Min(CapacityMl, amountMl));
            Accepted = accepted;
        }

        /// <summary>撮った時刻。</summary>
        public DateTimeOffset At { get; }

        /// <summary>ラベルから読んだペットボトルの容量 (ml)。読めなければ 0。</summary>
        public int CapacityMl { get; }

        /// <summary>写っている水の量 (容量 × 水位)。空のボトルなら 0。</summary>
        public int AmountMl { get; }

        /// <summary>backend が量を使ってよいと答えたか (<c>decision: accept</c>)。</summary>
        public bool Accepted { get; }
    }

    /// <summary>空のボトルの写真から飲んだ量を決められなかった理由。画面はそれぞれに合った案内を出す。</summary>
    public enum WaterBottleFailure
    {
        None,

        /// <summary>ペットボトルを見分けられなかった・ラベルの容量を読めなかった (撮り直し)。</summary>
        NotAccepted,

        /// <summary>まだ水が残っている (飲みきってから撮ってもらう)。</summary>
        NotEmpty,
    }

    public readonly struct WaterBottleResult
    {
        private WaterBottleResult(WaterIntake intake, WaterBottleFailure failure)
        {
            Intake = intake;
            Failure = failure;
        }

        /// <summary>成立したときの飲んだ量。成立しなければ null。</summary>
        public WaterIntake Intake { get; }

        public WaterBottleFailure Failure { get; }

        public bool Succeeded => Intake != null;

        internal static WaterBottleResult Of(WaterIntake intake) => new WaterBottleResult(intake, WaterBottleFailure.None);

        internal static WaterBottleResult Fail(WaterBottleFailure failure) => new WaterBottleResult(null, failure);
    }

    /// <summary>
    /// 飲んだ水の量を、飲みきった空のペットボトルの写真 1 枚で決める (docs/decisions.md の工場ゲームの水)。
    /// 飲んだ量 = 容量 − 残り。残りが容量の <see cref="MaxRemainingRatio"/> を超えるボトルは「まだ飲んでいる途中」として受け付けない。
    /// 防げないもの (水を流して捨てる・空のボトルを何本も撮る) は、同じ写真を弾くことと 1 日の上限で効き目を抑える。
    /// </summary>
    public static class WaterEmptyBottle
    {
        /// <summary>空とみなす残りの割合の上限。AI の水位の見積もりは 1 割ほどぶれるので、少し余裕を持たせる。</summary>
        public const double MaxRemainingRatio = 0.15;

        public static WaterBottleResult Judge(WaterPhoto photo)
        {
            if (photo == null)
            {
                throw new ArgumentNullException(nameof(photo));
            }

            if (!photo.Accepted || photo.CapacityMl == 0)
            {
                return WaterBottleResult.Fail(WaterBottleFailure.NotAccepted);
            }

            if (photo.AmountMl > photo.CapacityMl * MaxRemainingRatio)
            {
                return WaterBottleResult.Fail(WaterBottleFailure.NotEmpty);
            }

            return WaterBottleResult.Of(WaterIntake.Create(photo.At, photo.CapacityMl - photo.AmountMl));
        }
    }
}
