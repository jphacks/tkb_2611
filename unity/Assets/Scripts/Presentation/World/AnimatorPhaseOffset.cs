using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>
    /// 子の Animator の再生位置を、出てきたときにばらばらにずらす。
    /// 同じ足踏みのクリップを何羽もが同時に再生すると、全員の足がそろって機械のように見えるため。
    /// </summary>
    public sealed class AnimatorPhaseOffset : MonoBehaviour
    {
        [Tooltip("ずらす時間の最大 (秒)。足踏みのクリップの長さ (約 2 秒) と同じくらいにする")]
        [SerializeField] private float _maxOffsetSeconds = 2f;

        private void Start()
        {
            foreach (var animator in GetComponentsInChildren<Animator>())
            {
                if (animator.runtimeAnimatorController != null)
                {
                    animator.Update(Random.Range(0f, _maxOffsetSeconds));
                }
            }
        }
    }
}
