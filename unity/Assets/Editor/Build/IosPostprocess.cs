#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace ProjectPenguin.Editor.Build
{
    /// <summary>
    /// iOS の Xcode プロジェクトに、歩数 (CoreMotion) のための設定を足す。
    /// 署名や証明書には触らない (README の「署名」)。
    /// </summary>
    public sealed class IosPostprocess : IPostprocessBuildWithReport
    {
        /// <summary>動作と移動の許可を求めるときに出す説明。</summary>
        public const string MotionUsageDescription = "歩いた歩数を島のガスに変えるために、モーションと移動のデータを使います。歩数は端末の外に送りません。";

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
            plist.WriteToFile(plistPath);

            var projectPath = PBXProject.GetPBXProjectPath(output);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "CoreMotion.framework", false);
            project.WriteToFile(projectPath);
        }
    }
}
#endif
