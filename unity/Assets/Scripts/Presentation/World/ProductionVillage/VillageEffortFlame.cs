using UnityEngine;

namespace ProjectPenguin.Presentation.World.ProductionVillage
{
    /// <summary>Shows configured effort above a facility; waiting and paused markers remain readable without flicker.</summary>
    public sealed class VillageEffortFlame
    {
        public readonly GameObject Root;
        public readonly VillageEffortFlameGraphic Graphic;
        private readonly float _roofHeight;

        public VillageEffortFlame(Transform parent, GameObject model, Camera camera, int id)
        {
            _roofHeight = .65f;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                _roofHeight = Mathf.Max(_roofHeight, renderer.bounds.max.y - model.transform.position.y);
            Root = new GameObject("VillageEffortFlame" + id, typeof(RectTransform), typeof(Canvas));
            Root.transform.SetParent(parent, false);
            var canvas = Root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            var rt = (RectTransform)Root.transform;
            rt.sizeDelta = new Vector2(1.4f, .67f); rt.pivot = new Vector2(.5f, 0);
            var graphicGo = new GameObject("Flames", typeof(RectTransform), typeof(CanvasRenderer)); graphicGo.transform.SetParent(Root.transform, false);
            var graphicRect = (RectTransform)graphicGo.transform;
            graphicRect.anchorMin = Vector2.zero; graphicRect.anchorMax = Vector2.one;
            graphicRect.offsetMin = Vector2.zero; graphicRect.offsetMax = Vector2.zero;
            Graphic = graphicGo.AddComponent<VillageEffortFlameGraphic>(); Graphic.raycastTarget = false;
        }

        public void Render(Vector3 facilityPosition, int effort, bool working, Camera camera, float seconds)
        {
            Root.SetActive(effort > 0);
            if (effort == 0) return;
            Root.transform.position = facilityPosition + Vector3.up * (_roofHeight + .18f);
            if (camera != null) Root.transform.rotation = camera.transform.rotation;
            Graphic.Render(effort, working, seconds);
        }
    }
}
