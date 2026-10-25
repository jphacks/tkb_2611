using UnityEngine;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// 端末の設定アプリにある、このアプリのページを開く。
    /// iOS ではカメラを一度拒否されるとアプリから再度許可を求められないため、ここから切り替えてもらう。
    /// アプリ内の設定画面ができるまでは、ホームの歯車とメニューの「設定」から直接開く (暫定)。
    /// </summary>
    public static class AppSettingsLauncher
    {
        public static void Open()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Application.OpenURL("app-settings:");
#else
            // Android は MVP の対象外。Editor では開く先がない。
            Debug.Log("[AppSettings] 端末の設定画面は iOS の実機でのみ開けます");
#endif
        }
    }
}
