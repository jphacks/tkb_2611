using System;
using ProjectPenguin.Domain.Village;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>Reversible operating intensity; the session owns validation and saving.</summary>
    public sealed class VillageEffortPanel
    {
        public const string DecreaseButtonName = "villageEffortDecrease";
        public const string IncreaseButtonName = "villageEffortIncrease";
        public const string ResetButtonName = "villageEffortReset";
        private readonly Label _effort, _effect, _gas, _hint;
        private readonly VisualElement[] _flames = new VisualElement[3];
        private readonly Label _flameHint;
        private readonly Button _decrease, _increase, _reset;
        private readonly Func<int, int, bool> _setEffort;
        private VillageNodeView _node;
        private double _availableGas;

        public VillageEffortPanel(VisualElement parent, Func<int, int, bool> setEffort)
        {
            _setEffort = setEffort;
            parent.AddToClassList(ProductionUiNames.Effort);
            var header = new VisualElement(); header.AddToClassList(ProductionUiNames.EffortHeader);
            header.Add(IslandUi.Text("エフォート", ProductionUiNames.CardTitle));
            _effort = IslandUi.Text("", ProductionUiNames.EffortValue); header.Add(_effort); parent.Add(header);
            _effect = IslandUi.Text("", ProductionUiNames.EffortEffect); parent.Add(_effect);
            _gas = IslandUi.Text("", ProductionUiNames.Subtle); parent.Add(_gas);
            var row = new VisualElement(); row.AddToClassList(ProductionUiNames.EffortActions);
            row.name = "currentEffortFlames";
            for (var i = 0; i < _flames.Length; i++)
            {
                _flames[i] = new VisualElement(); _flames[i].AddToClassList(ProductionUiNames.EffortFlame); row.Add(_flames[i]);
            }
            _flameHint = IslandUi.Text("", ProductionUiNames.Subtle); row.Add(_flameHint); parent.Add(row);
            _hint = IslandUi.Text("", ProductionUiNames.EffortHint); parent.Add(_hint);
            var actions = new VisualElement(); actions.AddToClassList(ProductionUiNames.EffortActions);
            _decrease = Button(actions, DecreaseButtonName, "− 下げる", () => Change(-1), false);
            _increase = Button(actions, IncreaseButtonName, "＋ 上げる", () => Change(1), true);
            _reset = Button(actions, ResetButtonName, "0に戻す", () => Apply(0), false);
            parent.Add(actions);
            parent.Add(IslandUi.Text("設定の変更に費用はかかりません。生産速度と1加工のガス消費を増やすため、毎分のガス消費も増えます。加工中の分はそのまま完了し、次の加工から反映されます。", ProductionUiNames.Subtle));
            Disable();
        }

        private static Button Button(VisualElement parent, string name, string text, Action action, bool primary)
        {
            var button = new Button(action) { name = name };
            button.AddToClassList(UiClasses.Button); button.AddToClassList(primary ? UiClasses.Btn.Raised : UiClasses.Btn.Ghost);
            button.Add(IslandUi.Text(text)); parent.Add(button); return button;
        }

        public void Render(VillageNodeView node, double gas)
        {
            _node = node; _availableGas = gas;
            if (node?.EffortSettings == null) { Disable(); return; }
            var settings = node.EffortSettings;
            _effort.text = $"{node.Effort} / {VillageEffortRules.MaxEffort}";
            _effect.text = settings.Supported ? VillageEffortRules.Effect(node.Effort) : "この施設は通常稼働です";
            _gas.text = $"所持ガス {gas:0.#}";
            for (var i = 0; i < _flames.Length; i++) IslandUi.SetVisible(_flames[i], i < node.Effort);
            _flameHint.text = node.Effort == 0 ? "通常運転 · 炎なし" : $"施設の上に炎 {node.Effort}つ";
            _hint.text = settings.Supported && gas < settings.GasPerCycle
                ? "次の加工に必要なガスが不足しています。エフォートを下げるか、ガスを補給してください" : settings.Reason;
            _decrease.SetEnabled(settings.Supported && node.Effort > 0);
            _increase.SetEnabled(settings.Supported && node.Effort < VillageEffortRules.MaxEffort);
            _reset.SetEnabled(settings.Supported && node.Effort > 0);
        }

        private void Disable() { _decrease.SetEnabled(false); _increase.SetEnabled(false); _reset.SetEnabled(false); }
        private void Change(int delta) { if (_node != null) Apply(_node.Effort + delta); }
        private void Apply(int effort)
        {
            if (_node?.EffortSettings.Supported != true || effort < 0 || effort > VillageEffortRules.MaxEffort) return;
            Disable();
            // A fresh Render after a successful save supplies the authoritative value.
            if (_setEffort(_node.Id, effort)) _node = null;
            else Render(_node, _availableGas);
        }
    }
}
