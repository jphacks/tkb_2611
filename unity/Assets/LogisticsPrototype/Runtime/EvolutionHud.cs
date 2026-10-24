using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.LogisticsPrototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectPenguin.LogisticsPrototype
{
    internal sealed class EvolutionHud
    {
        private static readonly Color Ink=new Color(.07f,.2f,.27f);
        private static readonly Color Teal=new Color(.02f,.58f,.55f);
        private readonly EvolutionDriver _driver;
        private readonly Camera _camera;
        private readonly Font _font;
        private readonly RectTransform _canvas,_safe,_labels;
        private readonly Text _status,_message,_selection,_pause;
        private readonly Dictionary<string,Text> _markers=new Dictionary<string,Text>();
        private readonly Dictionary<EvolutionTool,Image> _tools=new Dictionary<EvolutionTool,Image>();
        internal EvolutionHud(Transform parent,Camera camera,Font font,EvolutionDriver driver)
        {
            _camera=camera;_font=font;_driver=driver;
            var root=new GameObject("EvolutionHud",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(parent,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            _canvas=(RectTransform)root.transform;
            _labels=Rect("BoardLabels",_canvas,Vector2.zero,Vector2.zero);_labels.anchorMin=Vector2.zero;_labels.anchorMax=Vector2.one;
            _safe=Rect("SafeArea",_canvas,Vector2.zero,Vector2.zero);_safe.anchorMin=Vector2.zero;_safe.anchorMax=Vector2.one;
            var header=Panel("EvolutionHeader",_safe,new Vector2(0,-175),new Vector2(1020,310));header.anchorMin=header.anchorMax=new Vector2(.5f,1);
            Label("Title",header,new Vector2(0,105),new Vector2(980,65),"つながる、広がる、生産の島",42,Ink);
            Label("Recipes",header,new Vector2(0,35),new Vector2(980,65),"漁場3つ → ちくわ   /   漁・塩・漁 → かまぼこ\n漁場2つ＋製塩所のL字 → 干物場",25,Ink);
            _status=Label("EvolutionStatus",header,new Vector2(0,-35),new Vector2(980,50),"",27,Teal);
            _selection=Label("Selection",header,new Vector2(0,-105),new Vector2(980,80),"",24,Ink);
            var footer=Panel("EvolutionControls",_safe,new Vector2(0,240),new Vector2(1020,440));footer.anchorMin=footer.anchorMax=new Vector2(.5f,0);
            _message=Label("Message",footer,new Vector2(0,168),new Vector2(980,65),"",25,Ink);
            ToolButton(footer,EvolutionTool.Fishery,"漁場を置く",-400,85);
            ToolButton(footer,EvolutionTool.Saltworks,"製塩所を置く",-200,85);
            ToolButton(footer,EvolutionTool.Warehouse,"倉庫を置く",0,85);
            ToolButton(footer,EvolutionTool.Select,"選択",200,85);
            ToolButton(footer,EvolutionTool.Move,"移動",400,85);
            ToolButton(footer,EvolutionTool.Connect,"線で接続",-400,0);
            ToolButton(footer,EvolutionTool.Remove,"撤去",-200,0);
            Button(footer,"Disconnect","線を外す",0,0,driver.DisconnectAll);
            Button(footer,"ZoomIn","拡大 ＋",200,0,()=>driver.Zoom(.8f));
            Button(footer,"ZoomOut","縮小 －",400,0,()=>driver.Zoom(1.25f));
            Button(footer,"CompleteShapes","形を完成",-400,-90,driver.CompleteShapes);
            Button(footer,"BreakShapes","形を崩す",-200,-90,driver.BreakShapes);
            Button(footer,"ResetDemo","やり直す",0,-90,driver.ResetDemonstration);
            Button(footer,"ClearBoard","更地にする",200,-90,driver.ClearBoard);
            _pause=Button(footer,"Pause","再開",400,-90,driver.TogglePause);
            Label("TrialValues",footer,new Vector2(0,-177),new Vector2(980,55),"24×24マス · 生産2秒 / 配送1.5秒 · 試作・保存なし\n形は配置で成立。線は倉庫へ運ぶために使います",23,Ink);
        }
        internal bool Blocks(Vector2 point)
        {
            var data=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(data,hits);return hits.Count>0;
        }
        internal void Render(EvolutionView view)
        {
            var safe=Screen.safeArea;_safe.anchorMin=new Vector2(safe.xMin/Screen.width,safe.yMin/Screen.height);_safe.anchorMax=new Vector2(safe.xMax/Screen.width,safe.yMax/Screen.height);
            _status.text=$"成立 {view.Formations.Count}組    配送 {view.Cargo.Count}個    倉庫 {view.Workshops.Where(n=>n.Kind==WorkshopKind.Warehouse).Sum(n=>n.Total)}個";
            _message.text=_driver.Message;_pause.text=_driver.Paused?"再開":"一時停止";
            var selected=view.Workshops.FirstOrDefault(n=>n.Id==_driver.SelectedId);
            _selection.text=selected==null?"施設をタップして選択 → 移動先や倉庫を選べます":$"#{selected.Id} {Name(selected.Kind)} ({selected.X},{selected.Y})  "+Stock(selected);
            foreach(var p in _tools)p.Value.color=p.Key==_driver.Tool?new Color(.08f,.32f,.4f):Teal;
            var wanted=new HashSet<string>();var grouped=new HashSet<int>(view.Formations.SelectMany(s=>s.Members));
            foreach(var n in view.Workshops.Where(n=>!grouped.Contains(n.Id)))
            {
                var key="N"+n.Id;wanted.Add(key);var text=Marker(key);
                text.text=n.Kind==WorkshopKind.Warehouse?$"庫{n.Id} {n.Total}/20":$"{n.Id} "+(n.Kind==WorkshopKind.Fishery?"魚":"塩");
                text.fontSize=21;text.rectTransform.sizeDelta=new Vector2(n.Kind==WorkshopKind.Warehouse?170:85,45);
                PlaceMarker(text,EvolutionPresenter.Center(n),-50);
            }
            foreach(var s in view.Formations)
            {
                var key="S"+s.Key;wanted.Add(key);var text=Marker(key);var core=view.Workshops.First(n=>n.Id==s.CoreId);
                var blocked=view.Workshops.Where(n=>s.Members.Contains(n.Id)).Any(n=>n.OutputBlocked);
                text.text=Name(s.Product)+"\n"+(blocked?"原料満杯で休止":GoodName(s.Product)+$" {core.Held(s.Product)}/6");
                text.fontSize=26;text.rectTransform.sizeDelta=new Vector2(270,85);
                var center=s.Product==WorkshopGood.Himono?EvolutionPresenter.Center(core):new Vector3(s.X+s.Width*.5f-12,.5f,s.Y+s.Height*.5f-12);
                PlaceMarker(text,center,-65);
            }
            foreach(var key in _markers.Keys.Where(k=>!wanted.Contains(k)).ToArray()){UnityEngine.Object.Destroy(_markers[key].gameObject);_markers.Remove(key);}
        }
        private void PlaceMarker(Text text,Vector3 world,float offset)
        {
            var screen=_camera.WorldToScreenPoint(world);RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas,screen,null,out var point);
            text.rectTransform.anchoredPosition=point+new Vector2(0,offset);
        }
        private Text Marker(string key)
        {
            if(!_markers.TryGetValue(key,out var text))_markers[key]=text=Label(key,_labels,Vector2.zero,new Vector2(230,60),"",24,Ink);
            return text;
        }
        internal static string Name(WorkshopKind kind) => kind==WorkshopKind.Fishery?"漁場":kind==WorkshopKind.Saltworks?"製塩所":"倉庫";
        internal static string GoodName(WorkshopGood good) => good==WorkshopGood.Chikuwa?"ちくわ":good==WorkshopGood.Kamaboko?"かまぼこ":good==WorkshopGood.Himono?"干物":good==WorkshopGood.Fish?"魚":"塩";
        private static string Name(WorkshopGood good) => good==WorkshopGood.Chikuwa?"ちくわ工場":good==WorkshopGood.Kamaboko?"かまぼこ工場":"干物場";
        private static string Stock(WorkshopView node) => node.Stock.Count==0?"在庫なし":string.Join("  ",node.Stock.Where(p=>p.Value>0).Select(p=>GoodName(p.Key)+" "+p.Value));
        private void ToolButton(Transform parent,EvolutionTool tool,string title,float x,float y)
        {
            var label=Button(parent,"Tool"+tool,title,x,y,()=>_driver.SetTool(tool));_tools[tool]=label.transform.parent.GetComponent<Image>();
        }
        private Text Button(Transform parent,string name,string caption,float x,float y,Action action)
        {
            var panel=Panel(name,parent,new Vector2(x,y),new Vector2(188,74));panel.GetComponent<Image>().color=Teal;
            var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=panel.GetComponent<Image>();button.onClick.AddListener(()=>action());
            return Label("Caption",panel,Vector2.zero,new Vector2(180,70),caption,25,Color.white);
        }
        private RectTransform Panel(string name,Transform parent,Vector2 position,Vector2 size)
        {var rt=Rect(name,parent,position,size);rt.gameObject.AddComponent<Image>().color=new Color(.97f,.99f,1,.97f);return rt;}
        private RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
        }
        private Text Label(string name,Transform parent,Vector2 position,Vector2 size,string caption,int fontSize,Color color)
        {
            var rt=Rect(name,parent,position,size);var text=rt.gameObject.AddComponent<Text>();text.font=_font;text.fontSize=fontSize;text.color=color;
            text.text=caption;text.alignment=TextAnchor.MiddleCenter;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;return text;
        }
    }
}
