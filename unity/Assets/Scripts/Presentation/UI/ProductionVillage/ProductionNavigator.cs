using System;
using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.World.ProductionVillage;
using ProjectPenguin.Systems;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    [Serializable] public sealed class VillageThumbnail
    {
        [SerializeField] private FacilityKind _kind;
        [SerializeField] private Texture2D _texture;
        public FacilityKind Kind=>_kind;public Texture2D Texture=>_texture;
    }
    [RequireComponent(typeof(UIDocument))]
    public sealed class ProductionNavigator : MonoBehaviour
    {
        [SerializeField] private ProductionVillageSession _session;
        [SerializeField] private VillageWorldPresenter _world;
        [SerializeField] private VillageThumbnail[] _thumbnails;
        private VisualElement _root,_sheet,_tools,_tray;
        private ScrollView _content;
        private Label _live;
        private Func<string> _liveText;
        private bool _home=true;
        private float _nextRefresh;
        public bool WorldInputEnabled=>_home&&_sheet.ClassListContains(ProductionUiNames.Hidden)&&_session.Core.View!=null;
        public VillagePlacementController Placement=>_world.Placement;
        public VillageWorldPresenter World=>_world;
        private void Start()
        {
            _root=GetComponent<UIDocument>().rootVisualElement;
            _sheet=_root.Q(ProductionUiNames.Sheet);_content=_root.Q<ScrollView>(ProductionUiNames.Content);_tools=_root.Q(ProductionUiNames.Tools);_tray=_root.Q(ProductionUiNames.Tray);
            Bind(ProductionUiNames.Placement,ShowPlacement);Bind(ProductionUiNames.Production,()=>ShowSummary(false));Bind(ProductionUiNames.Harbor,()=>ShowSummary(true));
            Bind(ProductionUiNames.HomeTab,Close);Bind(ProductionUiNames.Close,Close);Bind(ProductionUiNames.RecordTab,ShowRecord);Bind(ProductionUiNames.ScanTab,ShowScan);Bind(ProductionUiNames.ManageTab,ShowManagement);Bind(ProductionUiNames.MenuTab,ShowMenu);
            Bind(ProductionUiNames.Confirm,()=>Placement?.Confirm());Bind(ProductionUiNames.Cancel,()=>Placement?.CancelPreview());Bind(ProductionUiNames.Done,()=>Placement?.Cancel());
        }
        private void Bind(string name,Action action)=>_root.Q<Button>(name).clicked+=action;
        private void Update()
        {
            if(_root==null||Time.unscaledTime<_nextRefresh)return;_nextRefresh=Time.unscaledTime+.1f;
            var c=_session.Core;var v=c.View;
            Text(ProductionUiNames.Profile,c.Development?"開発用村":"有限資源");
            Text(ProductionUiNames.Gas,$"ガス  {v?.Gas??0:0.#}");Text(ProductionUiNames.Electricity,$"電気  {v?.Electricity??0:0.#}");Text(ProductionUiNames.Water,$"水  {v?.Water??0:0.#}");Text(ProductionUiNames.Fish,$"お魚  {(v==null?0:v.Totals[Item.Fish])}");
            Text(ProductionUiNames.Notice,c.Error.Length>0?c.Error:c.Notice);
            var placing=Placement!=null&&Placement.Mode!=VillagePlacementMode.Browse;
            _tools.EnableInClassList(ProductionUiNames.Hidden,placing||!_home);_tray.EnableInClassList(ProductionUiNames.Hidden,!placing);
            if(placing)
            {
                Text(ProductionUiNames.Reason,Placement.Reason.Length>0?Placement.Reason:Placement.Mode==VillagePlacementMode.Build?"空き地を選び、建設を確定してください":"ここに移せます。在庫と進捗は維持されます");
                var confirm=_root.Q<Button>(ProductionUiNames.Confirm);confirm.text=Placement.Mode==VillagePlacementMode.Build?"ここに建てる":"ここに移す";confirm.SetEnabled(Placement.HasCandidate&&Placement.Reason.Length==0);
            }
            if(_live!=null)_live.text=_liveText?.Invoke()??"";
        }
        private void Text(string name,string value)=>_root.Q<Label>(name).text=value;
        public bool Blocks(Vector2 screen)
        {
            if(_root?.panel==null)return true;
            var p=RuntimePanelUtils.ScreenToPanel(_root.panel,new Vector2(screen.x,Screen.height-screen.y));
            var hit=_root.panel.Pick(p);
            while(hit!=null){if(hit==_sheet||hit==_tools||hit==_tray||hit.name==ProductionUiNames.Header||hit.name==ProductionUiNames.Navigation)return true;hit=hit.parent;}
            return false;
        }
        private void Open(string title,bool home=false)
        {Placement?.Cancel();_home=home;_content.Clear();_live=null;_liveText=null;Text(ProductionUiNames.Title,title);_sheet.RemoveFromClassList(ProductionUiNames.Hidden);}
        public void Close(){Placement?.Cancel();_home=true;_sheet.AddToClassList(ProductionUiNames.Hidden);_world.SelectedId=0;}
        private VisualElement Card(string title,string body)
        {
            var card=new VisualElement();card.AddToClassList(ProductionUiNames.Card);
            var heading=new Label(title);heading.AddToClassList(ProductionUiNames.CardTitle);card.Add(heading);
            if(!string.IsNullOrEmpty(body)){var label=new Label(body);label.AddToClassList(ProductionUiNames.Body);card.Add(label);}
            _content.Add(card);return card;
        }
        private static Button Action(VisualElement parent,string title,Action action,bool enabled=true)
        {var b=new Button(action){text=title};b.AddToClassList(ProductionUiNames.Button);b.SetEnabled(enabled);parent.Add(b);return b;}
        private void Live(VisualElement card,Func<string> text)
        {_live=new Label(text());_live.AddToClassList(ProductionUiNames.Body);card.Add(_live);_liveText=text;}
        public void ShowPlacement()
        {
            Open("村に配置",true);
            Action(Card("動かす","施設を選ぶ → 移動先 → ここに移す。取消で元の位置を保ちます。"),"動かす",()=>{Close();Placement.MoveSelect();});
            Card("建てる","電気と水を使って建設します。固有効果が準備中の施設は、建設できません。");
            foreach(var def in IslandCatalog.Facilities.OrderBy(d=>VillageGame.Supported(d.Kind)?0:1).ThenBy(d=>(int)d.Kind))
            {
                var d=def;var supported=VillageGame.Supported(d.Kind);
                var recipe=d.Recipes.FirstOrDefault();
                var body=supported?(d.Kind==FacilityKind.Warehouse?"容量20。近隣施設からの品物を保管します。":$"{string.Join(" + ",recipe.Inputs.Select(i=>IslandCatalog.NameOf(i.Item)+" ×"+i.Count))} → {IslandCatalog.NameOf(recipe.Output.Item)} ×{recipe.Output.Count}\n加工 {recipe.Seconds:0.#}秒 / ガス1（試作値）"):"固有効果は準備中です。建設はまだできません。";
                var card=Card(d.Name,body+$"\n{d.Size}×{d.Size} / 電気 {d.ElectricityCost:0.#} / 水 {d.WaterCost:0.#}");
                var texture=_thumbnails?.FirstOrDefault(t=>t.Kind==d.Kind)?.Texture;
                if(texture!=null){var image=new Image{image=texture,scaleMode=ScaleMode.ScaleToFit};image.AddToClassList(ProductionUiNames.Thumbnail);card.Insert(1,image);}
                var view=_session.Core.View;Action(card,"建てる",()=>{Close();Placement.Build(d.Kind);},supported&&view!=null&&(view.Unlimited||view.Electricity>=d.ElectricityCost&&view.Water>=d.WaterCost));
            }
        }
        public void ShowFacility(int id)
        {
            var n=_session.Core.View.Nodes.FirstOrDefault(a=>a.Id==id);if(n==null)return;
            Open(IslandCatalog.Get(n.Kind).Name,true);_world.SelectedId=id;
            var card=Card("施設の状態",$"施設 ID {id} / {n.Size}×{n.Size}");
            Live(card,()=>{
                var node=_session.Core.View.Nodes.FirstOrDefault(a=>a.Id==id);if(node==null)return "施設がありません";
                return $"{node.Status}\n加工進捗 {node.Progress*100:0}% / 保管 {node.Total}\n"+string.Join("\n",node.Stock.Where(p=>p.Value>0).Select(p=>IslandCatalog.NameOf(p.Key)+" ×"+p.Value))+"\n入庫予約 "+_session.Core.View.Cargo.Count(c=>c.To==id);
            });
            var r=IslandCatalog.Get(n.Kind).Recipes.FirstOrDefault();if(r!=null)Card("加工と配送",$"材料: {(r.Inputs.Count==0?"なし":string.Join(" / ",r.Inputs.Select(i=>IslandCatalog.NameOf(i.Item)+" ×"+i.Count)))}\n完成: {IslandCatalog.NameOf(r.Output.Item)} ×{r.Output.Count}\n加工1回にガス1。8マス以内の対象施設へ自動配送。配送ガスは距離に応じます。");
            Action(card,"動かす",()=>{Close();Placement.MoveSelect();Placement.Select(id);});
            Action(card,"撤去（在庫・配送・加工が空の場合）",()=>{if(_session.Core.Remove(id,ProductionVillageSession.UtcNow))Close();});
        }
        public void ShowSummary(bool harbor)
        {
            Open(harbor?"港と保管量":"生産量");
            var card=Card(harbor?"村の物流":"直近60秒の実績",harbor?"港は保管状況の確認場所です。旧共有在庫の注文・自動売却は接続していません。":"所持量と生産実績は別の値です。");
            Live(card,()=>{
                var v=_session.Core.View;if(v==null)return _session.Core.Error;
                return $"直近60秒の生産 {v.ProducedLastMinute}個\n直近60秒のガス使用 {v.GasPerMinute:0.#}\n累計生産 {v.Produced}個 / 累計ガス {v.GasSpent:0.#}\n配送中 {v.Cargo.Count}便\n\n"+string.Join("\n",v.Totals.Select(p=>IslandCatalog.NameOf(p.Key)+$"  {p.Value}個（うち配送中 {v.Cargo.Count(c=>c.Item==p.Key)}）"));
            });
        }
        private void ShowRecord()
        {
            Open("日常の記録");var card=Card("受け取った資源","歩数 → ガス / 水分補給 → 水 / 前日スクリーンタイム → 電気。受領済みの記録は重複加算しません。");
            Live(card,()=>{
                var entries=_session.Core.Records?.Entries;if(entries==null)return _session.Core.Error;
                return entries.Count==0?"まだ受領記録がありません。":string.Join("\n\n",entries.AsEnumerable().Reverse().Take(100).Select(e=>new DateTime(1970,1,1).AddDays(e.Day).ToString("yyyy/MM/dd")+"  "+e.Kind+" +"+e.Amount));
            });
            var health=_session.GetComponent<VillageHealthInput>();Card("歩数",health?.Status??"未接続");Action(card,"歩数を再確認",()=>{health?.Request();ShowRecord();});
        }
        private void ShowScan()
        {
            Open("記録をスキャン");Card("日常行動から資源へ","記録種別を選んでください。確認済み結果を受け取るまで資源は増えません。");
            foreach(var water in new[]{true,false})
            {
                var isWater=water;var card=Card(water?"水分補給の記録":"前日のスクリーンタイム",water?"本日の確認済み記録から水 +3（試作値）。摂取量の実測ではありません。":"前日1日分の確認済み使用時間から電気を受領します（試作係数）。");
                Action(card,"この記録を選ぶ",()=>ShowScanUnavailable(isWater));
            }
        }
        private void ShowScanUnavailable(bool water)
        {Open(water?"水分補給の記録":"スクリーンタイム");var c=Card("解析APIが未接続です","現在の写真判定契約は、この健康記録の種別・対象日・確定結果に対応していません。画像を送信・保存せず、資源も付与しません。契約と検証APIの接続後に撮影・画像選択を有効にします。");Action(c,"撮影 / 画像を選択（未接続）",()=>{},false);Action(c,"再試行",()=>ShowScanUnavailable(water));Action(c,"記録種別へ戻る",ShowScan);}
        private void ShowManagement()
        {
            Open("村の管理");Card("ペンギン","背景のペンギンは表示用です。新しいペンギンの実入手・配置効果は準備中です。");
            Card("組み合わせ","施設の近隣配置で配送距離が変わります。配置進化・組み合わせ報酬は未接続です。");
            Card("ロードマップ","施設別在庫・加工配送・無料移動はこの村で利用できます。研究解放・増幅・氷室・仕分けの固有効果は準備中です。");
        }
        private void ShowMenu()
        {
            Open("メニュー");var card=Card("遊び方","配置 → 建てる、または動かす → 施設選択 → 移動先 → 確定 / 取消。通常画面ではドラッグでカメラ移動、二本指でズーム。受入便がある施設は到着まで移動できません。");
            Action(card,"保存 / 読込を再試行",()=>_session.RetryLoad());
            Action(card,"歩数・健康記録",ShowRecord);
            Card("試作由来の設定","初期資源は各100。初期建設後は電気65。加工ガス1、配送距離費用、健康報酬係数は試作値です。旧ミッション・図鑑・氷の成長則・共有在庫からの移行は新しい状態への接続を保留しています。");
            Card("データについて","新しい村は専用のSQLiteへ保存します。旧penguin.dbは参照しません。画像や購買明細は保存しません。");
        }
    }
}
