using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.VillageSandbox;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.VillageUiPrototype
{
    public sealed class VillageWorld
    {
        public const float Cell=.65f;
        private readonly Transform _parent;
        private readonly GameObject[] _prefabs;
        private readonly RectTransform _canvas;
        private readonly GameObject _grid;
        private readonly Image _selection,_preview;
        private readonly Dictionary<int,GameObject> _models=new Dictionary<int,GameObject>();
        private readonly Dictionary<string,Image> _lines=new Dictionary<string,Image>();
        private readonly Dictionary<long,Image> _cargo=new Dictionary<long,Image>();
        private GameObject _ghost;
        private FacilityKind _ghostKind;
        public Vector3 HarborPosition {get;}=new Vector3(-6.8f,.1f,5.7f);
        public VillageWorld(Transform parent,Camera camera,GameObject[] prefabs,GameObject harbor)
        {
            _parent=parent;_prefabs=prefabs;
            var root=new GameObject(VillageUiNames.WorldCanvas,typeof(RectTransform),typeof(Canvas));root.transform.SetParent(parent,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;root.GetComponent<Canvas>().worldCamera=camera;
            _canvas=(RectTransform)root.transform;_canvas.sizeDelta=Vector2.one*24*Cell;_canvas.position=new Vector3(0,.035f,0);_canvas.rotation=Quaternion.Euler(90,0,0);
            _grid=new GameObject(VillageUiNames.Grid,typeof(RectTransform));_grid.transform.SetParent(_canvas,false);
            for(var i=0;i<=24;i++)
            {Rect(VillageUiNames.Grid+"X"+i,_grid.transform,new Vector2((i-12)*Cell,0),new Vector2(.012f,24*Cell),new Color(.2f,.55f,.65f,.24f));
             Rect(VillageUiNames.Grid+"Y"+i,_grid.transform,new Vector2(0,(i-12)*Cell),new Vector2(24*Cell,.012f),new Color(.2f,.55f,.65f,.24f));}
            _grid.SetActive(false);
            _selection=Rect(VillageUiNames.Selection,_canvas,Vector2.zero,Vector2.zero,new Color(.1f,.76f,.68f,.32f));_selection.enabled=false;
            _preview=Rect(VillageUiNames.Preview,_canvas,Vector2.zero,Vector2.zero,Color.green);_preview.enabled=false;
            if(harbor!=null){var port=Model(harbor,2.5f,VillageUiNames.Port);port.transform.position=HarborPosition;}
        }
        public static Vector3 Position(int x,int y,int size)=>new Vector3((x+size*.5f-12)*Cell,.08f,(y+size*.5f-12)*Cell);
        public static Vector2 Flat(Vector3 p)=>new Vector2(p.x,p.z);
        public void Render(SandboxView view,int selected)
        {
            foreach(var id in _models.Keys.Where(id=>!view.Nodes.Any(n=>n.Id==id)).ToArray()){Object.Destroy(_models[id]);_models.Remove(id);}
            foreach(var n in view.Nodes)
            {if(!_models.TryGetValue(n.Id,out var model))_models[n.Id]=model=Model(_prefabs[(int)n.Kind],n.Size*Cell*.94f,VillageUiNames.Facility+n.Id);model.transform.position=Position(n.X,n.Y,n.Size);}
            var selection=view.Nodes.FirstOrDefault(n=>n.Id==selected);_selection.enabled=selection!=null;
            if(selection!=null){_selection.rectTransform.anchoredPosition=Flat(Position(selection.X,selection.Y,selection.Size));_selection.rectTransform.sizeDelta=Vector2.one*selection.Size*Cell;}
            var keys=new HashSet<string>();
            foreach(var r in view.Routes)
            {
                var key=r.From+":"+r.To;keys.Add(key);var a=view.Nodes.First(n=>n.Id==r.From);var b=view.Nodes.First(n=>n.Id==r.To);
                if(!_lines.TryGetValue(key,out var line))_lines[key]=line=Rect(VillageUiNames.Route+key,_canvas,Vector2.zero,Vector2.zero,new Color(.06f,.48f,.55f,.55f));
                DrawLine(line,Flat(Position(a.X,a.Y,a.Size)),Flat(Position(b.X,b.Y,b.Size)),.035f);
            }
            foreach(var key in _lines.Keys.Where(k=>!keys.Contains(k)).ToArray()){Object.Destroy(_lines[key].gameObject);_lines.Remove(key);}
        }
        public void UpdateCargo(SandboxView view)
        {
            if(view==null)return;
            foreach(var id in _cargo.Keys.Where(id=>!view.Cargo.Any(c=>c.Id==id)).ToArray()){Object.Destroy(_cargo[id].gameObject);_cargo.Remove(id);}
            foreach(var c in view.Cargo)
            {
                if(!_cargo.TryGetValue(c.Id,out var icon))_cargo[c.Id]=icon=Rect(VillageUiNames.Cargo+c.Id,_canvas,Vector2.zero,Vector2.one*.15f,new Color(1,.62f,.13f));
                var a=Flat(Position(c.FromX,c.FromY,view.Nodes.First(n=>n.Id==c.From).Size));var b=Flat(Position(c.ToX,c.ToY,view.Nodes.First(n=>n.Id==c.To).Size));
                icon.rectTransform.anchoredPosition=Vector2.Lerp(a,b,Mathf.Clamp01((float)(view.AtMs-c.Start)/(c.End-c.Start)));
            }
        }
        public void Preview(FacilityKind kind,int x,int y,bool valid,bool enabled)
        {
            _grid.SetActive(enabled);_preview.enabled=enabled;
            if(!enabled){if(_ghost!=null)_ghost.SetActive(false);return;}
            var size=IslandCatalog.Get(kind).Size;
            if(_ghost==null||_ghostKind!=kind){if(_ghost!=null)Object.Destroy(_ghost);_ghostKind=kind;_ghost=Model(_prefabs[(int)kind],size*Cell*.94f,VillageUiNames.Ghost);}
            _ghost.SetActive(true);_ghost.transform.position=Position(x,y,size)+Vector3.up*.15f;
            _preview.color=valid?new Color(.15f,.85f,.5f,.65f):new Color(.95f,.25f,.22f,.65f);
            _preview.rectTransform.anchoredPosition=Flat(Position(x,y,size));_preview.rectTransform.sizeDelta=Vector2.one*size*Cell;
        }
        private static void DrawLine(Image image,Vector2 a,Vector2 b,float width)
        {var rt=image.rectTransform;var delta=b-a;rt.pivot=new Vector2(0,.5f);rt.anchoredPosition=a;rt.sizeDelta=new Vector2(delta.magnitude,width);rt.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);}
        private static Image Rect(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchoredPosition=position;rt.sizeDelta=size;var i=go.GetComponent<Image>();i.color=color;i.raycastTarget=false;return i;}
        private GameObject Model(GameObject prefab,float width,string name)
        {
            var holder=new GameObject(name);holder.transform.SetParent(_parent,false);
            if(prefab==null)return holder;
            var instance=Object.Instantiate(prefab,holder.transform);
            foreach(var b in instance.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            foreach(var a in instance.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var renderers=instance.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return holder;
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            instance.transform.localScale*=width/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z));
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            instance.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);return holder;
        }
    }
}
