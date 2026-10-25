using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>出てきたものを少し弾ませる (建てた・広げた)。</summary>
    public sealed class IslandPop : MonoBehaviour
    {
        private const float Duration = 0.35f;

        private Vector3 _scale;
        private float _time;

        public static void Play(Transform target)
        {
            if (target == null)
            {
                return;
            }

            var pop = target.GetComponent<IslandPop>();
            if (pop == null)
            {
                pop = target.gameObject.AddComponent<IslandPop>();
                pop._scale = target.localScale;
            }

            pop._time = 0f;
            pop.enabled = true;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var t = Mathf.Clamp01(_time / Duration);

            // 小さく出て、少し行き過ぎてから戻る。
            var s = t < 1f ? Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(t)) : 1f;
            transform.localScale = _scale * s;
            if (t >= 1f)
            {
                enabled = false;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
