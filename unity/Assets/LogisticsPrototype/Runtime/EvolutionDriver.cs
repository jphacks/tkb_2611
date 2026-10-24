using System;
using System.Linq;
using ProjectPenguin.Domain.LogisticsPrototype;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectPenguin.LogisticsPrototype
{
    public enum EvolutionTool { Select, Fishery, Saltworks, Warehouse, Move, Connect, Remove }

    public sealed class EvolutionDriver : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private GameObject[] _models;
        [SerializeField] private Font _font;
        private EvolutionFacade _facade;
        private EvolutionPresenter _presenter;
        private EvolutionHud _hud;
        private double _elapsed;
        private float _nextRefresh;
        private float _zoom=1;
        private Vector3 _cameraHome;
        public EvolutionView View { get; private set; }
        public EvolutionResult LastResult { get; private set; }
        public EvolutionTool Tool { get; private set; }
        public int SelectedId { get; private set; }
        public bool Paused { get; private set; }
        public string Message { get; private set; }
        public int FormedEvents { get; private set; }
        public int DissolvedEvents { get; private set; }

        private void Awake()
        {
            _cameraHome=_camera.transform.position;
            ResetDemonstration();
            _presenter=new EvolutionPresenter(transform,_camera,_models);
            _hud=new EvolutionHud(transform,_camera,_font,this);
            Render();
        }
        private void Update()
        {
            _camera.orthographicSize=Mathf.Max(20f,14.5f/Mathf.Max(.2f,_camera.aspect))*_zoom;
            if(!Paused)
            {
                _elapsed+=Mathf.Min(Time.deltaTime,.5f)*1000;
                AdvanceSessionTo((long)_elapsed);
            }
            var touch=Touchscreen.current?.primaryTouch;
            if(touch!=null&&touch.press.wasPressedThisFrame) Pointer(touch.position.ReadValue());
            else if(Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame) Pointer(Mouse.current.position.ReadValue());
            if(Time.unscaledTime>=_nextRefresh) { _nextRefresh=Time.unscaledTime+.1f;Render(); }
        }
        public void AdvanceSessionTo(long milliseconds)
        {
            var result=_facade.AdvanceTo(milliseconds);
            if(result.Success) { _elapsed=Math.Max(_elapsed,milliseconds);Publish(result,false); }
            else Message=result.Reason;
        }
        private void Pointer(Vector2 point)
        {
            if(_hud.Blocks(point)) return;
            var ray=_camera.ScreenPointToRay(point);
            if(new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var distance))
            {
                var position=ray.GetPoint(distance)+new Vector3(12,0,12);
                TapCell(Mathf.FloorToInt(position.x),Mathf.FloorToInt(position.z));
            }
        }
        public void TapCell(int x,int y)
        {
            if(x<0||y<0||x>=View.BoardSize||y>=View.BoardSize){Message="敷地の中をタップしてください";Render();return;}
            var target=View.Workshops.FirstOrDefault(n=>x>=n.X&&x<n.X+n.Footprint&&y>=n.Y&&y<n.Y+n.Footprint);
            if(Tool==EvolutionTool.Select||Tool==EvolutionTool.Connect||Tool==EvolutionTool.Remove||Tool==EvolutionTool.Move&&SelectedId==0)
            {
                var formation=View.Formations.FirstOrDefault(s=>x>=s.X&&x<s.X+s.Width&&y>=s.Y&&y<s.Y+s.Height);
                if(formation!=null&&(target==null||formation.Members.Contains(target.Id)))target=View.Workshops.First(n=>n.Id==formation.CoreId);
            }
            if(Tool==EvolutionTool.Fishery||Tool==EvolutionTool.Saltworks||Tool==EvolutionTool.Warehouse)
            {
                var kind=Tool==EvolutionTool.Fishery?WorkshopKind.Fishery:Tool==EvolutionTool.Saltworks?WorkshopKind.Saltworks:WorkshopKind.Warehouse;
                Publish(_facade.Place(kind,kind==WorkshopKind.Warehouse?x:x/2*2,kind==WorkshopKind.Warehouse?y:y/2*2));
            }
            else if(Tool==EvolutionTool.Move&&SelectedId!=0)
                Publish(_facade.Move(SelectedId,x/2*2,y/2*2));
            else if(Tool==EvolutionTool.Connect&&SelectedId!=0&&target!=null)
            { Publish(_facade.Connect(SelectedId,target.Id));SelectedId=0; }
            else if(Tool==EvolutionTool.Remove&&target!=null) Publish(_facade.Remove(target.Id));
            else
            {
                SelectedId=target?.Id??0;
                Message=target==null?"空き地です。漁場・製塩所・倉庫を選んで置けます":"施設を選択。移動、線で倉庫へ接続、在庫の確認ができます";
            }
            Render();
        }
        public void SetTool(EvolutionTool tool)
        {
            Tool=tool;
            Message=tool==EvolutionTool.Move?"移動する施設を選択済みにして、空き地をタップ":tool==EvolutionTool.Connect?"送り元の施設 → 倉庫の順にタップ":tool==EvolutionTool.Remove?"撤去する施設をタップ。在庫や配送中があると撤去できません":"盤面をタップしてください";
            Render();
        }
        public void CompleteShapes() => Arrange(true);
        public void BreakShapes() => Arrange(false);
        private void Arrange(bool completed)
        {
            var ids=new[]{3,7,11};
            var cells=completed?new[]{6,16,18,16,4,6}:new[]{10,16,22,16,4,10};
            for(var i=0;i<ids.Length;i++)
            {
                var r=_facade.Move(ids[i],cells[2*i],cells[2*i+1]);Publish(r,false);
                if(!r.Success){Message=r.Reason;Render();return;}
            }
            Paused=false;Tool=EvolutionTool.Select;
            Message=completed?"3つの形を完成。ちくわ・かまぼこ・干物の建物へ！":"形を崩しました。元の施設・在庫に戻ります";
            Render();
        }
        public void ResetDemonstration()
        {
            _facade=EvolutionFactory.CreateDemonstration();_elapsed=0;SelectedId=0;Tool=EvolutionTool.Select;
            _zoom=1;_camera.transform.position=_cameraHome;
            Paused=true;FormedEvents=0;DissolvedEvents=0;View=_facade.CreateView();
            Message="まず「形を完成」を押すと、離れた漁場がつながります";
            _presenter?.Clear();Render();
        }
        public void ClearBoard()
        {
            _facade=new EvolutionFacade();_elapsed=0;SelectedId=0;Tool=EvolutionTool.Fishery;Paused=false;
            FormedEvents=0;DissolvedEvents=0;
            View=_facade.CreateView();Message="空の24×24敷地。自由に配置して形を試してください";
            _presenter?.Clear();Render();
        }
        public void DisconnectAll() { Publish(_facade.DisconnectAll());Message="線を外しました。配送済みの荷物は届きます";Render(); }
        public void TogglePause() { Paused=!Paused;Render(); }
        public void Zoom(float factor)
        {
            _zoom=Mathf.Clamp(_zoom*factor,.45f,1.3f);
            var node=View.Workshops.FirstOrDefault(n=>n.Id==SelectedId);
            if(_zoom>=1) _camera.transform.position=_cameraHome;
            else if(node!=null) { var center=EvolutionPresenter.Center(node);_camera.transform.position=_cameraHome+new Vector3(center.x,0,center.z); }
        }
        private void Publish(EvolutionResult result,bool feedback=true)
        {
            LastResult=result;
            foreach(var e in result.Events)
            { if(e.Kind==EvolutionEventKind.Formed) FormedEvents++; if(e.Kind==EvolutionEventKind.Dissolved) DissolvedEvents++; }
            View=_facade.CreateView();
            if(feedback) Message=result.Success?"操作しました":result.Reason;
        }
        private void Render() { _presenter?.Render(View,SelectedId);_hud?.Render(View); }
    }
}
