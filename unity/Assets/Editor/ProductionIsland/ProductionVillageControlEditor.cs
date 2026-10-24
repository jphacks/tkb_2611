using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Systems;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace ProjectPenguin.Editor.ProductionIsland
{
    [CustomEditor(typeof(ProductionVillageControl))]
    public sealed class ProductionVillageControlEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root=new VisualElement();var control=(ProductionVillageControl)target;
            root.Add(new HelpBox("初期値は新規村と開発用リセットにだけ適用します。現在値は下の明示操作で変更します。ゲーム画面へのデバッグ操作追加はありません。",HelpBoxMessageType.Info));
            var initial=new Foldout{text="初期設定（新規作成時だけ）",value=false};
            foreach(var name in new[]{"_initialGas","_initialElectricity","_initialWater","_initialFacilities"})initial.Add(new PropertyField(serializedObject.FindProperty(name)));
            root.Add(initial);
            var state=new Label();state.style.whiteSpace=WhiteSpace.Normal;root.Add(state);
            var error=new HelpBox("",HelpBoxMessageType.Info);root.Add(error);
            var enter=new Button(()=>control.Core?.BeginDevelopment(ProductionVillageSession.UtcNow)){text="開発プロフィールへ切替（製品状態のコピー）"};root.Add(enter);
            root.Add(new HelpBox("開発中の保存先: penguin-production-v2-debug.db。再起動時は製品プロフィールから開始します。切替ボタンで既存の開発用保存を現在の製品状態で置き換えます。",HelpBoxMessageType.Warning));
            var actions=new VisualElement();root.Add(actions);
            DoubleField gas=new DoubleField("ガス"),electricity=new DoubleField("電気"),water=new DoubleField("水");
            actions.Add(new Label("資源操作（入力だけでは変更しません）"));actions.Add(gas);actions.Add(electricity);actions.Add(water);
            actions.Add(new Button(()=>control.Core.Resources(gas.value,electricity.value,water.value,false,ProductionVillageSession.UtcNow)){text="指定値を反映"});
            actions.Add(new Button(()=>control.Core.Resources(gas.value,electricity.value,water.value,true,ProductionVillageSession.UtcNow)){text="指定値を加算"});
            var node=new IntegerField("施設ID（0 = 最初の倉庫）");var item=new EnumField("投入する品目",Item.Fish);var count=new IntegerField("個数"){value=1};
            actions.Add(node);actions.Add(item);actions.Add(count);
            actions.Add(new Button(()=>{var id=node.value==0?control.Core.View.Nodes.FirstOrDefault(n=>n.Kind==FacilityKind.Warehouse)?.Id??0:node.value;control.Core.Inject(id,(Item)item.value,count.value,ProductionVillageSession.UtcNow);}){text="容量・予約を検証して投入"});
            actions.Add(new Label("時間・速度（開始済み仕事の期限は維持）"));
            Toggle infinite=new Toggle("開発用無限"),running=new Toggle("稼働"){value=true};var time=new DoubleField("時間倍率（0〜100）"){value=1};var speed=new DoubleField("生産速度（0.01〜100）"){value=1};var delivery=new DoubleField("配送秒（0.1〜3600）"){value=1.5};
            actions.Add(infinite);actions.Add(running);actions.Add(time);actions.Add(speed);actions.Add(delivery);
            actions.Add(new Button(()=>{if(double.IsNaN(delivery.value)||double.IsInfinity(delivery.value)||delivery.value<.1||delivery.value>3600){error.text="配送秒は0.1〜3600の有限値にしてください";return;}control.Core.Configure(infinite.value,speed.value,(int)(delivery.value*1000),time.value,running.value,ProductionVillageSession.UtcNow);}){text="無限・稼働・速度を反映"});
            actions.Add(new Button(()=>{var c=control.Core;var s=c.View; c.Configure(s.Unlimited,speed.value,(int)(delivery.value*1000),c.TimeScale,!c.Running,ProductionVillageSession.UtcNow);}){text="停止 / 再開"});
            foreach(var seconds in new[]{1,60,3600}){var n=seconds;actions.Add(new Button(()=>control.Core.Forward(n,ProductionVillageSession.UtcNow)){text="+"+(seconds==3600?"1時間":seconds==60?"1分":"1秒")});}
            actions.Add(new Button(()=>{if(EditorUtility.DisplayDialog("開発用村の初期化","penguin-production-v2-debug.db の村を初期設定で置き換えます。製品用DB・旧DBは変更しません。","開発用村をリセット","取消"))control.Core.ResetDevelopment(ProductionVillageSession.UtcNow);}){text="開発用村を初期設定にリセット"});
            root.schedule.Execute(()=>{
                if(control==null)return;var c=control.Core;var v=c?.View;
                state.text=v==null?"再生すると現在の村を表示します。":$"現在: {(c.Development?"開発":"製品")} / {(c.Running?"稼働":"停止")}\nガス {v.Gas:0.##} / 電気 {v.Electricity:0.##} / 水 {v.Water:0.##}\n施設 {v.Nodes.Count} / 配送 {v.Cargo.Count} / {v.AtMs/1000.0:0.0}秒\n"+string.Join(" / ",v.Totals.Select(p=>IslandCatalog.NameOf(p.Key)+" "+p.Value))+"\n"+string.Join("\n",v.Nodes.Select(n=>$"ID {n.Id}: {IslandCatalog.Get(n.Kind).Name} 在庫 {n.Total}"));
                enter.SetEnabled(EditorApplication.isPlaying&&v!=null&&!c.Development);actions.SetEnabled(EditorApplication.isPlaying&&v!=null&&c.Development);
                error.text=c==null?"再生前: 初期設定のみ変更できます。":string.IsNullOrEmpty(c.Error)?c.Notice:c.Error;
            }).Every(200);
            return root;
        }
    }
}
