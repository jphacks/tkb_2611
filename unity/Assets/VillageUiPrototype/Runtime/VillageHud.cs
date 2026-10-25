using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.VillageSandbox;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectPenguin.VillageUiPrototype
{
    public sealed class VillageHud
    {
        private readonly VillageDriver _driver;private readonly Font _font;
        private readonly RectTransform _safe,_shelf,_sheet,_cancel;
        private readonly Text _gas,_electricity,_water,_fish,_usage,_message,_sheetTitle,_sheetBody;
        private readonly Color _ink=new Color(.1f,.23f,.3f),_teal=new Color(.02f,.52f,.55f);
        private string _sheetMode="";
        public VillageHud(Transform parent,Font font,VillageDriver driver)
        {
            _driver=driver;_font=font;
            var root=new GameObject(VillageUiNames.Canvas,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(parent,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=300;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            _safe=Rect(VillageUiNames.Safe,root.transform,Vector2.zero,Vector2.zero);_safe.anchorMin=Vector2.zero;_safe.anchorMax=Vector2.one;
            var header=Panel(VillageUiNames.Header,_safe,new Vector2(0,-140),new Vector2(1024,245));Anchor(header,1);
            Label(header,"ペンギン村",new Vector2(-270,72),new Vector2(430,55),42,TextAnchor.MiddleLeft);
            Button(header,"村の情報",new Vector2(360,72),new Vector2(230,58),()=>ShowMenu());
            _gas=Label(header,"",new Vector2(-365,-3),new Vector2(235,58),30);
            _electricity=Label(header,"",new Vector2(-120,-3),new Vector2(235,58),30);
            _water=Label(header,"",new Vector2(125,-3),new Vector2(235,58),30);
            _fish=Label(header,"",new Vector2(370,-3),new Vector2(235,58),30);
            _usage=Label(header,"",new Vector2(0,-77),new Vector2(960,45),24);
            var nav=Panel(VillageUiNames.Nav,_safe,new Vector2(0,78),new Vector2(1024,135));Anchor(nav,0);
            var labels=new[]{"ホーム","記録","スキャン","村管理","メニュー"};
            Action[] actions={()=>{CloseSheet();CloseBuild();driver.Interaction.Cancel();},ShowRecord,ShowScan,()=>ShowManagement("ペンギン"),ShowMenu};
            for(var i=0;i<5;i++)Button(nav,labels[i],new Vector2((i-2)*200,0),new Vector2(185,95),actions[i],i==0);
            var build=Button(_safe,"＋\n建てる",new Vector2(420,330),new Vector2(150,135),ShowBuild,true);Anchor(build,0);
            var stats=Button(_safe,"生産量",new Vector2(420,190),new Vector2(150,85),()=>ShowStats(false));Anchor(stats,0);
            _message=Label(_safe,"",new Vector2(-65,207),new Vector2(770,70),23);Anchor(_message.rectTransform,0);
            _cancel=Button(_safe,"配置取消",new Vector2(-420,315),new Vector2(150,75),()=>driver.Interaction.Cancel());Anchor(_cancel,0);_cancel.gameObject.SetActive(false);
            _shelf=Panel(VillageUiNames.Shelf,_safe,new Vector2(0,390),new Vector2(1024,450));Anchor(_shelf,0);
            Label(_shelf,"建てる施設",new Vector2(-275,160),new Vector2(440,55),36,TextAnchor.MiddleLeft);
            Label(_shelf,"棚から村へドラッグして配置",new Vector2(-160,100),new Vector2(670,42),25,TextAnchor.MiddleLeft);
            Button(_shelf,"閉じる",new Vector2(375,160),new Vector2(180,60),CloseBuild);
            var scrollRoot=Panel(VillageUiNames.Scroll,_shelf,new Vector2(0,-54),new Vector2(970,255));
            scrollRoot.gameObject.AddComponent<RectMask2D>();var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=Rect(VillageUiNames.Content,scrollRoot,new Vector2(0,0),new Vector2(IslandCatalog.Facilities.Count*210,235));
            content.anchorMin=content.anchorMax=new Vector2(0,.5f);content.pivot=new Vector2(0,.5f);scroll.content=content;scroll.viewport=scrollRoot;
            var index=0;
            foreach(var def in IslandCatalog.Facilities)
            {
                var d=def;var tile=Button(content,def.Name+"\n"+def.Size+"×"+def.Size,new Vector2(index*210+102,0),new Vector2(195,220),()=>driver.BeginBuild(d.Kind));
                tile.anchorMin=tile.anchorMax=new Vector2(0,.5f);
                tile.GetComponentInChildren<Text>().fontSize=28;
                var drag=tile.gameObject.AddComponent<VillageBuildDrag>();drag.Initialize(driver,d.Kind,scroll);index++;
            }
            _shelf.gameObject.SetActive(false);
            _sheet=Panel(VillageUiNames.Sheet,_safe,new Vector2(0,450),new Vector2(1024,580));Anchor(_sheet,0);
            _sheetTitle=Label(_sheet,"",new Vector2(-70,215),new Vector2(770,65),37,TextAnchor.MiddleLeft);
            Button(_sheet,"閉じる",new Vector2(390,215),new Vector2(170,65),CloseSheet);
            _sheetBody=Label(_sheet,"",new Vector2(0,-15),new Vector2(940,340),28,TextAnchor.UpperLeft);
            Button(_sheet,"ペンギン",new Vector2(-325,-230),new Vector2(290,70),()=>ShowManagement("ペンギン"));
            Button(_sheet,"組み合わせ",new Vector2(0,-230),new Vector2(290,70),()=>ShowManagement("組み合わせ"));
            Button(_sheet,"ロードマップ",new Vector2(325,-230),new Vector2(290,70),()=>ShowManagement("ロードマップ"));
            _sheet.gameObject.SetActive(false);
        }
        public void Render(SandboxView v,int selected,string notice)
        {
            var area=Screen.safeArea;_safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);_safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            _gas.text="ガス  "+(v.Unlimited?"∞":v.Gas.ToString("0"));_electricity.text="電気  "+(v.Unlimited?"∞":v.Electricity.ToString("0"));
            _water.text="水  "+(v.Unlimited?"∞":v.Water.ToString("0"));_fish.text="魚  "+(v.Unlimited?"∞":v.Totals[Item.Fish].ToString());
            _usage.text=$"ガス使用 {v.GasPerMinute:0} / 分     生産 {v.ProducedLastMinute} 個 / 分     施設 {v.Nodes.Count} 棟";
            _message.text=notice;
            _cancel.gameObject.SetActive(_driver.Interaction!=null&&_driver.Interaction.IsPlacing);
            if(_sheet.gameObject.activeSelf&&_sheetMode=="facility")FacilityText(selected);
            if(_sheet.gameObject.activeSelf&&_sheetMode=="stats")StatsText(false);
            if(_sheet.gameObject.activeSelf&&_sheetMode=="port")StatsText(true);
        }
        public bool Blocks(Vector2 p)
        {
            if(EventSystem.current==null)return false;
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=p},hits);return hits.Count>0;
        }
        public void ShowBuild(){CloseSheet();_driver.Interaction.Cancel();_shelf.gameObject.SetActive(true);}
        public void CloseBuild(){_shelf.gameObject.SetActive(false);}
        public void CloseSheet(){_sheet.gameObject.SetActive(false);_sheetMode="";}
        private void Open(string title,string mode){CloseBuild();_sheetTitle.text=title;_sheetMode=mode;_sheet.gameObject.SetActive(true);}
        public void ShowFacility(int id){Open("施設のようす","facility");FacilityText(id);}
        private void FacilityText(int id)
        {
            var n=_driver.View.Nodes.FirstOrDefault(a=>a.Id==id);if(n==null){CloseSheet();return;}
            var d=IslandCatalog.Get(n.Kind);_sheetTitle.text=d.Name;
            _sheetBody.text=$"{n.Status}\n\n置き場  {string.Join("  ",n.Stock.Where(i=>i.Value>0).Select(i=>GoodName(i.Key)+" "+i.Value))}\n座標 ({n.X}, {n.Y})    大きさ {n.Size}×{n.Size}\n\n長押しして村の空き地へ動かせます。\n近くの施設・倉庫へ自動で配送します。";
        }
        public void ShowStats(bool port){Open(port?"港と魚の生産":"村全体の生産量",port?"port":"stats");StatsText(port);}
        private void StatsText(bool port)
        {
            var v=_driver.View;
            _sheetBody.text=(port?"魚の在庫  "+v.Totals[Item.Fish]+" 個\n\n":"")+$"直近60秒の生産  {v.ProducedLastMinute} 個\n直近60秒のガス使用  {v.GasPerMinute:0}\n累計生産  {v.Produced} 個\n配送中  {v.Cargo.Count} 便\n\n所持量と生産実績を分けて表示しています。";
        }
        private void ShowRecord(){Open("村の記録","record");var v=_driver.View;_sheetBody.text=$"施設 {v.Nodes.Count} 棟\nこれまでに作った品物 {v.Produced} 個\n\nこの村での生産と資源の記録をまとめます。";}
        private void ShowScan(){Open("日常を村のエネルギーへ","scan");_sheetBody.text="歩く → ガス\n水分補給の記録 → 水\nスクリーンタイムの記録 → 電気\n\n画像の選択・解析は準備中です。";}
        private void ShowMenu(){Open("村のメニュー","menu");_sheetBody.text="建物をタップして詳しく見る\n空き地をドラッグして見渡す\nホイール・2本指で拡大縮小\n建物を長押しして移動\n\nペンギン・組み合わせ・ロードマップは下から確認できます。";}
        public void ShowManagement(string page)
        {
            Open(page,"management");
            _sheetBody.text=page=="ペンギン"?"ペンギンの状態\n\n村全体への効果と、施設で働く仲間をここで管理します。\n実際の獲得・個体管理は準備中です。":page=="組み合わせ"?
                "配置の組み合わせ\n\n漁場 × 3 を一列 → ちくわ\n漁場・製塩所・漁場 → かまぼこ\n製塩所と漁場2棟のL字 → 干物\n\n配置の参考一覧です。新しい解放ルールは準備中です。":
                "村の成長ロードマップ\n\n1  施設を建てる\n2  配送の流れを作る\n3  施設の組み合わせを試す\n4  新しい仲間と村を育てる\n\n解放条件は設計中です。";
        }
        public static string GoodName(Item item)=>new[]{"魚","すり身","塩","海藻","かまぼこ","ちくわ","干物","おでん"}[(int)item];
        private static void Anchor(RectTransform rt,float y){rt.anchorMin=rt.anchorMax=new Vector2(.5f,y);}
        private static RectTransform Rect(string name,Transform parent,Vector2 p,Vector2 size)
        {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rt=(RectTransform)go.transform;rt.anchoredPosition=p;rt.sizeDelta=size;return rt;}
        private RectTransform Panel(string name,Transform parent,Vector2 p,Vector2 size)
        {var rt=Rect(name,parent,p,size);var image=rt.gameObject.AddComponent<Image>();image.color=new Color(.98f,.995f,1,.97f);return rt;}
        private Text Label(Transform parent,string text,Vector2 p,Vector2 size,int fontSize,TextAnchor align=TextAnchor.MiddleCenter)
        {var rt=Rect(VillageUiNames.Label,parent,p,size);var t=rt.gameObject.AddComponent<Text>();t.font=_font;t.fontSize=fontSize;t.color=_ink;t.alignment=align;t.text=text;t.raycastTarget=false;return t;}
        private RectTransform Button(Transform parent,string title,Vector2 p,Vector2 size,Action action,bool filled=false)
        {
            var rt=Panel(VillageUiNames.Button+title,parent,p,size);var image=rt.GetComponent<Image>();image.color=filled?_teal:new Color(.87f,.95f,.96f);
            var b=rt.gameObject.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(()=>action());
            var text=Label(rt,title,Vector2.zero,size-Vector2.one*10,28);if(filled)text.color=Color.white;return rt;
        }
    }
    public sealed class VillageBuildDrag : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        private VillageDriver _driver;private FacilityKind _kind;private ScrollRect _scroll;private bool _building;
        public void Initialize(VillageDriver driver,FacilityKind kind,ScrollRect scroll){_driver=driver;_kind=kind;_scroll=scroll;}
        public void OnBeginDrag(PointerEventData e)
        {_building=Mathf.Abs(e.position.y-e.pressPosition.y)>Mathf.Abs(e.position.x-e.pressPosition.x);if(_building)_driver.Interaction.BeginShelf(_kind,e.position);else _scroll.OnBeginDrag(e);}
        public void OnDrag(PointerEventData e){if(_building)_driver.Interaction.DragShelf(e.position);else _scroll.OnDrag(e);}
        public void OnEndDrag(PointerEventData e){if(_building)_driver.Interaction.EndShelf(e.position);else _scroll.OnEndDrag(e);_building=false;}
    }
}
