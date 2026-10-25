using System;
using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// 生産の島のペンギンの動き。値の名前は Animator のステート名 (PenguinProduction.controller) と同じにする。
    /// </summary>
    public enum PenguinActivity
    {
        CarryWalk,
        WorkLoop,
        Idle,
        Cheer,
    }

    /// <summary>
    /// <see cref="PenguinActivity"/> と Animator のステートの対応。<c>Play("CarryWalk")</c> のように文字列で書かず、
    /// ここのハッシュを使う (毎回のハッシュ計算も省ける)。
    /// コントローラーにすべてのステートがあることを EditMode の PenguinAnimatorStatesTests が確かめる。
    /// </summary>
    public static class PenguinAnimatorStates
    {
        /// <summary>ステートを持つコントローラー (ProductionIslandAssetBuilder が作る)。</summary>
        public const string ControllerPath = "Assets/ProductionIsland/Animations/PenguinProduction.controller";

        private static readonly int[] Hashes = Array.ConvertAll(
            (PenguinActivity[])Enum.GetValues(typeof(PenguinActivity)), a => Animator.StringToHash(NameOf(a)));

        /// <summary>Animator のステート名。</summary>
        public static string NameOf(PenguinActivity activity) => activity.ToString();

        /// <summary><see cref="Animator.Play(int, int, float)"/> と <see cref="AnimatorStateInfo.shortNameHash"/> に使うハッシュ。</summary>
        public static int HashOf(PenguinActivity activity) => Hashes[(int)activity];

        /// <summary>レイヤー <paramref name="layer"/> が今 <paramref name="activity"/> のステートにいるか。</summary>
        public static bool IsIn(Animator animator, PenguinActivity activity, int layer = 0) =>
            animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == HashOf(activity);
    }
}
