using System;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectPenguin.LogisticsPrototype
{
    /// <summary>Canvas Images form the wires. Screen positions track the three 3D facilities.</summary>
    internal sealed class LogisticsPrototypeHud
    {
        private static readonly Color Ink = new Color(.08f, .19f, .26f);
        private static readonly Color Teal = new Color(.08f, .65f, .65f);
        private static readonly Color Amber = new Color(1f, .67f, .22f);
        private readonly Camera _camera;
        private readonly Transform[] _facilities;
        private readonly LogisticsPrototypeController _owner;
        private readonly Font _font;
        private readonly RectTransform _canvas;
        private readonly RectTransform _safe;
        private readonly RectTransform[] _cards = new RectTransform[3];
        private readonly Image[] _cardBackgrounds = new Image[3];
        private readonly Text[] _stock = new Text[3];
        private readonly Image[] _lines = new Image[2];
        private readonly Image[] _cargo = new Image[2];
        private readonly Text[] _delivery = new Text[2];
        private readonly Image _progress;
        private readonly Text _message;
        private readonly Text _summary;
        private readonly Text _pause;
        private readonly Vector2[] _points = new Vector2[3];

        public LogisticsPrototypeHud(Transform parent, Camera camera, Transform[] facilities, Font font, LogisticsPrototypeController owner)
        {
            _camera = camera; _facilities = facilities; _font = font; _owner = owner;
            var go = new GameObject("LogisticsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.GetComponent<Canvas>().sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _canvas = (RectTransform)go.transform;
            _safe = Rect("SafeArea", _canvas, Vector2.zero, Vector2.zero); _safe.anchorMin = Vector2.zero; _safe.anchorMax = Vector2.one;
            var header = Panel("Header", _safe, new Vector2(0, -140), new Vector2(960, 240), new Color(.97f, .99f, 1f, .97f));
            header.anchorMin = header.anchorMax = new Vector2(.5f, 1);
            Label("Title", header, new Vector2(0, 65), new Vector2(900, 65), "小さな物流の島", 48, Ink);
            Label("Subtitle", header, new Vector2(0, 0), new Vector2(900, 50), "魚を運ぶ → さばく → 倉庫へ", 29, Ink);
            Label("Timing", header, new Vector2(0, -67), new Vector2(900, 45), owner.TimingLabel, 26, Teal);
            for (var i=0;i<2;i++)
            {
                _lines[i] = Picture("RectWire" + i, _canvas, Vector2.zero, new Vector2(100, 9), Teal);
                _lines[i].rectTransform.pivot = new Vector2(0, .5f);
                _delivery[i] = Label("DeliveryState"+i, _canvas, Vector2.zero, new Vector2(340, 58), "", 25, Ink);
                _cargo[i] = Picture("Cargo" + i, _canvas, Vector2.zero, new Vector2(28, 28), Amber);
            }
            var names = new[] { "01  漁場", "02  さばき場", "03  倉庫" };
            for(var i=0;i<3;i++)
            {
                var index = i;
                _cards[i] = Panel("FacilityCard"+i, _canvas, Vector2.zero, new Vector2(470, 166), Color.white);
                _cardBackgrounds[i] = _cards[i].GetComponent<Image>();
                var button = _cards[i].gameObject.AddComponent<Button>(); button.targetGraphic = _cardBackgrounds[i];
                button.onClick.AddListener(()=>owner.SelectFacility(index));
                Label("Name", _cards[i], new Vector2(0, 44), new Vector2(440, 53), names[i], 31, Ink);
                _stock[i] = Label("Stock", _cards[i], new Vector2(0, -19), new Vector2(450, 74), "", 25, Ink);
            }
            _progress = Picture("ProcessingProgress", _cards[1], new Vector2(-205, -72), new Vector2(410, 7), Amber);
            _progress.rectTransform.pivot = new Vector2(0, .5f);
            var footer = Panel("Controls", _safe, new Vector2(0, 190), new Vector2(960, 320), new Color(.97f,.99f,1f,.97f));
            footer.anchorMin = footer.anchorMax = new Vector2(.5f, 0);
            _summary = Label("Summary", footer, new Vector2(0, 109), new Vector2(920, 50), "", 29, Ink);
            _message = Label("Message", footer, new Vector2(0, 44), new Vector2(920, 70), "", 25, Ink);
            Control(footer, "線を外す", -305, owner.DisconnectAll);
            Control(footer, "リセット", 305, owner.ResetChain);
            _pause = Control(footer, "一時停止", 0, owner.TogglePause);
            Label("SavePolicy", footer, new Vector2(0, -133), new Vector2(920, 35), "試作専用 · 保存なし · 配送中は黄色 / 加工中は橙のバー", 22, Ink);
            _safe.SetAsLastSibling();
        }
        public void Refresh()
        {
            var area = Screen.safeArea;
            _safe.anchorMin = new Vector2(area.xMin/Screen.width, area.yMin/Screen.height);
            _safe.anchorMax = new Vector2(area.xMax/Screen.width, area.yMax/Screen.height);
            var c = _owner.Chain;
            for(var i=0;i<3;i++)
            {
                var screen = _camera.WorldToScreenPoint(_facilities[i].position + Vector3.up * 1.1f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, screen, null, out _points[i]);
                _cards[i].anchoredPosition = _points[i] + new Vector2(0, -112);
                _cardBackgrounds[i].color = _owner.Selected == i ? new Color(.67f,.94f,.90f) : new Color(1,1,1,.96f);
            }
            _stock[0].text = $"原料の魚  {c.Raw}個";
            _stock[1].text = $"材料 {c.Input}/2  ·  加工品 {c.Output}/6\n" + (c.Processing ? $"加工中  {c.ProcessingProgress:P0}" : c.Output >= 6 ? "出力満杯 · 配送待ち" : "材料待ち");
            _stock[2].text = $"入庫  {c.Stored}/20個";
            _progress.rectTransform.sizeDelta = new Vector2(410 * (float)c.ProcessingProgress, 7);
            for(var i=0;i<2;i++)
            {
                var a = _points[i] + Vector2.up*25; var b = _points[i+1]+Vector2.up*25;
                var diff=b-a; var line=_lines[i].rectTransform;
                line.anchoredPosition=a; line.sizeDelta=new Vector2(diff.magnitude, 9);
                line.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(diff.y,diff.x)*Mathf.Rad2Deg);
                _lines[i].enabled = c.Connected(i) || c.Flying(i);
                _lines[i].color = c.Flying(i) ? Amber : Teal;
                _cargo[i].enabled = c.Flying(i);
                _cargo[i].rectTransform.anchoredPosition=Vector2.Lerp(a,b,(float)c.DeliveryProgress(i));
                _delivery[i].rectTransform.anchoredPosition=(a+b)*.5f+new Vector2(140,0);
                _delivery[i].text=c.Flying(i) ? $"配送中  {c.DeliveryProgress(i):P0}" : c.Connected(i) ? "接続 →" : "未接続";
            }
            _summary.text = $"配送中 {c.InTransit}個    ·    倉庫に到着 {c.Stored}個";
            _message.text = _owner.Message;
            _pause.text = _owner.Paused ? "再開" : "一時停止";
        }
        private Text Control(Transform parent, string caption, float x, Action action)
        {
            var panel=Panel(caption,parent,new Vector2(x,-57),new Vector2(275,90),Teal);
            var b=panel.gameObject.AddComponent<Button>(); b.targetGraphic=panel.GetComponent<Image>();
            b.onClick.AddListener(()=>action());
            return Label("Caption",panel,Vector2.zero,new Vector2(260,80),caption,29,Color.white);
        }
        private RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rt.SetParent(parent,false); rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f); rt.anchoredPosition=pos; rt.sizeDelta=size; return rt;
        }
        private RectTransform Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var rt=Rect(name,parent,pos,size); rt.gameObject.AddComponent<Image>().color=color; return rt;
        }
        private Image Picture(string name,Transform parent,Vector2 pos,Vector2 size,Color color)
        {
            var rt=Panel(name,parent,pos,size,color); var img=rt.GetComponent<Image>(); img.raycastTarget=false; return img;
        }
        private Text Label(string name, Transform parent, Vector2 pos, Vector2 size, string value, int fontSize, Color color)
        {
            var rt=Rect(name,parent,pos,size); var text=rt.gameObject.AddComponent<Text>();
            text.font=_font; text.text=value; text.fontSize=fontSize; text.color=color;
            text.alignment=TextAnchor.MiddleCenter; text.verticalOverflow=VerticalWrapMode.Overflow;
            text.raycastTarget=false; return text;
        }
    }
}
