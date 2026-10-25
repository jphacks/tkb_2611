using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Factory;
using UnityEngine;

namespace ProjectPenguin.FactoryGame
{
    internal sealed class FactoryGameWorld
    {
        internal const float CellSize = 1.6f;
        private readonly Transform _root;
        private readonly Dictionary<string, GameObject> _prefabs;
        private readonly Dictionary<int, GameObject> _nodes = new Dictionary<int, GameObject>();
        private readonly Dictionary<(int, int), GameObject> _tiles = new Dictionary<(int, int), GameObject>();
        private readonly List<GameObject> _cargo = new List<GameObject>();
        private readonly Material _ice, _selected, _parcel, _boundary;
        private readonly Mesh _boundaries;
        private int _blockCount;

        internal FactoryGameWorld(Transform parent, Camera camera, FactoryModel[] models)
        {
            _root = new GameObject("FactoryWorld").transform; _root.SetParent(parent, false);
            _prefabs = models?.Where(m => m.prefab != null).ToDictionary(m => m.recipe, m => m.prefab) ?? new Dictionary<string, GameObject>();
            _ice = Material(new Color(.69f, .9f, .96f)); _selected = Material(new Color(.25f, .82f, .66f)); _parcel = Material(new Color(1, .77f, .3f));
            _boundary = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default")) { color = new Color(1, .77f, .3f) };
            _boundaries = new Mesh { name = "FactoryBlockBoundaries" };
            var boundary = new GameObject("BlockBoundaries", typeof(MeshFilter), typeof(MeshRenderer)); boundary.transform.SetParent(_root, false);
            boundary.GetComponent<MeshFilter>().sharedMesh = _boundaries;
            var renderer = boundary.GetComponent<MeshRenderer>(); renderer.sharedMaterial = _boundary;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            camera.orthographic = true; camera.orthographicSize = 3;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.rect = new Rect(0, .43f, 1, .32f);
            camera.transform.position = new Vector3(.8f, 12, -6);
            camera.transform.LookAt(new Vector3(.8f, 0, .8f));
            camera.backgroundColor = new Color(.05f, .16f, .23f);
        }

        private static Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
        }

        internal void Render(FactoryState state, bool overview, int selectedX, int selectedY)
        {
            UpdateBoundaries(state);
            foreach (var b in state.blocks)
            {
                for (var dy = 0; dy < 2; dy++) for (var dx = 0; dx < 2; dx++)
                {
                    var key = (b.x * 2 + dx, b.y * 2 + dy);
                    if (!_tiles.TryGetValue(key, out var tile))
                    {
                        tile = GameObject.CreatePrimitive(PrimitiveType.Cube); tile.name = $"Land_{key.Item1}_{key.Item2}";
                        tile.transform.SetParent(_root, false); tile.transform.localScale = new Vector3(1.52f, .12f, 1.52f);
                        tile.transform.position = new Vector3(key.Item1 * CellSize, -.1f, key.Item2 * CellSize);
                        _tiles[key] = tile;
                    }
                    tile.GetComponent<Renderer>().sharedMaterial = key == (selectedX, selectedY) ? _selected : _ice;
                }
            }
            foreach (var n in state.facilities)
            {
                if (!_nodes.TryGetValue(n.id, out var model))
                {
                    model = _prefabs.TryGetValue(n.recipe, out var prefab) ? Object.Instantiate(prefab, _root) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                    model.name = "Factory_" + n.id + "_" + n.recipe;
                    model.transform.SetParent(_root, false);
                    foreach (var behavior in model.GetComponentsInChildren<MonoBehaviour>(true)) behavior.enabled = false;
                    foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    Fit(model, n.upper ? 2.7f : 1.3f);
                    _nodes[n.id] = model;
                }
                var hasUpper = state.facilities.Any(u => u.upper && !u.retired && u.x == Mathf.FloorToInt(n.x / 2f) && u.y == Mathf.FloorToInt(n.y / 2f));
                model.SetActive(n.upper ? overview && !n.retired : !overview || !hasUpper);
                model.transform.position = new Vector3((n.upper ? n.x * 2 + .5f : n.x) * CellSize, .02f, (n.upper ? n.y * 2 + .5f : n.y) * CellSize);
            }
            while (_cargo.Count < state.cargo.Count)
            {
                var parcel = GameObject.CreatePrimitive(PrimitiveType.Sphere); parcel.name = "Delivery"; parcel.transform.SetParent(_root, false);
                parcel.transform.localScale = Vector3.one * .15f; parcel.GetComponent<Renderer>().sharedMaterial = _parcel;
                _cargo.Add(parcel);
            }
            for (var i = 0; i < _cargo.Count; i++)
            {
                _cargo[i].SetActive(i < state.cargo.Count);
                if (i >= state.cargo.Count) continue;
                var c = state.cargo[i]; var to = state.facilities.Single(n => n.id == c.to);
                _cargo[i].transform.position = new Vector3((to.upper ? to.x * 2 + .5f : to.x) * CellSize, .6f, (to.upper ? to.y * 2 + .5f : to.y) * CellSize);
            }
        }

        private void UpdateBoundaries(FactoryState state)
        {
            if (_blockCount == state.blocks.Count) return;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            foreach (var block in state.blocks)
            {
                var x = (block.x * 2 - .5f) * CellSize; var z = (block.y * 2 - .5f) * CellSize;
                var endX = x + CellSize * 2; var endZ = z + CellSize * 2;
                BorderQuad(vertices, triangles, x, z - .02f, endX, z + .02f);
                BorderQuad(vertices, triangles, x, endZ - .02f, endX, endZ + .02f);
                BorderQuad(vertices, triangles, x - .02f, z, x + .02f, endZ);
                BorderQuad(vertices, triangles, endX - .02f, z, endX + .02f, endZ);
            }
            _boundaries.Clear(); _boundaries.SetVertices(vertices); _boundaries.SetTriangles(triangles, 0); _boundaries.RecalculateBounds();
            _blockCount = state.blocks.Count;
        }

        private static void BorderQuad(List<Vector3> vertices, List<int> triangles, float x, float z, float endX, float endZ)
        {
            var at = vertices.Count;
            vertices.AddRange(new[] { new Vector3(x, -.02f, z), new Vector3(endX, -.02f, z),
                new Vector3(endX, -.02f, endZ), new Vector3(x, -.02f, endZ) });
            triangles.AddRange(new[] { at, at + 2, at + 1, at, at + 3, at + 2 });
        }

        internal void Dispose()
        { foreach (var material in new[] { _ice, _selected, _parcel, _boundary }) Object.Destroy(material); Object.Destroy(_boundaries); }

        private static void Fit(GameObject model, float width)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds; foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
            model.transform.localScale *= width / Mathf.Max(.001f, Mathf.Max(bounds.size.x, bounds.size.z));
            // Prefabs use a ground-level root; retain their authored pivots and orientation.
        }
    }
}
