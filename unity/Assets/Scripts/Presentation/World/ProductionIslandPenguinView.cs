using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    public sealed class ProductionIslandPenguinView : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private GameObject _carriedItem;

        public void Configure(Animator animator, GameObject carriedItem)
        {
            _animator = animator;
            _carriedItem = carriedItem;
        }

        public void SetActivity(PenguinActivity activity)
        {
            if (_animator != null) _animator.Play(PenguinAnimatorStates.HashOf(activity), 0, 0f);
            if (_carriedItem != null) _carriedItem.SetActive(activity == PenguinActivity.CarryWalk);
        }

        private void Update()
        {
            if (_animator == null || _carriedItem == null) return;
            var carrying = PenguinAnimatorStates.IsIn(_animator, PenguinActivity.CarryWalk);
            if (_carriedItem.activeSelf != carrying) _carriedItem.SetActive(carrying);
        }
    }
}
