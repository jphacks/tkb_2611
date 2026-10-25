using System.Linq;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectPenguin.Editor.ProductionIsland
{
    /// <summary>Interactive review of the real effort widget, using an isolated in-memory village.</summary>
    public sealed class VillageEffortPreview : EditorWindow
    {
        private VillageGame _game;
        private VillageEffortPanel _panel;
        private VisualElement _card;
        private EnumField _kind;
        private DoubleField _gas;

        [MenuItem("Project Penguin/Village/Preview Facility Effort")]
        public static void Open()
        {
            var window = GetWindow<VillageEffortPreview>("エフォートの確認");
            window.minSize = new Vector2(560, 650);
            window.Show();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();
            foreach (var path in new[] { "Assets/UI/DesignSystem/Tokens.uss", "Assets/UI/DesignSystem/Components.uss", "Assets/UI/ProductionVillage/ProductionApp.uss" })
                root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(path));
            root.style.unityFontDefinition = FontDefinition.FromFont(AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/MPLUSRounded1c-Regular.ttf"));
            root.Add(new HelpBox("ProductionVillageHomeと同じエフォートUIです。この確認用データはゲームの保存に書き込みません。", HelpBoxMessageType.Info));
            _kind = new EnumField("施設", FacilityKind.Fishery);
            _kind.RegisterValueChangedCallback(_ => Reset()); root.Add(_kind);
            _gas = new DoubleField("所持しているガス") { value = 100 };
            _gas.RegisterValueChangedCallback(_ =>
            {
                if (_game == null || !VillageGame.Finite(_gas.value) || _gas.value < 0 || _gas.value > int.MaxValue) return;
                _game.SetResources(_gas.value, 100, 100); Render();
            });
            root.Add(_gas);
            var reset = new Button(Reset) { text = "0から試す" }; root.Add(reset);
            var scroll = new ScrollView(); scroll.style.flexGrow = 1; root.Add(scroll);
            _card = new VisualElement(); _card.AddToClassList(UiClasses.Card); _card.AddToClassList(ProductionUiNames.Card);
            scroll.Add(_card); Reset();
        }

        private void Reset()
        {
            if (_card == null) return;
            _game = new VillageGame(false); _game.SetResources(100, 10000, 100);
            _game.Place((FacilityKind)_kind.value, 0, 0);
            _game.SetResources(100, 100, 100); _gas.SetValueWithoutNotify(100);
            _card.Clear();
            _panel = new VillageEffortPanel(_card, (id, effort) =>
            {
                var result = _game.SetEffort(id, effort);
                if (!result.Success) return false;
                _card.schedule.Execute(Render);
                return true;
            });
            Render();
        }

        private void Render()
        {
            var view = _game.CreateView(); _panel.Render(view.Nodes.Single(), view.Gas);
            _gas.SetValueWithoutNotify(view.Gas);
        }
    }
}
