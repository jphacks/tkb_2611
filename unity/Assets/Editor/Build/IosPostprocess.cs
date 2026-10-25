#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace ProjectPenguin.Editor.Build
{
    /// <summary>
    /// iOS の Xcode プロジェクトに、歩数 (CoreMotion) とすれ違い (CoreBluetooth) のための設定を足す。
    /// 署名や証明書には触らない (README の「署名」)。
    /// </summary>
    public sealed class IosPostprocess : IPostprocessBuildWithReport
    {
        /// <summary>動作と移動の許可を求めるときに出す説明。</summary>
        public const string MotionUsageDescription = "歩いた歩数を島のガスに変えるために、モーションと移動のデータを使います。歩数は端末の外に送りません。";

        /// <summary>Bluetooth の許可を求めるときに出す説明。無いと CoreBluetooth を使った時点でアプリが落ちる。</summary>
        public const string BluetoothUsageDescription = "近くでアプリを開いている人とすれ違ってペンギンに出会うために、Bluetooth を使います。やり取りするのは毎日作り直す乱数だけです。";

        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            var output = report.summary.outputPath;

            var plistPath = Path.Combine(output, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSMotionUsageDescription", MotionUsageDescription);
            plist.root.SetString("NSBluetoothAlwaysUsageDescription", BluetoothUsageDescription);
            plist.root.SetString("NSCameraUsageDescription", "商品のバーコードを読み取り、水の記録を作るためにカメラを使います。画像は保存・送信しません。");
            plist.WriteToFile(plistPath);

            var projectPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreMotion.framework", false);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreBluetooth.framework", false);
            foreach (var framework in new[] { "AVFoundation.framework", "PhotosUI.framework", "Vision.framework" })
                project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), framework, false);
            var records = project.FindFileGuidByProjectPath("Libraries/Plugins/iOS/PenguinFactoryRecords.mm");
            if (!string.IsNullOrEmpty(records))
                project.SetCompileFlagsForFile(project.GetUnityFrameworkTargetGuid(), records, new System.Collections.Generic.List<string> { "-fobjc-arc" });
            project.WriteToFile(projectPath);
        }
    }
}
#endif
