using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

[assembly: InternalsVisibleTo("ProjectPenguin.Tests.EditMode")]

namespace ProjectPenguin.Editor.Build
{
    /// <summary>
    /// ビルドの入口。Editor のメニューと CLI の -executeMethod が両方ここに来る。
    ///
    ///   Unity -quit なしで実行し、終了コードはこちらの EditorApplication.Exit で返す:
    ///     Unity -batchmode -nographics -projectPath . -logFile - \
    ///           -executeMethod ProjectPenguin.Editor.Build.BuildEntry.IosXcode -- --dev --build-number 5
    ///
    ///   batch mode ではビルド先の切り替え (SwitchActiveBuildTarget) が効かないので、-buildTarget を必ず付ける (#70):
    ///     Unity -batchmode -nographics -projectPath . -logFile - -buildTarget WebGL \
    ///           -executeMethod ProjectPenguin.Editor.Build.BuildEntry.WebGL
    /// </summary>
    public static class BuildEntry
    {
        private const string MenuRoot = "Build/";

        /// <summary>Apple のビルド番号の形式。ピリオド区切りで最大 3 つの非負整数。</summary>
        private static readonly Regex IosBuildNumberPattern = new Regex(@"^[0-9]+(\.[0-9]+){0,2}$");

        // ---- Editor メニュー -------------------------------------------------

        [MenuItem(MenuRoot + "Android APK (Development)", priority = 10)]
        public static void MenuAndroidApkDevelopment() =>
            RunFromMenu(new BuildRequest { Kind = BuildKind.AndroidApk, Development = true });

        [MenuItem(MenuRoot + "Android APK", priority = 11)]
        public static void MenuAndroidApk() =>
            RunFromMenu(new BuildRequest { Kind = BuildKind.AndroidApk });

        [MenuItem(MenuRoot + "Android AAB (配布用)", priority = 12)]
        public static void MenuAndroidAab() =>
            RunFromMenu(new BuildRequest { Kind = BuildKind.AndroidAab });

        [MenuItem(MenuRoot + "iOS Xcode プロジェクト", priority = 30)]
        public static void MenuIos() =>
            RunFromMenu(new BuildRequest { Kind = BuildKind.IosXcode });

        [MenuItem(MenuRoot + "Web (WebGL)", priority = 50)]
        public static void MenuWebGL() =>
            RunFromMenu(new BuildRequest { Kind = BuildKind.WebGL });

        [MenuItem(MenuRoot + "Web (WebGL, Development)", priority = 51)]
        public static void MenuWebGLDevelopment() =>
            RunFromMenu(new BuildRequest { Kind = BuildKind.WebGL, Development = true });

        [MenuItem(MenuRoot + "出力先を開く", priority = 100)]
        public static void MenuRevealOutput()
        {
            var root = Path.GetFullPath("Builds");
            Directory.CreateDirectory(root);
            EditorUtility.RevealInFinder(root);
        }

        // ---- CLI (-executeMethod) -------------------------------------------

        public static void AndroidApk() => RunFromCommandLine(BuildKind.AndroidApk);

        public static void AndroidAab() => RunFromCommandLine(BuildKind.AndroidAab);

        public static void IosXcode() => RunFromCommandLine(BuildKind.IosXcode);

        public static void WebGL() => RunFromCommandLine(BuildKind.WebGL);

        // ---- 実行 -------------------------------------------------------------

        /// <summary>ビルド先を切り替えた後、再読み込みの後に続けるメニューのビルド (Kind と Development)。</summary>
        private const string PendingMenuBuildKey = "ProjectPenguin.Build.PendingMenuBuild";

        private static void RunFromMenu(BuildRequest request)
        {
            // 未保存のシーンはビルドに含まれない。取りこぼしを防ぐため先に確認する。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != request.Target)
            {
                // clone した直後は iOS ではない。切り替えるとスクリプトがコンパイルし直されるので、
                // 再読み込みの後 (ResumePendingMenuBuild) に、切り替え先のスクリプトでビルドを続ける。
                SessionState.SetString(PendingMenuBuildKey, $"{request.Kind}|{request.Development}");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(request.Group, request.Target))
                {
                    SessionState.EraseString(PendingMenuBuildKey);
                    EditorUtility.DisplayDialog("ビルドできません",
                        $"{request.Target} へ切り替えられませんでした。Unity Hub で該当プラットフォームのビルドモジュールを追加してください。", "OK");
                    return;
                }

                // ここで続けない。再読み込みの前に走ると、切り替え前のスクリプトでビルドしてしまう。
                Debug.Log($"[Build] ビルド先を {request.Target} に切り替えました。スクリプトのコンパイルが終わったら {request.Label} のビルドを続けます。");
                return;
            }

