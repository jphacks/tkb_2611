using System;
using ProjectPenguin.Domain.HealthFactory;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.HealthFactoryPrototype
{
    /// <summary>View-only projection: models, RectTransform routes/cargo, and controls.</summary>
    internal sealed class HealthFactoryPresenter
    {
        private static readonly Color Ink=new Color(.08f,.19f,.27f), Teal=new Color(.04f,.48f,.49f), Amber=new Color(1f,.65f,.16f);
        private readonly Camera _camera;
        private readonly Font _font;
        private readonly Transform[] _facilities;
        private readonly Transform _plot;
        private readonly HealthFactoryDriver _driver;
        private readonly RectTransform _canvas, _safe;
        private readonly Text _resources, _quote, _status, _message, _details, _record, _pause, _route0, _route1;
        private readonly RectTransform[] _cards=new RectTransform[3];
        private readonly Text[] _stock=new Text[3];
        private readonly UnityEngine.UI.Image[] _wires=new UnityEngine.UI.Image[2], _cargo=new UnityEngine.UI.Image[4];
        private readonly UnityEngine.UI.Image[] _flightPaths=new UnityEngine.UI.Image[4];
        private readonly UnityEngine.UI.Image _progress;
        private readonly Vector2[] _points=new Vector2[3];
        private int _boardSize=16;

        internal HealthFactoryPresenter(Transform parent,Camera camera,Font font,Transform[] facilities,Transform plot,HealthFactoryDriver driver)
        {
            _camera=camera;_font=font;_facilities=facilities;_plot=plot;_driver=driver;
            var root=new GameObject(HealthFactoryUiNames.Canvas,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(parent,false);root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder=200;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            _canvas=(RectTransform)root.transform;
            for(var i=0;i<2;i++)
            {_wires[i]=Picture(HealthFactoryUiNames.Wire+i,_canvas,Vector2.zero,new Vector2(1,7),Teal);_wires[i].rectTransform.pivot=new Vector2(0,.5f);}
            for(var i=0;i<4;i++)
            {
                _flightPaths[i]=Picture(HealthFactoryUiNames.Wire+HealthFactoryUiNames.Cargo+i,_canvas,Vector2.zero,Vector2.zero,Amber);
                _flightPaths[i].rectTransform.pivot=new Vector2(0,.5f);
                _cargo[i]=Picture(HealthFactoryUiNames.Cargo+i,_canvas,Vector2.zero,new Vector2(24,24),Amber);
            }
            var names=new[]{"01  漁場","02  さばき場","03  倉庫"};
            for(var i=0;i<3;i++)
            {
                var id=i;_cards[i]=Panel(HealthFactoryUiNames.Card+i,_canvas,Vector2.zero,new Vector2(310,110),new Color(.98f,1,1,.96f));
                var button=_cards[i].gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=_cards[i].GetComponent<UnityEngine.UI.Image>();
                button.onClick.AddListener(()=>driver.Select(id));
                Label(HealthFactoryUiNames.Title,_cards[i],new Vector2(0,27),new Vector2(300,42),names[i],28,Ink);
                _stock[i]=Label(HealthFactoryUiNames.Label,_cards[i],new Vector2(0,-25),new Vector2(300,55),"",24,Ink);
            }
            _progress=Picture(HealthFactoryUiNames.Progress,_cards[1],new Vector2(-145,-51),new Vector2(290,6),Amber);
            _progress.rectTransform.pivot=new Vector2(0,.5f);
            _safe=Rect(HealthFactoryUiNames.Safe,_canvas,Vector2.zero,Vector2.zero);
            _safe.anchorMin=Vector2.zero;_safe.anchorMax=Vector2.one;
            var header=Panel(HealthFactoryUiNames.Header,_safe,new Vector2(0,-210),new Vector2(1000,380),new Color(.97f,.99f,1,.98f));
            header.anchorMin=header.anchorMax=new Vector2(.5f,1);
            Label(HealthFactoryUiNames.Title,header,new Vector2(0,136),new Vector2(960,58),"歩いて動かす、工場の島",43,Ink);
            _resources=Label(HealthFactoryUiNames.Resources,header,new Vector2(0,66),new Vector2(950,64),"",39,Teal);
            _quote=Label(HealthFactoryUiNames.Quote,header,new Vector2(0,-16),new Vector2(950,85),"",28,Ink);
            _status=Label(HealthFactoryUiNames.Status,header,new Vector2(0,-91),new Vector2(960,52),"",24,Ink);
            _details=Label(HealthFactoryUiNames.Details,header,new Vector2(0,-145),new Vector2(960,48),"",23,Teal);

            var controls=Panel(HealthFactoryUiNames.Controls,_safe,new Vector2(0,340),new Vector2(1000,650),new Color(.97f,.99f,1,.98f));
            controls.anchorMin=controls.anchorMax=new Vector2(.5f,0);
            _message=Label(HealthFactoryUiNames.Message,controls,new Vector2(0,263),new Vector2(940,76),"",26,Ink);
            _pause=Button(controls,HealthFactoryUiNames.Pause,"再開",0,3,180,driver.TogglePause);
            Button(controls,HealthFactoryUiNames.Compact,"効率配置",1,3,180,()=>driver.SetLayout(true));
            Button(controls,HealthFactoryUiNames.Long,"長い配置",2,3,180,()=>driver.SetLayout(false));
            _route0=Button(controls,HealthFactoryUiNames.Route0,"経路1",0,3,104,()=>driver.ToggleRoute(0));
            _route1=Button(controls,HealthFactoryUiNames.Route1,"経路2",1,3,104,()=>driver.ToggleRoute(1));
            Button(controls,HealthFactoryUiNames.Native,"実歩数更新",2,3,104,driver.RequestNativeSteps);
            Button(controls,HealthFactoryUiNames.Left,"← 2マス",0,4,28,()=>driver.MoveSelected(-2,0));
            Button(controls,HealthFactoryUiNames.Right,"2マス →",1,4,28,()=>driver.MoveSelected(2,0));
            Button(controls,HealthFactoryUiNames.Up,"↑ 2マス",2,4,28,()=>driver.MoveSelected(0,2));
            Button(controls,HealthFactoryUiNames.Down,"↓ 2マス",3,4,28,()=>driver.MoveSelected(0,-2));
            Button(controls,HealthFactoryUiNames.Build,"バッファ建設 電気4",0,3,-48,driver.Build);
            Button(controls,HealthFactoryUiNames.Upgrade,"速度改修 電気4",1,3,-48,driver.Upgrade);
            Button(controls,HealthFactoryUiNames.Expand,"敷地拡張 水3",2,3,-48,driver.Expand);
            if(driver.DevelopmentInputs)
            {
                Button(controls,HealthFactoryUiNames.Steps6000,"累計6000歩",0,4,-124,()=>driver.FakeSteps(6000),true);
                Button(controls,HealthFactoryUiNames.Steps10000,"累計10000歩",1,4,-124,()=>driver.FakeSteps(10000),true);
                Button(controls,HealthFactoryUiNames.Steps15000,"累計15000歩",2,4,-124,()=>driver.FakeSteps(15000),true);
                Button(controls,HealthFactoryUiNames.Reset,"試作初期化",3,4,-124,driver.RestartTrial,true);
                Button(controls,HealthFactoryUiNames.Water,"当日 水記録",0,4,-200,driver.FakeWater,true);
                Button(controls,HealthFactoryUiNames.Screen,"前日 使用240分",1,4,-200,driver.FakeScreen,true);
                Button(controls,HealthFactoryUiNames.Passive,"島倍率 切替",2,4,-200,driver.TogglePassive,true);
                Button(controls,HealthFactoryUiNames.Assigned,"アサイン 切替",3,4,-200,driver.ToggleAssignment,true);
            }
            _record=Label(HealthFactoryUiNames.Record,controls,new Vector2(0,-265),new Vector2(950,50),"",22,Ink);
            Label(HealthFactoryUiNames.Policy,controls,new Vector2(0,-307),new Vector2(950,33),"専用試作 · 保存なし · 週次報酬なし · 画像選択／解析／すれ違い通信は未接続",20,Ink);
        }
        internal void Render(HealthFactoryView v,HealthFactoryQuote q)
        {
            var area=Screen.safeArea;_safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            _safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            _resources.text=$"ガス {v.Gas}    水 {v.Water}    電気 {v.Electricity}";
            _quote.text=q.Reachable?$"倉庫 {v.Stored}/4個 · 使用 {v.Spent}ガス\n残り費用 {q.FutureGas} / 不足 {q.GasShortfall}ガス ≒ 追加 {q.AdditionalSteps:N0}歩分":q.Reason;
            _status.text=_driver.StepStatus;
            _details.text=$"{(v.StepDay==_driver.CurrentAppDay?"当日歩数":"最終観測")} {v.MaxSteps:N0} · 受領 {v.GrantedGas}ガス / 日上限なし · 島倍率 ×{v.StepMultiplier} · 加工 {v.ProcessingDurationMs/1000f:0.0}秒";
            _message.text=_driver.Message;_pause.text=_driver.Paused?"時計を再開":"一時停止";
            _route0.text=$"経路1 {(v.Routes[0]?"ON":"OFF")} · {v.RouteCosts[0]}ガス";
            _route1.text=$"経路2 {(v.Routes[1]?"ON":"OFF")} · {v.RouteCosts[1]}ガス";
            _record.text=_driver.DevelopmentInputs?$"青い欄は開発用の偽入力 · {_driver.RecordDay} · 写真・実測ではありません\n{v.StopReason}":v.StopReason;
            _plot.localScale=Vector3.one*(v.BoardSize/16f);
            _boardSize=v.BoardSize;
            _camera.orthographicSize=v.BoardSize==16?11:13.5f;
            for(var i=0;i<3;i++)
            {
                var n=v.Nodes[i];_facilities[i].position=World(n.X,n.Y);
                _points[i]=ScreenPoint(_facilities[i].position+Vector3.up*.55f);
                var card=_points[i]+new Vector2(i==1?-258:258,0);
                card.x=Mathf.Clamp(card.x,-_canvas.rect.width*.5f+170,_canvas.rect.width*.5f-170);
                card.y=Mathf.Clamp(card.y,-_canvas.rect.height*.5f+730,_canvas.rect.height*.5f-465);
                _cards[i].anchoredPosition=card;
                _cards[i].GetComponent<UnityEngine.UI.Image>().color=_driver.Selected==i?new Color(.67f,.94f,.88f,.98f):new Color(.98f,1,1,.96f);
            }
            _stock[0].text=$"魚 {v.Raw}個 · ({v.Nodes[0].X},{v.Nodes[0].Y})";
            _stock[1].text=$"材料 {v.Input}/{v.InputCapacity} · 加工魚 {v.Output}/6\n"+(v.Processing?$"支払済み · 残り {Math.Max(0,v.ProcessEnd-v.AtMs)/1000f:0.0}秒":$"加工待機 · 同時便 {v.Slots}");
            _stock[2].text=$"到着 {v.Stored}/4個 · 容量20";
            _progress.rectTransform.sizeDelta=new Vector2(v.Processing?290*Mathf.Clamp01((float)(v.AtMs-v.ProcessStart)/(v.ProcessEnd-v.ProcessStart)):0,6);
            for(var i=0;i<2;i++){Wire(_wires[i],_points[i],_points[i+1],7);_wires[i].enabled=v.Routes[i];}
            for(var i=0;i<4;i++)
            {
                var visible=i<v.Deliveries.Count;_cargo[i].enabled=_flightPaths[i].enabled=visible;if(!visible)continue;
                var d=v.Deliveries[i];var a=ScreenPoint(World(d.FromX,d.FromY)+Vector3.up*.55f);var b=ScreenPoint(World(d.ToX,d.ToY)+Vector3.up*.55f);
                Wire(_flightPaths[i],a,b,7);
                _cargo[i].rectTransform.anchoredPosition=Vector2.Lerp(a,b,Mathf.Clamp01((float)(v.AtMs-d.DepartAt)/(d.ArriveAt-d.DepartAt)));
            }
        }
        private Vector3 World(int x,int y)=>new Vector3((x+1-_boardSize*.5f)*.65f,.1f,(y+1-_boardSize*.5f)*.65f);
        private Vector2 ScreenPoint(Vector3 position)
        {RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas,_camera.WorldToScreenPoint(position),null,out var point);return point;}
        private static void Wire(UnityEngine.UI.Image image,Vector2 a,Vector2 b,float width)
        {var delta=b-a;image.rectTransform.anchoredPosition=a;image.rectTransform.sizeDelta=new Vector2(delta.magnitude,width);image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);}
        private Text Button(Transform parent,string name,string caption,int column,int count,float y,Action action,bool fixture=false)
        {
            var width=940f/count;var rect=Panel(name,parent,new Vector2((column-(count-1)/2f)*width,y),new Vector2(width-12,64),fixture?new Color(.22f,.37f,.65f):Teal);
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();button.onClick.AddListener(()=>action());
            return Label(HealthFactoryUiNames.Caption,rect,Vector2.zero,new Vector2(width-22,59),caption,24,Color.white);
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;rt.SetParent(parent,false);rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.anchoredPosition=position;rt.sizeDelta=size;return rt;}
        private static RectTransform Panel(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {var rt=Rect(name,parent,position,size);rt.gameObject.AddComponent<UnityEngine.UI.Image>().color=color;return rt;}
        private static UnityEngine.UI.Image Picture(string name,Transform parent,Vector2 position,Vector2 size,Color color)
        {var image=Panel(name,parent,position,size,color).GetComponent<UnityEngine.UI.Image>();image.raycastTarget=false;return image;}
        private Text Label(string name,Transform parent,Vector2 position,Vector2 size,string value,int fontSize,Color color)
        {var text=Rect(name,parent,position,size).gameObject.AddComponent<Text>();text.font=_font;text.text=value;text.fontSize=fontSize;text.color=color;text.alignment=TextAnchor.MiddleCenter;text.verticalOverflow=VerticalWrapMode.Overflow;text.raycastTarget=false;return text;}
    }
}
