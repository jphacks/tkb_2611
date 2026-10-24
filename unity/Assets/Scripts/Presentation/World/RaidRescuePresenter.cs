using System;
using System.Collections.Generic;
using ProjectPenguin.Domain.Raid;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.World
{
    /// <summary>Isolated rescue diorama, rendered into the raid UI without changing the player's island.</summary>
    public sealed class RaidRescuePresenter : MonoBehaviour
    {
        [SerializeField] private GameObject[] _rescuedStages = Array.Empty<GameObject>();
        [SerializeField] private ParticleSystem _celebration;
        [SerializeField] private Camera _previewCamera;
        [SerializeField] private Transform _penguin;
        [SerializeField] private Vector3 _strandedPosition;
        [SerializeField] private Vector3 _safePosition;
        private RenderTexture _texture;
        private Image _image;
        private Vector3 _destination;
        private long _revision = -1;
        private readonly RaidRescueVisualState _visual = new RaidRescueVisualState();
        private RaidSnowMountainStage _mountain;
        private bool _visible, _reading;

        public void Bind(Image image)
        {
            ReleaseImage();
            _image = image;
            if (_image == null || _previewCamera == null) return;
            if (_mountain == null && _penguin != null)
            {
                Material template = null;
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.transform.IsChildOf(_penguin)) continue;
                    if (template == null && renderer.sharedMaterial != null) template = renderer.sharedMaterial;
                }
                if (template != null)
                {
                    foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                        if (!renderer.transform.IsChildOf(_penguin)) renderer.enabled = false;
                    _mountain = new RaidSnowMountainStage(transform, _penguin, _previewCamera, template);
                }
            }
            _image.RegisterCallback<GeometryChangedEvent>(OnImageGeometry);
            ResizeTexture(1024, 768);
            _destination = _strandedPosition;
            if (_mountain != null) _mountain.Draw(_visual, 0, _reading);
            else if (_penguin != null) _penguin.localPosition = _destination;
            SetVisible(true);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_previewCamera != null) _previewCamera.enabled = visible && _texture != null;
        }

        public void SetReading(bool reading) => _reading = reading;
        public void SetReducedMotion(bool reduced) { _visual.ReducedMotion = reduced; _mountain?.Draw(_visual,0,_reading); }
        public void ResetProgress() { _visual.Reset(); _revision = -1; _mountain?.Draw(_visual,0,_reading); }

        public void Apply(RaidProgress progress, bool animate)
        {
            if (_mountain != null)
            {
                if (_visual.Apply(progress, animate)) _mountain.Draw(_visual, 0, _reading);
                return;
            }
            if (progress==null || progress.revision<_revision) return;
            var changed=progress.revision>_revision;
            _revision=progress.revision;
            for(var i=0;i<_rescuedStages.Length;i++)
                if (_rescuedStages[i]!=null) _rescuedStages[i].SetActive(progress.Ratio>=(i+1)/4.0);
            _destination = progress.Ratio >= 1 ? _safePosition : _strandedPosition;
            if (_penguin != null && !animate) _penguin.localPosition = _destination;
            if (animate && changed && _celebration!=null) _celebration.Play();
        }

        private void Update()
        {
            if (!_visible || _previewCamera == null || !_previewCamera.enabled || _penguin == null) return;
            if (_mountain != null)
            {
                _visual.Advance(Time.unscaledDeltaTime,!_reading);
                _mountain.Draw(_visual,Time.unscaledDeltaTime,_reading);
                return;
            }
            _penguin.localPosition = Vector3.MoveTowards(_penguin.localPosition, _destination, Time.unscaledDeltaTime * 2.5f);
        }

        private void OnImageGeometry(GeometryChangedEvent evt)
        {
            var width = evt.newRect.width; var height = evt.newRect.height;
            if (width <= 0 || height <= 0 || float.IsNaN(width) || float.IsNaN(height)) return;
            var pixels = Mathf.Clamp(Mathf.CeilToInt(width), 320, 1024);
            ResizeTexture(pixels, Mathf.Clamp(Mathf.CeilToInt(pixels * height / width), 256, 1024));
        }

        private void ResizeTexture(int width, int height)
        {
            if (_texture != null && _texture.width == width && _texture.height == height) return;
            if (_texture != null) { _previewCamera.targetTexture = null; _texture.Release(); Destroy(_texture); }
            _texture = new RenderTexture(width, height, 24) { name = "Raid mountain preview", hideFlags = HideFlags.DontSave };
            _texture.Create(); _previewCamera.targetTexture = _texture; _previewCamera.aspect = (float)width / height;
            _image.image = _texture; _image.scaleMode = ScaleMode.ScaleToFit;
        }

        private void ReleaseImage()
        {
            if (_image != null) { _image.UnregisterCallback<GeometryChangedEvent>(OnImageGeometry); _image.image = null; }
            if (_previewCamera != null) { _previewCamera.enabled = false; _previewCamera.targetTexture = null; }
            if (_texture != null) { _texture.Release(); Destroy(_texture); _texture = null; }
        }

        private void OnDestroy()
        {
            ReleaseImage(); _mountain?.Dispose();
        }
    }

    /// <summary>Owned runtime scenery. Uses the prefab's imported material; no new shader or asset lookup.</summary>
    internal sealed class RaidSnowMountainStage : IDisposable
    {
        private const int SegmentCount = 16, SnowCount = 120;
        private const float BridgeStart = -3.2f, BridgeEnd = 4.4f;
        private readonly GameObject _root;
        private readonly Transform[] _segments = new Transform[SegmentCount];
        private readonly Transform _penguin;
        private readonly Vector3 _penguinScale;
        private readonly List<Object> _owned = new List<Object>();
        private readonly Material _ice;
        private readonly Mesh _snowMesh;
        private readonly Vector3[] _snowVertices = new Vector3[SnowCount * 3];
        private readonly Vector3[] _snowSeeds = new Vector3[SnowCount];
        private readonly Color _iceColor = new Color(0.39f, 0.76f, 0.92f);
        private float _snowTime;
        private readonly float _footOffset;

        public RaidSnowMountainStage(Transform parent, Transform penguin, Camera camera, Material template)
        {
            _root = new GameObject("Snow mountain rescue (runtime)");
            _root.transform.SetParent(parent, false); _root.layer = parent.gameObject.layer;
            _penguin = penguin; _penguinScale = penguin.localScale;
            var snow = Material(template, new Color(0.84f, 0.92f, 0.98f));
            var rock = Material(template, new Color(0.24f, 0.36f, 0.49f));
            var distant = Material(template, new Color(0.53f, 0.64f, 0.77f));
            var crevasse = Material(template, new Color(0.09f, 0.18f, 0.3f));
            var lamp = Material(template, new Color(1f, 0.77f, 0.35f));
            _ice = Material(template, _iceColor);
            var box = BoxMesh();
            Piece("Glacial depth", box, crevasse, new Vector3(0,-3.6f,2), new Vector3(26,0.3f,28));
            Piece("Far rock ledge", box, rock, new Vector3(0,-1.2f,6.5f), new Vector3(8,2.4f,4.3f));
            Piece("Far snow ledge", box, snow, new Vector3(0,0.02f,6.4f), new Vector3(7.8f,0.2f,4.2f));
            Piece("Home rock ledge", box, rock, new Vector3(0,-1.15f,-5.7f), new Vector3(9,2.3f,5));
            Piece("Home snow ledge", box, snow, new Vector3(0,0.02f,-5.7f), new Vector3(8.8f,0.2f,5));
            for (var i=0; i<7; i++)
            {
                var x = (i-3)*3.3f;
                var height = i==3 ? 7.8f : 4.8f + (i%3)*0.85f;
                var position = new Vector3(x,0,10+(i%2)*2.5f);
                Piece("Mountain " + i, PeakMesh(3.2f,height), i%2==0?distant:rock, position, Vector3.one);
                Piece("Snow cap " + i, PeakMesh(2.5f,height*0.78f), snow,
                    position+Vector3.up*height*0.22f, Vector3.one);
            }
            var length = (BridgeEnd-BridgeStart)/SegmentCount;
            for (var i=0; i<SegmentCount; i++)
            {
                _segments[i] = Piece("Ice bridge " + i, box, _ice,
                    new Vector3(0,-0.08f,BridgeStart+i*length), Vector3.one).transform;
                Piece("Snow on bridge " + i, box, snow, new Vector3(0,0.55f,0),
                    new Vector3(0.96f,0.06f,0.96f)).transform.SetParent(_segments[i],false);
                _segments[i].gameObject.SetActive(false);
            }
            Piece("Lamp post", box, rock, new Vector3(1.2f,0.55f,-4.7f), new Vector3(0.12f,1,0.12f));
            Piece("Warm home light", box, lamp, new Vector3(1.2f,1.1f,-4.7f), new Vector3(0.32f,0.38f,0.32f));
            var random = new System.Random(20261021);
            for (var i=0; i<SnowCount; i++)
                _snowSeeds[i] = new Vector3((float)random.NextDouble()*20-10,
                    (float)random.NextDouble()*10, (float)random.NextDouble()*16-4);
            _snowMesh = new Mesh { name = "Bounded blizzard mesh", hideFlags = HideFlags.DontSave };
            _snowMesh.MarkDynamic(); _owned.Add(_snowMesh);
            var indices = new int[_snowVertices.Length]; var normals = new Vector3[_snowVertices.Length];
            for(var i=0; i<indices.Length; i++) { indices[i]=i; normals[i]=Vector3.back; }
            _snowMesh.vertices = _snowVertices; _snowMesh.triangles=indices; _snowMesh.normals=normals;
            _snowMesh.bounds = new Bounds(new Vector3(0,4,4), new Vector3(30,24,30));
            Piece("Snow in crosswind", _snowMesh, snow, Vector3.zero, Vector3.one);
            camera.orthographic = false; camera.fieldOfView = (float)RaidRescueFraming.FieldOfView;
            camera.transform.localPosition = new Vector3(0,(float)RaidRescueFraming.CameraY,(float)RaidRescueFraming.CameraZ);
            camera.transform.LookAt(parent.TransformPoint(new Vector3(0,(float)RaidRescueFraming.LookY,(float)RaidRescueFraming.LookZ)));
            camera.nearClipPlane=0.1f; camera.farClipPlane=60;
            camera.backgroundColor=new Color(0.42f,0.54f,0.68f);
            penguin.localRotation = Quaternion.Euler(0,180,0);
            var penguinRenderers=penguin.GetComponentsInChildren<Renderer>(true);
            if(penguinRenderers.Length>0)
            {
                var bounds=penguinRenderers[0].bounds;
                foreach(var renderer in penguinRenderers) bounds.Encapsulate(renderer.bounds);
                var height=parent.InverseTransformVector(Vector3.up*bounds.size.y).magnitude;
                if(height>0.001f) penguin.localScale=_penguinScale*(1.4f/height);
                bounds=penguinRenderers[0].bounds;
                foreach(var renderer in penguinRenderers) bounds.Encapsulate(renderer.bounds);
                _footOffset=parent.InverseTransformPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)).y-penguin.localPosition.y;
            }
        }

        public void Draw(RaidRescueVisualState state, float seconds, bool reading)
        {
            var length=(BridgeEnd-BridgeStart)/SegmentCount;
            for(var i=0; i<SegmentCount; i++)
            {
                var fill=(float)state.SegmentFill(i,SegmentCount);
                _segments[i].gameObject.SetActive(fill>0);
                _segments[i].localScale=new Vector3(1.7f+(i%3-1)*0.04f,0.38f,length*fill+0.012f);
                _segments[i].localPosition=new Vector3(0,-0.08f,BridgeStart+i*length+length*fill*0.5f);
            }
            var t=(float)state.ReturnProgress;
            var position=Vector3.Lerp(new Vector3(0,0.14f-_footOffset,(float)RaidRescueFraming.StrandedZ),
                new Vector3(0,0.14f-_footOffset,(float)RaidRescueFraming.SafeZ),t);
            if(!state.ReducedMotion && t>0 && t<1) position.y+=Mathf.Abs(Mathf.Sin(t*Mathf.PI*20))*0.045f;
            _penguin.localPosition=position;
            _ice.SetColor(ShaderIds.BaseColor, Color.Lerp(_iceColor, Color.white,(float)state.ContributionPulse*0.28f));
            if(!state.ReducedMotion) _snowTime+=Mathf.Min(seconds,0.1f)*(reading?0.45f:1);
            for(var i=0; i<SnowCount; i++)
            {
                var seed=_snowSeeds[i];
                var p=new Vector3(Mathf.Repeat(seed.x+_snowTime*2.1f+10,20)-10,
                    Mathf.Repeat(seed.y-_snowTime*0.8f+10,10),seed.z);
                // A clear corridor keeps both the penguin and the leading edge of the bridge readable.
                var hidden=state.ReducedMotion || (reading && i%3!=0) || (Mathf.Abs(p.x)<1.4f && p.y<3.5f);
                var size=hidden?0:(seed.z<0?0.07f:0.035f);
                _snowVertices[i*3]=p;
                _snowVertices[i*3+1]=p+new Vector3(size*1.8f,size,0);
                _snowVertices[i*3+2]=p+new Vector3(size,-size,0);
            }
            _snowMesh.vertices=_snowVertices;
        }

        private Material Material(Material source, Color color)
        {
            var material=new Material(source) {name="Rescue runtime tint",hideFlags=HideFlags.DontSave};
            material.SetColor(ShaderIds.BaseColor,color);
            if(material.HasProperty(ShaderIds.BaseMap)) material.SetTexture(ShaderIds.BaseMap,null);
            if(material.HasProperty(ShaderIds.Smoothness)) material.SetFloat(ShaderIds.Smoothness,0.15f);
            _owned.Add(material); return material;
        }

        private GameObject Piece(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var go=new GameObject(name) {layer=_root.layer}; go.transform.SetParent(_root.transform,false);
            go.transform.localPosition=position; go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return go;
        }

        private Mesh PeakMesh(float radius, float height)
        {
            var vertices=new Vector3[18]; var triangles=new int[18];
            for(var i=0;i<6;i++)
            {
                var a=i*Mathf.PI/3; var b=(i+1)*Mathf.PI/3;
                vertices[i*3]=new Vector3(0,height,0);
                vertices[i*3+1]=new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius);
                vertices[i*3+2]=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
            }
            for(var i=0;i<triangles.Length;i++) triangles[i]=i;
            return Mesh("Faceted snowy mountain",vertices,triangles);
        }

        private Mesh BoxMesh()
        {
            var corners=new[] {new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),
                new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,.5f),
                new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            var faces=new[] {0,3,2,1, 5,6,7,4, 4,7,3,0, 1,2,6,5, 3,7,6,2, 4,0,1,5};
            var vertices=new Vector3[24]; var triangles=new int[36];
            for(var f=0;f<6;f++)
            {
                for(var v=0;v<4;v++) vertices[f*4+v]=corners[faces[f*4+v]];
                var offset=f*6; var start=f*4;
                triangles[offset]=start; triangles[offset+1]=start+1; triangles[offset+2]=start+2;
                triangles[offset+3]=start; triangles[offset+4]=start+2; triangles[offset+5]=start+3;
            }
            return Mesh("Rescue ice block",vertices,triangles);
        }

        private Mesh Mesh(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh=new Mesh {name=name,hideFlags=HideFlags.DontSave,vertices=vertices,triangles=triangles};
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); _owned.Add(mesh); return mesh;
        }

        public void Dispose()
        {
            if(_penguin!=null) _penguin.localScale=_penguinScale;
            foreach(var resource in _owned) if(resource!=null) Object.Destroy(resource);
            if(_root!=null) Object.Destroy(_root);
        }
    }
}