            SessionState.EraseString(PendingMenuBuildKey);

            try
            {
                var report = ProjectBuilder.Run(request);
                if (report.summary.result == BuildResult.Succeeded)
                {
                    EditorUtility.RevealInFinder(report.summary.outputPath);
                }
            }
            catch (BuildRequestException e)
            {
                // 入力不備はダイアログで直接伝える。Console だけだと気付かれない。
                EditorUtility.DisplayDialog("ビルドできません", e.Message, "OK");
            }
        }

        [InitializeOnLoadMethod]
        private static void ScheduleResume()
        {
            if (!Application.isBatchMode && !string.IsNullOrEmpty(SessionState.GetString(PendingMenuBuildKey, string.Empty)))
            {
                EditorApplication.delayCall += ResumePendingMenuBuild;
            }
        }

        private static void ResumePendingMenuBuild()
        {
            var pending = SessionState.GetString(PendingMenuBuildKey, string.Empty);
            if (string.IsNullOrEmpty(pending))
            {
                return;
            }

            SessionState.EraseString(PendingMenuBuildKey);
            var parts = pending.Split('|');
            if (parts.Length != 2 || !Enum.TryParse(parts[0], out BuildKind kind) || !bool.TryParse(parts[1], out var development))
            {
                return;
            }

            var request = new BuildRequest { Kind = kind, Development = development };
            if (EditorUserBuildSettings.activeBuildTarget != request.Target)
            {
                // ここでもう一度切り替えると、再読み込みのたびに切り替えを繰り返すので止める。
                Debug.LogError($"[Build] ビルド先が {request.Target} になっていないので、{request.Label} のビルドを続けませんでした。メニューから選び直してください。");
                return;
            }

            RunFromMenu(request);
        }

        private static void RunFromCommandLine(BuildKind kind)
        {
            var succeeded = false;
            try
            {
                var request = Parse(kind, Environment.GetCommandLineArgs());
                succeeded = ProjectBuilder.Run(request).summary.result == BuildResult.Succeeded;
            }
            catch (BuildRequestException e)
            {
                Debug.LogError($"[Build] {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Build] 予期しない失敗: {e}");
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(succeeded ? 0 : 1);
            }
        }

        /// <summary>
        /// CLI 引数を <see cref="BuildRequest"/> にする。Unity 自身のオプションと
        /// 混ざるため、認識できない引数は黙って読み飛ばす。
        /// </summary>
        internal static BuildRequest Parse(BuildKind kind, string[] args)
        {
            var request = new BuildRequest { Kind = kind };

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--dev":
                    case "--development":
                        request.Development = true;
                        break;
                    case "--output":
                        request.OutputRoot = Next(args, i++, "--output");
                        break;
                    case "--bundle-version":
                        request.BundleVersion = Next(args, i++, "--bundle-version");
                        break;
                    case "--version-code":
                        var raw = Next(args, i++, "--version-code");
                        if (!int.TryParse(raw, out var code))
                        {
                            throw new BuildRequestException($"--version-code に整数以外が渡されました: {raw}");
                        }

                        request.AndroidVersionCode = code;
                        break;
                    case "--build-number":
                        var number = Next(args, i++, "--build-number");
                        if (!IsValidIosBuildNumber(number))
                        {
                            throw new BuildRequestException(
                                $"--build-number は「5」「1.0.3」のような、ピリオド区切りで最大 3 つの数字にしてください: {number}");
                        }

                        request.IosBuildNumber = number;
                        break;
                }
            }

            return request;
        }

        /// <summary>CFBundleVersion として受け付けられる形式か。</summary>
        internal static bool IsValidIosBuildNumber(string value) =>
            value != null && IosBuildNumberPattern.IsMatch(value);

        private static string Next(string[] args, int index, string option)
        {
            if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                throw new BuildRequestException($"{option} に値が指定されていません。");
            }

            return args[index + 1];
        }
    }
}
