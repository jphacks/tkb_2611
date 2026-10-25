using NUnit.Framework;
using ProjectPenguin.Editor.Build;

namespace ProjectPenguin.Tests.EditMode
{
    /// <summary>
    /// CLI 引数から <see cref="BuildRequest"/> を作る部分。ビルドそのものは重いので回さず、
    /// 入力の読み違いと検証漏れだけを確かめる。
    /// </summary>
    public sealed class BuildEntryTests
    {
        [Test]
        public void IosStartsTheNewFactoryWithoutChangingTheLegacyWebScenes()
        {
            Assert.That(ProjectBuilder.ScenesFor(new BuildRequest { Kind = BuildKind.IosXcode }),
                Is.EqualTo(new[] { "Assets/Scenes/FactoryGame.unity" }));
            Assert.That(ProjectBuilder.ScenesFor(new BuildRequest { Kind = BuildKind.WebGL }), Is.EqualTo(ProjectBuilder.EnabledScenes()));
        }
        private static BuildRequest ParseIos(params string[] args) => BuildEntry.Parse(BuildKind.IosXcode, args);

        [Test]
        public void BuildNumberIsReadAfterUnityOwnArguments()
        {
            var request = ParseIos(
                "-batchmode", "-projectPath", ".", "-executeMethod",
                "ProjectPenguin.Editor.Build.BuildEntry.IosXcode", "--", "--build-number", "5");

            Assert.That(request.IosBuildNumber, Is.EqualTo("5"));
        }

        [Test]
        public void BuildNumberIsNullWhenOmitted()
        {
            Assert.That(ParseIos("--dev").IosBuildNumber, Is.Null);
        }

        [TestCase("5")]
        [TestCase("1.0")]
        [TestCase("1.0.3")]
        [TestCase("0")]
        public void AppleFormatIsAccepted(string value)
        {
            Assert.That(ParseIos("--build-number", value).IosBuildNumber, Is.EqualTo(value));
        }

        [TestCase("1.0.3.4")]
        [TestCase("1.")]
        [TestCase(".1")]
        [TestCase("1..2")]
        [TestCase("v5")]
        [TestCase("1.0-beta")]
        [TestCase("")]
        public void OtherFormatsAreRejected(string value)
        {
            Assert.Throws<BuildRequestException>(() => ParseIos("--build-number", value));
        }

        [Test]
        public void MissingValueIsRejected()
        {
            Assert.Throws<BuildRequestException>(() => ParseIos("--build-number"));
            Assert.Throws<BuildRequestException>(() => ParseIos("--build-number", "--dev"));
        }

        [Test]
        public void OtherOptionsStillWorkTogether()
        {
            var request = ParseIos("--dev", "--bundle-version", "0.2.0", "--build-number", "12", "--output", "Out");

            Assert.That(request.Development, Is.True);
            Assert.That(request.BundleVersion, Is.EqualTo("0.2.0"));
            Assert.That(request.IosBuildNumber, Is.EqualTo("12"));
            Assert.That(request.OutputRoot, Is.EqualTo("Out"));
        }

        [Test]
        public void VersionCodeMustBeInteger()
        {
            Assert.That(BuildEntry.Parse(BuildKind.AndroidApk, new[] { "--version-code", "7" }).AndroidVersionCode,
                Is.EqualTo(7));
            Assert.Throws<BuildRequestException>(
                () => BuildEntry.Parse(BuildKind.AndroidApk, new[] { "--version-code", "7a" }));
        }

        [Test]
        public void WebGLTargetsTheWebPlatform()
        {
            var request = BuildEntry.Parse(BuildKind.WebGL, new[] { "--dev" });

            Assert.That(request.Target, Is.EqualTo(UnityEditor.BuildTarget.WebGL));
            Assert.That(request.Group, Is.EqualTo(UnityEditor.BuildTargetGroup.WebGL));
            Assert.That(request.IsWebGL, Is.True);
            Assert.That(request.IsAndroid, Is.False);
            Assert.That(request.IsIos, Is.False);
            Assert.That(request.Development, Is.True);
        }

        [Test]
        public void WebGLIsWrittenToItsOwnDirectory()
        {
            var request = BuildEntry.Parse(BuildKind.WebGL, new[] { "--output", "Out" });

            Assert.That(ProjectBuilder.ResolveLocation(request),
                Is.EqualTo(System.IO.Path.GetFullPath(System.IO.Path.Combine("Out", "WebGL"))));
        }

        [TestCase(BuildKind.AndroidApk, UnityEditor.BuildTarget.Android)]
        [TestCase(BuildKind.AndroidAab, UnityEditor.BuildTarget.Android)]
        [TestCase(BuildKind.IosXcode, UnityEditor.BuildTarget.iOS)]
        [TestCase(BuildKind.WebGL, UnityEditor.BuildTarget.WebGL)]
        public void EachKindHasItsTarget(BuildKind kind, UnityEditor.BuildTarget target)
        {
            Assert.That(new BuildRequest { Kind = kind }.Target, Is.EqualTo(target));
        }
    }
}
