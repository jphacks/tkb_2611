using System;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    public sealed class VillageWarehousePanel
    {
        public const string InputName = "villageWarehouseCapacity";
        public const string SaveButtonName = "villageWarehouseCapacitySave";
        private readonly IntegerField _input;
        private readonly Label _summary, _hint;
        private readonly Button _save;
        private readonly Func<int, int, bool> _setCapacity;
        private VillageNodeView _node;
        private int _incoming, _savedCapacity;

        public VillageWarehousePanel(VisualElement parent, Func<int, int, bool> setCapacity)
        {
            _setCapacity = setCapacity;
            parent.Add(IslandUi.Text("倉庫に置ける数", ProductionUiNames.CardTitle));
            parent.Add(IslandUi.Text("基本保管数（全品目の合計）", ProductionUiNames.Subtle));
            _input = new IntegerField { name = InputName, tooltip = "1〜10,000個。氷室の効果は別に加わります" };
            _input.AddToClassList(UiClasses.TextField); parent.Add(_input);
            _summary = IslandUi.Text("", ProductionUiNames.Subtle); parent.Add(_summary);
            _hint = IslandUi.Text("", ProductionUiNames.Subtle); parent.Add(_hint);
            _save = new Button(Apply) { name = SaveButtonName };
            _save.AddToClassList(UiClasses.Button); _save.AddToClassList(UiClasses.Btn.Raised);
            _save.Add(IslandUi.Text("保管数を保存")); parent.Add(_save);
            _input.RegisterValueChangedCallback(_ => Refresh()); _save.SetEnabled(false);
        }

        public void Render(VillageNodeView node, int incoming)
        {
            if (node?.Kind != FacilityKind.Warehouse) { _node = null; _save.SetEnabled(false); return; }
            // Live stock updates must not overwrite a number being typed.
            if (_node?.Id != node.Id || _savedCapacity != node.WarehouseBaseCapacity)
                _input.SetValueWithoutNotify(node.WarehouseBaseCapacity);
            _node = node; _incoming = incoming; _savedCapacity = node.WarehouseBaseCapacity;
            Refresh();
        }

        private void Refresh()
        {
            if (_node == null) return;
            var value = _input.value;
            var valid = value >= 1 && value <= VillageGame.MaxWarehouseCapacity;
            var multiplier = _node.Capacity / _node.WarehouseBaseCapacity;
            var effective = valid ? value * multiplier : 0;
            var occupied = _node.Total + _incoming;
            _summary.text = $"在庫 {_node.Total:N0}個 · 到着予定 {_incoming:N0}個 · 現在の容量 {_node.Capacity:N0}個";
            _hint.text = !valid ? "1〜10,000の整数を入力してください"
                : effective < occupied ? $"在庫と到着予定の合計{occupied:N0}個を収められる値にしてください"
                : multiplier > 1 ? $"氷室の効果 ×{multiplier} → 合計容量 {effective:N0}個"
                : $"変更後の容量 {effective:N0}個";
            _save.SetEnabled(valid && effective >= occupied && value != _savedCapacity);
        }

        private void Apply()
        {
            if (_node == null || !_save.enabledSelf) return;
            _save.SetEnabled(false);
            if (!_setCapacity(_node.Id, _input.value)) Refresh();
        }
    }
}
