using UnityEngine;

namespace ProjectPenguin.Presentation.World
{
    public sealed class ProductionIslandPortPulse : MonoBehaviour
    {
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            var brightness = .65f + .35f * Mathf.Sin(Time.time * 6f);
            _block.SetColor(ShaderIds.BaseColor, new Color(brightness,brightness,brightness,1f));
            foreach (var renderer in _renderers) renderer.SetPropertyBlock(_block);
        }
    }
}
