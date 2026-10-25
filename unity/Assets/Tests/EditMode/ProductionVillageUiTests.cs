using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using ProjectPenguin.Presentation.World.ProductionVillage;
using UnityEditor;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>
    /// 村のホーム (ProductionApp.uxml / .uss) の名前とクラスが <see cref="ProductionUiNames"/> と食い違っていないか、
    /// レイアウト用の USS に色を直書きしていないか、カメラの寄りが島を画面に収めるかを確かめる。
    /// </summary>
    public sealed class ProductionVillageUiTests
    {
        private const string UxmlPath = "Assets/UI/ProductionVillage/ProductionApp.uxml";
        private const string UssPath = "Assets/UI/ProductionVillage/ProductionApp.uss";

        /// <summary>要素名 ("villageFish" のようにハイフンが無い) と、クラス名 ("village-hint" のようにハイフンがある) に分ける。</summary>
        private static Dictionary<string, string> Constants(bool classes) =>
            typeof(ProductionUiNames).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (f.Name, Value: (string)f.GetRawConstantValue()))
                .Where(c => c.Value.Contains('-') == classes)
                .ToDictionary(c => c.Name, c => c.Value);

        [Test]
        public void EveryNameExistsInUxml()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath).Instantiate();
            var missing = Constants(false).Where(kv => root.Q(kv.Value) == null).Select(kv => kv.Key).ToList();
            Assert.That(missing, Is.Empty, "UXML にない要素名の定数があります");
        }

        [Test]
        public void EveryNameInUxmlHasConstant()
        {
            var defined = new HashSet<string>(Constants(false).Values);
            var missing = XDocument.Load(UxmlPath).Descendants()
                .Where(e => e.Name.LocalName != "Template")
                .Select(e => (string)e.Attribute("name"))
                .Where(n => n != null && !defined.Contains(n)).ToList();
            Assert.That(missing, Is.Empty, "ProductionUiNames に定数のない要素名があります");
        }

        [Test]
        public void EveryClassExistsInUss()
        {
            var css = string.Join("\n", Directory.GetFiles("Assets/UI", "*.uss", SearchOption.AllDirectories).Select(File.ReadAllText));
            var missing = Constants(true).Where(kv => !Regex.IsMatch(css, @"\." + Regex.Escape(kv.Value) + @"(?![\w-])")).Select(kv => kv.Key).ToList();
            Assert.That(missing, Is.Empty, "USS にないクラス名の定数があります");
        }

        [Test]
        public void LayoutUssUsesTokensInsteadOfRawColors()
        {
            var text = Regex.Replace(File.ReadAllText(UssPath), @"/\*.*?\*/", "", RegexOptions.Singleline);
            Assert.That(Regex.Matches(text, @"#[0-9A-Fa-f]{3,8}\b|rgba?\(").Count, Is.Zero, "色はトークン (var(--color-*)) で指定してください");
        }

        [TestCase(1080f / 1920f)]
        [TestCase(1080f / 2400f)]
        [TestCase(1170f / 2532f)]
        [TestCase(16f / 9f)]
        public void CameraFitShowsWholeIsland(float aspect)
        {
            const float pitch = 55f;
            var half = VillageCameraController.IslandHalf;
            var size = VillageCameraController.FitSizeFor(aspect, half, pitch, 1f);
            Assert.That(size * aspect, Is.GreaterThanOrEqualTo(half - 1e-4f), "島の幅が画面からはみ出します");
            var depth = half * UnityEngine.Mathf.Sin(pitch * UnityEngine.Mathf.Deg2Rad);
            Assert.That(depth, Is.LessThanOrEqualTo(size * .6f + 1e-4f), "島の奥行きが画面の高さの 6 割を超えます");
        }
    }
}
