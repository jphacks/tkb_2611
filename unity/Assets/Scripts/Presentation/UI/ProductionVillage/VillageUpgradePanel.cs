using System;
using ProjectPenguin.Domain.Village;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>Displays a detached upgrade quote. The session owns spending and saving.</summary>
    public sealed class VillageUpgradePanel
    {
        public const string UpgradeButtonName = "villageUpgradeButton";
        public const string ConfirmButtonName = "villageUpgradeConfirm";
        public const string CancelButtonName = "villageUpgradeCancel";
        private readonly Label _level, _current, _next, _cost, _balance, _hint, _confirmationText, _buttonLabel;
        private readonly VisualElement _nextStage, _confirmation;
        private readonly VisualElement _currentTile, _nextTile;
        private readonly Button _button, _confirm;
        private readonly Func<int, int, bool> _upgrade;
        private VillageNodeView _node;
        private int _expectedLevel;
        private bool _confirming;

        public VillageUpgradePanel(VisualElement parent, Func<int, int, bool> upgrade)
        {
            _upgrade = upgrade;
            parent.AddToClassList(ProductionUiNames.Upgrade);
            var header = new VisualElement(); header.AddToClassList(ProductionUiNames.UpgradeHeader);
            header.Add(IslandUi.Text("施設を強化", ProductionUiNames.CardTitle));
            _level = IslandUi.Text("", ProductionUiNames.UpgradeLevel); header.Add(_level); parent.Add(header);
            var grid = new VisualElement(); grid.AddToClassList(ProductionUiNames.UpgradeGrid);
            var currentStage = Stage("現在", false); _current = IslandUi.Text("", ProductionUiNames.UpgradeEffect); currentStage.Add(_current);
            _nextStage = Stage("強化後", true); _next = IslandUi.Text("", ProductionUiNames.UpgradeEffect); _nextStage.Add(_next);
            _currentTile = TileSwatch(currentStage, "currentLevelTile"); _nextTile = TileSwatch(_nextStage, "nextLevelTile");
            grid.Add(currentStage); grid.Add(_nextStage); parent.Add(grid);
            var costRow = new VisualElement(); costRow.AddToClassList(ProductionUiNames.UpgradeCost);
            costRow.Add(IslandUi.Icon(IslandUi.IconClass(ProjectPenguin.Domain.Island.Resource.Electricity), UiClasses.Tint.Electricity));
            _cost = IslandUi.Text("", UiClasses.Text.Label, UiClasses.Text.Bold); costRow.Add(_cost); parent.Add(costRow);
            _balance = IslandUi.Text("", ProductionUiNames.Subtle); parent.Add(_balance);
            _hint = IslandUi.Text("", ProductionUiNames.UpgradeHint); parent.Add(_hint);
            _button = Button(parent, UpgradeButtonName, "強化内容を確認", BeginConfirmation, true, out _buttonLabel);
            _confirmation = new VisualElement(); _confirmation.AddToClassList(ProductionUiNames.UpgradeConfirm);
            _confirmationText = IslandUi.Text("", ProductionUiNames.UpgradeEffect); _confirmation.Add(_confirmationText);
            var actions = new VisualElement(); actions.AddToClassList(ProductionUiNames.UpgradeActions);
            Button(actions, CancelButtonName, "やめる", () => { _confirming = false; RefreshConfirmation(); }, false, out _);
            _confirm = Button(actions, ConfirmButtonName, "確認して強化", Commit, true, out _);
            _confirmation.Add(actions); parent.Add(_confirmation);
            parent.Add(IslandUi.Text("生産中の加工・配送はそのまま完了し、速度の変化は次の生産から反映されます。1回分の材料・ガスは同じですが、毎分の消費は速度に応じて増えます。", ProductionUiNames.Subtle));
            RefreshConfirmation();
        }

        private static VisualElement Stage(string caption, bool next)
        {
            var stage = new VisualElement(); stage.AddToClassList(ProductionUiNames.UpgradeStage);
            if (next) stage.AddToClassList(ProductionUiNames.UpgradeNext);
            stage.Add(IslandUi.Text(caption, ProductionUiNames.UpgradeCaption)); return stage;
        }

        private static VisualElement TileSwatch(VisualElement parent, string name)
        {
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.marginTop = 8;
            var tile = new VisualElement { name = name }; tile.style.width = 18; tile.style.height = 18; tile.style.marginRight = 8;
            row.Add(tile); row.Add(IslandUi.Text("足元のタイル色", ProductionUiNames.Subtle)); parent.Add(row); return tile;
        }

        private static Button Button(VisualElement parent, string name, string text, System.Action action, bool primary, out Label label)
        {
            var button = new Button(action) { name = name };
            button.AddToClassList(UiClasses.Button);
            button.AddToClassList(primary ? UiClasses.Btn.Raised : UiClasses.Btn.Ghost);
            label = IslandUi.Text(text); button.Add(label); parent.Add(button); return button;
        }

        public void Render(VillageNodeView node, double electricity)
        {
            if (node?.Upgrade == null) { _node = null; _button.SetEnabled(false); _confirm.SetEnabled(false); return; }
            var changed = _confirming && (_node?.Id != node.Id || _expectedLevel != node.Level);
            if (changed) _confirming = false;
            _node = node;
            var quote = node.Upgrade;
            var maximum = quote.Level == VillageUpgradeRules.MaxLevel;
            _level.text = maximum ? $"Lv {quote.Level} · MAX" : $"Lv {quote.Level} → {quote.NextLevel}";
            _current.text = quote.CurrentEffect; _next.text = quote.NextEffect;
            _currentTile.style.backgroundColor = VillageLevelColors.Tile(quote.Level);
            _nextTile.style.backgroundColor = VillageLevelColors.Tile(quote.NextLevel);
            IslandUi.SetVisible(_nextStage, !maximum);
            _cost.text = maximum ? "強化完了" : quote.Free ? "開発用：資源消費なし" : $"電気 {quote.ElectricityCost:0} を使用";
            _balance.text = $"所持している電気 {electricity:0.#}";
            _hint.text = changed ? "レベルが変わりました。新しい強化内容を確認してください。" : quote.Reason;
            _hint.EnableInClassList(ProductionUiNames.UpgradeWarning, !quote.CanUpgrade && !maximum);
            _buttonLabel.text = maximum ? "最大レベルです" : "強化内容を確認";
            _button.SetEnabled(quote.CanUpgrade);
            _confirm.SetEnabled(quote.CanUpgrade && _expectedLevel == node.Level);
            RefreshConfirmation();
        }

        private void BeginConfirmation()
        {
            if (_node?.Upgrade.CanUpgrade != true) return;
            _expectedLevel = _node.Level; _confirming = true;
            _confirmationText.text = _node.Upgrade.Free
                ? $"資源を消費せず Lv{_node.Upgrade.NextLevel} に強化します。"
                : $"電気 {_node.Upgrade.ElectricityCost:0} を使って Lv{_node.Upgrade.NextLevel} に強化します。";
            _confirm.SetEnabled(true); RefreshConfirmation();
        }

        private void Commit()
        {
            if (!_confirming || _node?.Upgrade.CanUpgrade != true || _expectedLevel != _node.Level) return;
            _confirm.SetEnabled(false);
            if (_upgrade(_node.Id, _expectedLevel)) { _confirming = false; _node = null; }
            else _confirm.SetEnabled(true);
            RefreshConfirmation();
        }

        private void RefreshConfirmation()
        {
            IslandUi.SetVisible(_confirmation, _confirming);
            IslandUi.SetVisible(_button, !_confirming);
        }
    }
}
