using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>
    /// 村の画面には絵文字を使わない。印が要るときは、お魚などと同じデザインシステムのアイコン (Assets/UI/DesignSystem/Icons) を使う。
    /// コメントは対象外。
    /// </summary>
    public sealed class VillageNoEmojiTests
    {
        // 絵文字と、絵文字として表示される記号 (✨⚡▶✕✓↻ など)
        private static readonly Regex Emoji = new Regex(@"[←-⇿⌀-⏿■-◿☀-➿⬀-⯿]|\uD83C[\uDC00-\uDFFF]|\uD83D[\uDC00-\uDFFF]|\uD83E[\uDC00-\uDFFF]");

        private static readonly string[] Files =
        {
            "Assets/Scripts/Presentation/UI/ProductionVillage/ProductionNavigator.cs",
            "Assets/UI/ProductionVillage/ProductionApp.uxml",
            "Assets/Scripts/Domain/Village/VillageInsight.cs",
        };

        [Test]
        public void VillageScreensUseIconsInsteadOfEmoji()
        {
            var found = Files.SelectMany(path => File.ReadAllLines(path).Select((line, i) => (path, line, i)))
                .Where(x => !IsComment(x.line) && Emoji.IsMatch(x.line))
                .Select(x => $"{x.path}:{x.i + 1}: {x.line.Trim()}")
                .ToArray();
            Assert.That(found, Is.Empty, "絵文字の代わりに pp-icon のアイコンを使ってください");
        }

        private static bool IsComment(string line)
        {
            var t = line.Trim();
            return t.StartsWith("//") || t.StartsWith("<!--") || t.StartsWith("*") || t.StartsWith("/*");
        }
    }
}
