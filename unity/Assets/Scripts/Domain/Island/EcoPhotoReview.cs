using System;
using System.Collections.Generic;

namespace ProjectPenguin.Domain.Island
{
    /// <summary>
    /// 判定サーバーの検証層 (#112) が写真をどう扱うと決めたか (api/openapi.yaml の <c>decision</c>)。
    /// 知らない値・項目が無いときは <see cref="Accept"/> にする (契約の前方互換の規則)。
    /// </summary>
    public enum EcoPhotoDecision
    {
        /// <summary>写真の表の量をそのまま渡す。</summary>
        Accept,

        /// <summary>見分けの確かさが中くらい。量を <see cref="IslandParameters.PhotoReducedRate"/> 倍にして渡す。</summary>
        Reduce,

        /// <summary>資源も記録も出さず、撮り直してもらう。</summary>
        Retake,

        /// <summary>資源も記録も出さない (画面を撮った写真・文字だけの写真など)。</summary>
        Reject,
    }

    /// <summary>結果の画面の「確かめたこと」の 1 行。中身はサーバーが決め、クライアントは並べるだけ。</summary>
    public sealed class EcoPhotoCheck
    {
        public EcoPhotoCheck(string id, string label, bool passed, string detail = null)
        {
            Id = id ?? "";
            Label = label ?? "";
            Passed = passed;
            Detail = detail ?? "";
        }

        public string Id { get; }
        public string Label { get; }
        public bool Passed { get; }

        /// <summary>「マイボトル」「高い」など。無ければ空。</summary>
        public string Detail { get; }
    }

    /// <summary>
    /// 写真の判定に付く検証の結果。モックと 2026-10-19 以前の形の応答では <see cref="None"/> (そのまま受け取る)。
    /// </summary>
    public sealed class EcoPhotoReview
    {
        public static readonly EcoPhotoReview None = new EcoPhotoReview(EcoPhotoDecision.Accept, null, null);

        public EcoPhotoReview(EcoPhotoDecision decision, IReadOnlyList<EcoPhotoCheck> checks, string retakeHint)
        {
            Decision = decision;
            Checks = checks ?? Array.Empty<EcoPhotoCheck>();
            RetakeHint = retakeHint ?? "";
        }

        public EcoPhotoDecision Decision { get; }
        public IReadOnlyList<EcoPhotoCheck> Checks { get; }

        /// <summary><see cref="EcoPhotoDecision.Retake"/> のときの案内。無ければ空。</summary>
        public string RetakeHint { get; }

        /// <summary>記録 (氷) と資源を受け取る写真か。撮り直し・受け付けない写真は受け取らない。</summary>
        public bool Counts => Decision == EcoPhotoDecision.Accept || Decision == EcoPhotoDecision.Reduce;
    }
}
