using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProjectPenguin.Editor.Build
{
    /// <summary>
    /// ビルドの実行本体。MenuItem と CLI の両方がここを通る。
    /// 設定の書き換えは <see cref="PlayerSettingsScope"/> の中だけで行い、必ず元へ戻す。
    /// </summary>
    public static class ProjectBuilder
    {
        /// <summary>iOS の署名チーム ID。未設定なら Xcode 側で指定する。</summary>
        public const string AppleTeamIdEnv = "PENGUIN_IOS_TEAM_ID";

        /// <summary>Web ビルドの出力先 (OutputRoot からの相対)。scripts/deploy_web.py の既定の入力 (Builds/WebGL) と揃える。</summary>
        public const string WebGLDirectoryName = "WebGL";

        public static BuildReport Run(BuildRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var scenes = ScenesFor(request);
            if (scenes.Length == 0)
            {
                throw new BuildRequestException(
                    "Build Settings に有効なシーンが 1 つもありません。"
                    + "File > Build Profiles でシーンを追加してください。");
            }

            // 対象の切り替えはここでしない。切り替えたその回のビルドは切り替え前にコンパイルしたスクリプトで走るので、
            // #if UNITY_IOS の IosPostprocess などが抜け、CoreBluetooth のリンクで落ちる Xcode プロジェクトができてしまう。
            // メニューは BuildEntry が切り替えてから再読み込みの後に続け、CLI は -buildTarget で最初から合わせる。
            if (EditorUserBuildSettings.activeBuildTarget != request.Target)
            {
                throw new BuildRequestException(
                    $"いまのビルド先は {EditorUserBuildSettings.activeBuildTarget} です。{request.Target} のスクリプトでビルドするため、"
                    + $"メニューから選び直すか、CLI では -buildTarget {request.Target} を付けてください。");
            }

            using (new PlayerSettingsScope())
            {
                if (!string.IsNullOrWhiteSpace(request.BundleVersion))
                {
                    PlayerSettings.bundleVersion = request.BundleVersion;
                }

                var signing = "—";
                if (request.IsAndroid)
                {
                    if (request.AndroidVersionCode.HasValue)
                    {
                        PlayerSettings.Android.bundleVersionCode = request.AndroidVersionCode.Value;
                    }

                    EditorUserBuildSettings.buildAppBundle = request.Kind == BuildKind.AndroidAab;
                    signing = AndroidSigning.Apply(request);
                }
                else if (request.IsWebGL)
                {
                    ApplyWebGLSettings();
                }
                else
                {
                    PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                    if (!string.IsNullOrWhiteSpace(request.IosBuildNumber))
                    {
                        PlayerSettings.iOS.buildNumber = request.IosBuildNumber;
                    }

                    var team = Environment.GetEnvironmentVariable(AppleTeamIdEnv);
                    if (!string.IsNullOrWhiteSpace(team))
                    {
                        PlayerSettings.iOS.appleDeveloperTeamID = team;
                        signing = $"team {team}";
                    }
                    else
                    {
                        signing = "未設定 (Xcode 側で指定する)";
                    }
                }

                var location = ResolveLocation(request);
                var parent = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                Debug.Log(Describe(request, scenes, location, signing));

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = location,
                    target = request.Target,
                    targetGroup = request.Group,
                    options = request.Development
                        ? BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler
                        : BuildOptions.None,
                });

                LogResult(request, report);
                return report;
            }
        }

        /// <summary>
        /// Web ビルドの配信の設定。公開先 (Vercel) の CLI はアップロードが 100 MB までなので Brotli で縮める。
        /// Vercel が Content-Encoding: br を付ければブラウザが展開し、付かなくても Decompression Fallback で
        /// Unity の JS が展開するので、配信の設定に左右されず必ず起動する (docs/web-deploy.md)。
        /// ファイル名のハッシュ化 (nameFilesAsHashes) は、有効にすると「Backend has requested a buildprogram
        /// run 6 times」でビルドが落ちることがある (実際に 1 回落ちた。https://issuetracker.unity.com/issues/1245)
        /// ので使わない。代わりに vercel.json で Build/ を毎回 ETag で確かめさせ、更新後に古いファイルを読ませない。
        /// </summary>
        private static void ApplyWebGLSettings()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.dataCaching = true;
            // Keep the bridge outside Assets: no generated Unity asset/meta is needed.
            var library = Path.GetFullPath(Path.Combine(Application.dataPath, "../tools/classroom_raid_web/storage.jslib"));
            if (!File.Exists(library)) throw new BuildRequestException("教室レイドのWeb保存同期ライブラリがありません。");
            PlayerSettings.WebGL.emscriptenArgs += " --js-library \"" + library.Replace('\\', '/') + "\"";
        }

        /// <summary>Build Settings で有効になっているシーンだけを、その並び順で返す。</summary>
        public static string[] EnabledScenes() =>
            EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))
                .Select(scene => scene.path)
                .ToArray();

        /// <summary>iOS 版の正規のシーン (村のホーム)。工場ゲームの試作 (FactoryGame.unity) は入れない。</summary>
        public const string IosScene = "Assets/Scenes/ProductionVillageHome.unity";

        /// <summary>iOS は正規のシーンだけを入れる。Web と Android は Build Settings で有効なシーンのまま。</summary>
        public static string[] ScenesFor(BuildRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.IsIos) return EnabledScenes();
            if (!File.Exists(IosScene)) throw new BuildRequestException("iOS 版のシーン (" + IosScene + ") がありません。");
            return new[] { IosScene };
        }

        /// <summary>成果物の出力先。Android はファイル、iOS は Xcode プロジェクト、Web は公開するディレクトリ。</summary>
        public static string ResolveLocation(BuildRequest request)
        {
            var root = string.IsNullOrWhiteSpace(request.OutputRoot) ? "Builds" : request.OutputRoot;

            if (request.IsIos)
            {
                return Path.GetFullPath(Path.Combine(root, "iOS"));
            }

            if (request.IsWebGL)
            {
                return Path.GetFullPath(Path.Combine(root, WebGLDirectoryName));
            }

            var name = SafeFileName(PlayerSettings.productName);
            var version = SafeFileName(PlayerSettings.bundleVersion);
            var suffix = request.Development ? "-dev" : string.Empty;
            var extension = request.Kind == BuildKind.AndroidAab ? "aab" : "apk";

            return Path.GetFullPath(Path.Combine(root, "Android", $"{name}-{version}{suffix}.{extension}"));
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "build";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                builder.Append(invalid.Contains(c) || char.IsWhiteSpace(c) ? '-' : c);
            }

            return builder.ToString();
        }

        private static string Describe(BuildRequest request, string[] scenes, string location, string signing)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[Build] {request.Label} を開始します。");
            builder.AppendLine($"  出力先        : {location}");
            builder.AppendLine($"  バージョン    : {PlayerSettings.bundleVersion}" + (request.Kind switch
            {
                BuildKind.IosXcode => $" (build {PlayerSettings.iOS.buildNumber})",
                BuildKind.WebGL => $" (圧縮 {PlayerSettings.WebGL.compressionFormat})",
                _ => $" (versionCode {PlayerSettings.Android.bundleVersionCode})",
            }));
            builder.AppendLine($"  Development   : {(request.Development ? "あり" : "なし")}");
            builder.AppendLine($"  署名          : {signing}");
            builder.AppendLine($"  スクリプト    : {PlayerSettings.GetScriptingBackend(request.NamedTarget)}");
            builder.AppendLine($"  シーン ({scenes.Length}) : {string.Join(", ", scenes)}");
            return builder.ToString().TrimEnd();
        }

        private static void LogResult(BuildRequest request, BuildReport report)
        {
            var summary = report.summary;
            // TimeSpan の書式指定子はコロンをエスケープする必要がある。補間文字列に
            // 直接書くとエスケープが二重になるため、先に文字列へ落とす。
            var elapsed = summary.totalTime.ToString(@"hh\:mm\:ss");
            var message =
                $"[Build] {request.Label} : {summary.result} / "
                + $"{summary.totalSize / (1024f * 1024f):F1} MB / {elapsed}\n"
                + $"  {summary.outputPath}";

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log(message);
                return;
            }

            // 失敗の原因は BuildReport のログに入っている。要約だけでは追えないので抜き出す。
            var errors = report.steps
                .SelectMany(step => step.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => "  " + m.content)
                .Take(20);

            Debug.LogError(message + "\n" + string.Join("\n", errors));
        }
    }
}
