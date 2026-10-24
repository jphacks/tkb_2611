using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using ProjectPenguin.Presentation.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>
    /// <see cref="UiNames"/> と <see cref="UiClasses"/> の定数が、UXML と USS に実在するかを確かめる。
    /// 名前を変えたのに定数を直し忘れると、実行時に要素が見つからない (null) か、見た目が付かないだけで気づきにくいため。
    /// </summary>
    public sealed class UiNamesTests
    {
        private const string UiRoot = "Assets/UI";

        private static IEnumerable<Type> NameGroups =>
            typeof(UiNames).GetNestedTypes(BindingFlags.Public).Where(t => t.GetCustomAttribute<UxmlFileAttribute>() != null);

        private static IEnumerable<TestCaseData> NameGroupCases =>
            NameGroups.Select(t => new TestCaseData(t).SetName($"UiNames.{t.Name}"));

        [Test]
        public void EveryNameGroupDeclaresItsUxml()
        {
            var missing = typeof(UiNames).GetNestedTypes(BindingFlags.Public)
                .Where(t => t.GetCustomAttribute<UxmlFileAttribute>() == null)
                .Select(t => t.Name)
                .ToList();
            Assert.That(missing, Is.Empty, "UxmlFile 属性のない入れ子のクラスがあります");
        }

        [TestCaseSource(nameof(NameGroupCases))]
        public void EveryNameExistsInItsUxml(Type group)
        {
            var path = group.GetCustomAttribute<UxmlFileAttribute>().Path;
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(tree, Is.Not.Null, $"{path} が読み込めません");
            var root = tree.Instantiate();

            var missing = Constants(group).Where(kv => root.Q(kv.Value) == null).Select(kv => $"{kv.Key} = \"{kv.Value}\"").ToList();
            Assert.That(missing, Is.Empty, $"{path} にない要素名があります");
        }

        [TestCaseSource(nameof(NameGroupCases))]
        public void EveryNameInUxmlHasConstant(Type group)
        {
            var path = group.GetCustomAttribute<UxmlFileAttribute>().Path;
            var defined = new HashSet<string>(Constants(group).Values);
            var missing = XDocument.Load(path).Descendants()
                .Where(e => e.Name.LocalName != "Template")
                .Select(e => (string)e.Attribute("name"))
                .Where(n => n != null && !defined.Contains(n))
                .Distinct()
                .ToList();
            Assert.That(missing, Is.Empty, $"{path} の要素名のうち、UiNames.{group.Name} に定数がないもの (足してください)");
        }

        [Test]
        public void EveryClassExistsInUssOrUxml()
        {
            var known = new HashSet<string>();
            foreach (var uss in Directory.GetFiles(UiRoot, "*.uss", SearchOption.AllDirectories))
            {
                var text = Regex.Replace(File.ReadAllText(uss), @"/\*.*?\*/", "", RegexOptions.Singleline);
                text = Regex.Replace(text, @"url\([^)]*\)", "");
                foreach (Match m in Regex.Matches(text, @"\.(-?[A-Za-z_][\w-]*)"))
                {
                    known.Add(m.Groups[1].Value);
                }
            }

            foreach (var uxml in Directory.GetFiles(UiRoot, "*.uxml", SearchOption.AllDirectories))
            {
                foreach (var e in XDocument.Load(uxml).Descendants())
                {
                    var classes = (string)e.Attribute("class");
                    if (classes != null)
                    {
                        known.UnionWith(classes.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                    }
                }
            }

            var missing = AllConstants(typeof(UiClasses)).Where(kv => !known.Contains(kv.Value))
                .Select(kv => $"{kv.Key} = \"{kv.Value}\"").ToList();
            Assert.That(missing, Is.Empty, "USS にも UXML にもないクラス名があります");
        }

        [Test]
        public void ClassValuesAreUnique()
        {
            var duplicates = AllConstants(typeof(UiClasses)).GroupBy(kv => kv.Value).Where(g => g.Count() > 1)
                .Select(g => $"\"{g.Key}\": {string.Join(", ", g.Select(kv => kv.Key))}").ToList();
            Assert.That(duplicates, Is.Empty, "同じクラス名に定数が 2 つあります");
        }

        private static Dictionary<string, string> Constants(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue());

        /// <summary>入れ子のクラスも含めた const 文字列 (キーは UiClasses.Row.Body のような完全な名前)。</summary>
        private static IEnumerable<KeyValuePair<string, string>> AllConstants(Type type, string prefix = null)
        {
            prefix ??= type.Name;
            foreach (var kv in Constants(type))
            {
                yield return new KeyValuePair<string, string>($"{prefix}.{kv.Key}", kv.Value);
            }

            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
            {
                foreach (var kv in AllConstants(nested, $"{prefix}.{nested.Name}"))
                {
                    yield return kv;
                }
            }
        }
    }
}
