using UnityEditor;
using UnityEditor.Build;

namespace ProjectPenguin.Editor.Build
{
    /// <summary>ビルド成果物の種類。</summary>
    public enum BuildKind
    {
        /// <summary>実機で直接動かして確認するための APK。</summary>
        AndroidApk,

        /// <summary>Google Play に上げるための AAB。署名必須。</summary>
        AndroidAab,

        /// <summary>Xcode プロジェクト。実機ビルドは macOS 上の Xcode で行う。</summary>
        IosXcode,

        /// <summary>ブラウザで動く Web 版。Vercel に置いて審査員・観客に触ってもらう (docs/web-deploy.md)。</summary>
        WebGL,
    }

    /// <summary>
    /// ビルド 1 回分の入力。MenuItem からも CLI 引数からもここに集約してから
    /// <see cref="ProjectBuilder"/> に渡す。分岐をこの型の中だけに閉じる。
    /// </summary>
    public sealed class BuildRequest
    {
        public BuildKind Kind { get; set; }

        /// <summary>Development Build (プロファイラ接続・デバッグシンボルあり)。</summary>
        public bool Development { get; set; }

        /// <summary>成果物を置くルート。リポジトリ直下からの相対パス。</summary>
        public string OutputRoot { get; set; } = "Builds";

        /// <summary>null なら ProjectSettings の bundleVersion をそのまま使う。</summary>
        public string BundleVersion { get; set; }

        /// <summary>null なら ProjectSettings の versionCode をそのまま使う。</summary>
        public int? AndroidVersionCode { get; set; }

        /// <summary>
        /// iOS のビルド番号 (CFBundleVersion)。null なら ProjectSettings の値をそのまま使う。
        /// TestFlight は同じバージョンの中でビルド番号が重複したビルドを受け付けない。
        /// </summary>
        public string IosBuildNumber { get; set; }

        public bool IsAndroid => Kind == BuildKind.AndroidApk || Kind == BuildKind.AndroidAab;

        public bool IsIos => Kind == BuildKind.IosXcode;

        public bool IsWebGL => Kind == BuildKind.WebGL;

        public BuildTarget Target => Kind switch
        {
            BuildKind.IosXcode => BuildTarget.iOS,
            BuildKind.WebGL => BuildTarget.WebGL,
            _ => BuildTarget.Android,
        };

        public BuildTargetGroup Group => Kind switch
        {
            BuildKind.IosXcode => BuildTargetGroup.iOS,
            BuildKind.WebGL => BuildTargetGroup.WebGL,
            _ => BuildTargetGroup.Android,
        };

        public NamedBuildTarget NamedTarget => NamedBuildTarget.FromBuildTargetGroup(Group);

        /// <summary>ログと成果物名に使う短い識別子。</summary>
        public string Label => Kind switch
        {
            BuildKind.AndroidApk => "Android APK",
            BuildKind.AndroidAab => "Android AAB",
            BuildKind.WebGL => "Web (WebGL)",
            _ => "iOS Xcode",
        };
    }
}
