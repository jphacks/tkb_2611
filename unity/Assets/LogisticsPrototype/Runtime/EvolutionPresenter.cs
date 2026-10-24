using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.LogisticsPrototype;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.LogisticsPrototype
{
    /// <summary>Only renders immutable snapshots. Presentation never grants items or changes the simulation.</summary>
    internal sealed class EvolutionPresenter
    {
        private readonly Transform _parent;
        private readonly GameObject[] _prefabs;
        private readonly Dictionary<int,GameObject> _nodes=new Dictionary<int,GameObject>();
        private readonly Dictionary<string,GameObject> _formations=new Dictionary<string,GameObject>();
        private readonly Dictionary<string,Image[]> _areas=new Dictionary<string,Image[]>();
        private readonly Dictionary<string,Image> _wires=new Dictionary<string,Image>();
        private readonly Dictionary<long,Image> _cargo=new Dictionary<long,Image>();
        private readonly Dictionary<long,Image> _flightPaths=new Dictionary<long,Image>();
        private readonly RectTransform _canvas;
        private readonly Image _selection;
        internal EvolutionPresenter(Transform parent,Camera camera,GameObject[] prefabs)
        {
            _parent=parent;_prefabs=prefabs;
            var root=new GameObject("EvolutionWorldRects",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(parent,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            _canvas=(RectTransform)root.transform;_canvas.sizeDelta=new Vector2(24,24);_canvas.position=new Vector3(0,.04f,0);_canvas.rotation=Quaternion.Euler(90,0,0);
            for(var i=0;i<=24;i++)
            {
                MakeRect("GridX"+i,new Vector2(i-12,0),new Vector2(.014f,24),new Color(.2f,.55f,.65f,.22f));
                MakeRect("GridY"+i,new Vector2(0,i-12),new Vector2(24,.014f),new Color(.2f,.55f,.65f,.22f));
            }
            _selection=MakeRect("SelectedFootprint",Vector2.zero,new Vector2(2,2),new Color(.08f,.8f,.75f,.35f));_selection.enabled=false;
        }
        internal static Vector3 Center(WorkshopView n) => new Vector3(n.X+n.Footprint*.5f-12,.1f,n.Y+n.Footprint*.5f-12);
        internal void Render(EvolutionView view,int selected)
        {
            foreach(var id in _nodes.Keys.Where(id=>!view.Workshops.Any(n=>n.Id==id)).ToArray()){Object.Destroy(_nodes[id]);_nodes.Remove(id);}
            foreach(var key in _formations.Keys.Where(k=>!view.Formations.Any(s=>s.Key==k)).ToArray())
            {Object.Destroy(_formations[key]);_formations.Remove(key);foreach(var area in _areas[key])Object.Destroy(area.gameObject);_areas.Remove(key);}
            var hidden=new HashSet<int>(view.Formations.SelectMany(s=>s.Members));
            foreach(var n in view.Workshops)
            {
                if(!_nodes.TryGetValue(n.Id,out var model)) {_nodes[n.Id]=model=Model((int)n.Kind,n.Footprint*.92f,"Facility_"+n.Id);}
                model.transform.position=Center(n);model.SetActive(!hidden.Contains(n.Id));
            }
            foreach(var s in view.Formations)
            {
                if(!_formations.TryGetValue(s.Key,out var model))
                {
                    var index=s.Product==WorkshopGood.Chikuwa?3:s.Product==WorkshopGood.Kamaboko?4:5;
                    model=Model(index,s.Product==WorkshopGood.Himono?1.9f:Mathf.Min(s.Width,s.Height)*1.15f,"Formation_"+s.Key);_formations.Add(s.Key,model);
                    _areas[s.Key]=s.Members.Select(id=>MakeRect("CombinedFootprint_"+s.Key+"_"+id,Vector2.zero,new Vector2(2,2),new Color(.04f,.7f,.58f,.3f))).ToArray();
                }
                model.transform.position=new Vector3(s.X+s.Width*.5f-12,.15f,s.Y+s.Height*.5f-12);
                if(s.Product==WorkshopGood.Himono)model.transform.position=Center(view.Workshops.First(n=>n.Id==s.CoreId));
                model.transform.rotation=Quaternion.Euler(0,s.Height>s.Width?90:0,0);
                for(var i=0;i<s.Members.Count;i++)_areas[s.Key][i].rectTransform.anchoredPosition=Flat(Center(view.Workshops.First(n=>n.Id==s.Members[i])));
                // The group keeps its full footprint; the distinct factory model marks its center.
            }
            var selectedNode=view.Workshops.FirstOrDefault(n=>n.Id==selected);
            _selection.enabled=selectedNode!=null;
            if(selectedNode!=null){_selection.rectTransform.anchoredPosition=Flat(Center(selectedNode));_selection.rectTransform.sizeDelta=Vector2.one*selectedNode.Footprint;}
            var keys=new HashSet<string>();
            foreach(var r in view.Routes)
            {
                var key=r.From+":"+r.To;if(!keys.Add(key))continue;
                var from=view.Workshops.FirstOrDefault(n=>n.Id==r.From);var to=view.Workshops.FirstOrDefault(n=>n.Id==r.To);if(from==null||to==null)continue;
                if(!_wires.TryGetValue(key,out var line)) _wires[key]=line=MakeRect("RectRoute_"+key,Vector2.zero,Vector2.zero,new Color(.02f,.62f,.6f,.8f));
                var a=Flat(Center(from));var b=Flat(Center(to));var delta=b-a;
                var rt=line.rectTransform;rt.pivot=new Vector2(0,.5f);rt.anchoredPosition=a;rt.sizeDelta=new Vector2(delta.magnitude,.055f);
                rt.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            }
            foreach(var key in _wires.Keys.Where(k=>!keys.Contains(k)).ToArray()){Object.Destroy(_wires[key].gameObject);_wires.Remove(key);}
            foreach(var id in _cargo.Keys.Where(id=>!view.Cargo.Any(c=>c.Id==id)).ToArray())
            {Object.Destroy(_cargo[id].gameObject);_cargo.Remove(id);Object.Destroy(_flightPaths[id].gameObject);_flightPaths.Remove(id);}
            foreach(var c in view.Cargo)
            {
                var from=view.Workshops.FirstOrDefault(n=>n.Id==c.From);var to=view.Workshops.FirstOrDefault(n=>n.Id==c.To);if(from==null||to==null)continue;
                if(!_cargo.TryGetValue(c.Id,out var item))
                {
                    _flightPaths[c.Id]=MakeRect("CargoPath_"+c.Id,Vector2.zero,Vector2.zero,new Color(1,.62f,.12f,.85f));
                    _cargo[c.Id]=item=MakeRect("Cargo_"+c.Id,Vector2.zero,new Vector2(.25f,.25f),new Color(1,.62f,.12f));
                }
                var a=new Vector2(c.FromX+1-12,c.FromY+1-12);
                var b=new Vector2(c.ToX+.5f-12,c.ToY+.5f-12);
                var path=_flightPaths[c.Id].rectTransform;var delta=b-a;path.pivot=new Vector2(0,.5f);path.anchoredPosition=a;
                path.sizeDelta=new Vector2(delta.magnitude,.065f);path.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                item.rectTransform.anchoredPosition=Vector2.Lerp(a,b,Mathf.Clamp01((float)(view.AtMs-c.DepartAt)/(c.ArriveAt-c.DepartAt)));
            }
        }
        private static Vector2 Flat(Vector3 v) => new Vector2(v.x,v.z);
        private Image MakeRect(string name,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(_canvas,false);
            var rt=(RectTransform)go.transform;rt.sizeDelta=size;rt.anchoredPosition=position;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        private GameObject Model(int index,float width,string name)
        {
            var holder=new GameObject(name);holder.SetActive(false);holder.transform.SetParent(_parent,false);
            var instance=Object.Instantiate(_prefabs[index],holder.transform);
            foreach(var b in instance.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled=false;
            foreach(var a in instance.GetComponentsInChildren<Animator>(true)) a.enabled=false;
            var renderers=instance.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            instance.transform.localScale*=width/Mathf.Max(bounds.size.x,bounds.size.z);
            bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
            instance.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            holder.SetActive(true);return holder;
        }
        internal void Clear()
        {
            foreach(var g in _nodes.Values) Object.Destroy(g);foreach(var g in _formations.Values)Object.Destroy(g);
            foreach(var g in _wires.Values)Object.Destroy(g.gameObject);foreach(var g in _cargo.Values)Object.Destroy(g.gameObject);
            foreach(var group in _areas.Values)foreach(var g in group)Object.Destroy(g.gameObject);
            foreach(var g in _flightPaths.Values)Object.Destroy(g.gameObject);
            _nodes.Clear();_formations.Clear();_wires.Clear();_cargo.Clear();_areas.Clear();_flightPaths.Clear();
        }
    }
}
