#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace ProjectPenguin.Editor.Build
{
    /// <summary>
    /// iOS の Xcode プロジェクトに、歩数 (CoreMotion)・スクリーンタイムの画像の OCR (PhotosUI・Vision)・
    /// 水のペットボトルの撮影 (UIKit のカメラ、Swift)・すれ違い (CoreBluetooth) のための設定を足す。
    /// 署名や証明書には触らない (README の「署名」)。
    /// </summary>
    public sealed class IosPostprocess : IPostprocessBuildWithReport
    {
        /// <summary>動作と移動の許可を求めるときに出す説明。</summary>
        public const string MotionUsageDescription = "歩いた歩数を島のガスに変えるために、モーションと移動のデータを使います。歩数は端末の外に送りません。";

        /// <summary>Bluetooth の許可を求めるときに出す説明。無いと CoreBluetooth を使った時点でアプリが落ちる。</summary>
        public const string BluetoothUsageDescription = "近くでアプリを開いている人とすれ違ってペンギンに出会うために、Bluetooth を使います。やり取りするのは毎日作り直す乱数だけです。";

        /// <summary>カメラの許可を求めるときに出す説明。Player Settings の文を、水の写真の文で上書きする。</summary>
        public const string CameraUsageDescription = "飲みきったペットボトルを撮って、飲んだ水の量を確かめるためにカメラを使います。写真は量を見積もるサーバーに送ったら捨て、端末にもサーバーにも保存しません。";

        public const string SwiftVersion = "5.0";

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
            plist.root.SetString("NSCameraUsageDescription", CameraUsageDescription);
            plist.WriteToFile(plistPath);

            var projectPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            // Swift のプラグイン (PenguinWaterCamera.swift) をコンパイルする版。
            project.SetBuildProperty(project.GetUnityFrameworkTargetGuid(), "SWIFT_VERSION", SwiftVersion);
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
