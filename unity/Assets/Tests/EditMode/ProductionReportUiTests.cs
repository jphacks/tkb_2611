using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using ProjectPenguin.Domain.Island;
using ProjectPenguin.Domain.Village;
using ProjectPenguin.Presentation.UI.ProductionVillage;
using UnityEditor;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>
    /// 全画面生産レポート (ProductionReport.uxml / ProductionReport.uss / Industrial.uss) の
    /// 要素名・クラス名の整合性と、生色不使用ルールを検査する。
    /// </summary>
    public sealed class ProductionReportUiTests
    {
        private const string UxmlPath = "Assets/UI/ProductionVillage/Report/ProductionReport.uxml";
        private const string ReportUssPath = "Assets/UI/ProductionVillage/Report/ProductionReport.uss";
        private const string IndustrialUssPath = "Assets/UI/DesignSystem/Industrial.uss";

        private static Dictionary<string, string> ElementNames() =>
            typeof(ProductionReportNames).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (f.Name, Value: (string)f.GetRawConstantValue()))
                .Where(c => !c.Name.EndsWith("Class"))
                .ToDictionary(c => c.Name, c => c.Value);

        private static Dictionary<string, string> ClassNames() =>
            typeof(ProductionReportNames).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (f.Name, Value: (string)f.GetRawConstantValue()))
                .Where(c => c.Name.EndsWith("Class"))
                .ToDictionary(c => c.Name, c => c.Value);

        [Test]
        public void EveryElementNameExistsInUxml()
        {
            var doc = XDocument.Load(UxmlPath);
            var namesInUxml = new HashSet<string>(doc.Descendants().Select(e => (string)e.Attribute("name")).Where(n => n != null));

            var missing = ElementNames().Where(kv => !namesInUxml.Contains(kv.Value)).Select(kv => $"{kv.Key} ({kv.Value})").ToList();
            Assert.That(missing, Is.Empty, "ProductionReportNames に定義されているが UXML に存在しない要素名があります");
        }

        [Test]
        public void EveryNameInUxmlHasConstant()
        {
            var defined = new HashSet<string>(ElementNames().Values);
            var doc = XDocument.Load(UxmlPath);
            var missing = doc.Descendants().Select(e => (string)e.Attribute("name"))
                .Where(n => n != null && !defined.Contains(n)).ToList();
            Assert.That(missing, Is.Empty, "ProductionReport.uxml にあるが ProductionReportNames に定数のない要素名があります");
        }

        [Test]
        public void EveryClassExistsInUss()
        {
            var allUss = string.Join("\n", Directory.GetFiles("Assets/UI", "*.uss", SearchOption.AllDirectories).Select(File.ReadAllText));
            var missing = ClassNames().Where(kv => !Regex.IsMatch(allUss, @"\." + Regex.Escape(kv.Value) + @"(?![\w-])")).Select(kv => $"{kv.Key} ({kv.Value})").ToList();
            Assert.That(missing, Is.Empty, "USS にないクラス名の定数があります");
        }

        [Test]
        public void ReportUssUsesTokensInsteadOfRawColors()
        {
            var text = Regex.Replace(File.ReadAllText(ReportUssPath), @"/\*.*?\*/", "", RegexOptions.Singleline);
            var matches = Regex.Matches(text, @"#[0-9A-Fa-f]{3,8}\b|rgba?\(").Cast<Match>().Select(m => m.Value).ToList();
            Assert.That(matches, Is.Empty, "ProductionReport.uss に生の色が書かれています。var(--ind-*) / var(--color-*) を使用してください");
        }

        [Test]
        public void IndustrialUssUsesTokensInsteadOfRawColors()
        {
            var text = Regex.Replace(File.ReadAllText(IndustrialUssPath), @"/\*.*?\*/", "", RegexOptions.Singleline);
            var matches = Regex.Matches(text, @"#[0-9A-Fa-f]{3,8}\b|rgba?\(").Cast<Match>().Select(m => m.Value).ToList();
            Assert.That(matches, Is.Empty, "Industrial.uss に生の色が書かれています。var(--ind-*) / var(--color-*) を使用してください");
        }
    }
}
