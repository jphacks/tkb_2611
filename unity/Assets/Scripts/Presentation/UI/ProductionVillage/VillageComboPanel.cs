using ProjectPenguin.Domain.Village;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    public sealed class VillageComboPanel
    {
        public const string SummaryName = "villageComboSummary";
        public const string CenterName = "villageComboCenter";
        public const string HintName = "villageComboHint";
        private readonly Label _summary, _center, _hint;
        private readonly Label[] _directions = new Label[4];
        private static readonly VillageComboDirection[] Directions = { VillageComboDirection.Up, VillageComboDirection.Right, VillageComboDirection.Down, VillageComboDirection.Left };
        private static readonly string[] Names = { "上", "右", "下", "左" };

        public VillageComboPanel(VisualElement parent)
        {
            parent.Add(IslandUi.Text("コンボ", ProductionUiNames.CardTitle));
            _summary = IslandUi.Text("", ProductionUiNames.Subtle);
            _summary.name = SummaryName;
            parent.Add(_summary);
            var diagram = new VisualElement(); diagram.AddToClassList(ProductionUiNames.ComboDiagram); parent.Add(diagram);
            _center = IslandUi.Text("", ProductionUiNames.ComboCenter); _center.name = CenterName;
            for (var i = 0; i < 4; i++)
            {
                _directions[i] = IslandUi.Text("", ProductionUiNames.ComboDirection);
                _directions[i].name = DirectionName(Directions[i]);
            }
            Row(diagram, new VisualElement(), _directions[0], new VisualElement());
            Row(diagram, _directions[3], _center, _directions[1]);
            Row(diagram, new VisualElement(), _directions[2], new VisualElement());
            _hint = IslandUi.Text("", ProductionUiNames.ComboHint); _hint.name = HintName; parent.Add(_hint);
            parent.Add(IslandUi.Text("上下左右の同種施設1棟につき生産量+25%、4方向すべて成立すると最大2倍。斜めは対象外です。加工中の数量は変えず、小数分は次回へ持ち越します。", ProductionUiNames.Subtle));
            Render(null);
        }

        public static string DirectionName(VillageComboDirection direction) => "villageCombo" + direction;
        private static void Row(VisualElement parent, params VisualElement[] cells)
        {
            var row = new VisualElement(); row.AddToClassList(ProductionUiNames.ComboRow); parent.Add(row);
            foreach (var cell in cells) { cell.AddToClassList(ProductionUiNames.ComboCell); row.Add(cell); }
        }

        public void Render(VillageNodeView node)
        {
            _summary.text = node == null ? "施設を選んでください"
                : $"隣接 {node.ComboCount}棟 · 生産量 ×{node.ComboMultiplier:0.##}（+{node.ComboCount * VillageComboRules.PercentPerNeighbor}%）";
            var maximum = node?.ComboCount == VillageComboRules.MaxNeighbors;
            _center.text = node == null ? "選択施設" : $"×{node.ComboMultiplier:0.##}\n{node.ComboCount}/4方向";
            _center.EnableInClassList(ProductionUiNames.ComboMaximum, maximum);
            _hint.text = node == null ? "上下左右の成立状況を表示します"
                : maximum ? "4方向成立・最大 ×2"
                : $"あと{VillageComboRules.MaxNeighbors - node.ComboCount}方向で最大 ×2";
            for (var i = 0; i < 4; i++)
            {
                var active = node != null && (node.ComboDirections & Directions[i]) != 0;
                _directions[i].text = $"{Names[i]}\n" + (active ? "成立 +25%" : "未成立");
                _directions[i].EnableInClassList(ProductionUiNames.ComboDirectionActive, active);
            }
        }
    }
}
