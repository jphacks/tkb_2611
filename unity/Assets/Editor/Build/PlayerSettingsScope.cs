using System;
using UnityEditor;

namespace ProjectPenguin.Editor.Build
{
    /// <summary>
    /// ビルドのために書き換えた PlayerSettings / EditorUserBuildSettings を元へ戻す。
    ///
    /// これらの API はプロジェクト設定そのものを書き換えるため、戻さないと
    /// ProjectSettings.asset に差分が残り、署名パスワードまでコミットされうる。
    /// ビルドが失敗した場合も必ず戻すこと。
    /// </summary>
    internal sealed class PlayerSettingsScope : IDisposable
    {
        private readonly string _bundleVersion;
        private readonly int _androidVersionCode;
        private readonly bool _useCustomKeystore;
        private readonly string _keystoreName;
        private readonly string _keystorePass;
        private readonly string _keyaliasName;
        private readonly string _keyaliasPass;
        private readonly string _appleTeamId;
        private readonly string _iosBuildNumber;
        private readonly bool _buildAppBundle;
        private readonly WebGLCompressionFormat _webGLCompression;
        private readonly bool _webGLDecompressionFallback;
        private readonly bool _webGLNameFilesAsHashes;
        private readonly bool _webGLDataCaching;
        private readonly string _webGLEmscriptenArgs;

        public PlayerSettingsScope()
        {
            _bundleVersion = PlayerSettings.bundleVersion;
            _androidVersionCode = PlayerSettings.Android.bundleVersionCode;
            _useCustomKeystore = PlayerSettings.Android.useCustomKeystore;
            _keystoreName = PlayerSettings.Android.keystoreName;
            _keystorePass = PlayerSettings.Android.keystorePass;
            _keyaliasName = PlayerSettings.Android.keyaliasName;
            _keyaliasPass = PlayerSettings.Android.keyaliasPass;
            _appleTeamId = PlayerSettings.iOS.appleDeveloperTeamID;
            _iosBuildNumber = PlayerSettings.iOS.buildNumber;
            _buildAppBundle = EditorUserBuildSettings.buildAppBundle;
            _webGLCompression = PlayerSettings.WebGL.compressionFormat;
            _webGLDecompressionFallback = PlayerSettings.WebGL.decompressionFallback;
            _webGLNameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            _webGLDataCaching = PlayerSettings.WebGL.dataCaching;
            _webGLEmscriptenArgs = PlayerSettings.WebGL.emscriptenArgs;
        }

        public void Dispose()
        {
            PlayerSettings.bundleVersion = _bundleVersion;
            PlayerSettings.Android.bundleVersionCode = _androidVersionCode;
            PlayerSettings.Android.useCustomKeystore = _useCustomKeystore;
            // 空文字を代入すると Unity が "{inproject}: " に置き換えて保存するため、変わったときだけ戻す。
            if (PlayerSettings.Android.keystoreName != _keystoreName)
            {
                PlayerSettings.Android.keystoreName = _keystoreName;
            }

            PlayerSettings.Android.keystorePass = _keystorePass;
            PlayerSettings.Android.keyaliasName = _keyaliasName;
            PlayerSettings.Android.keyaliasPass = _keyaliasPass;
            PlayerSettings.iOS.appleDeveloperTeamID = _appleTeamId;
            PlayerSettings.iOS.buildNumber = _iosBuildNumber;
            EditorUserBuildSettings.buildAppBundle = _buildAppBundle;
            PlayerSettings.WebGL.compressionFormat = _webGLCompression;
            PlayerSettings.WebGL.decompressionFallback = _webGLDecompressionFallback;
            PlayerSettings.WebGL.nameFilesAsHashes = _webGLNameFilesAsHashes;
            PlayerSettings.WebGL.dataCaching = _webGLDataCaching;
            PlayerSettings.WebGL.emscriptenArgs = _webGLEmscriptenArgs;

            // BuildPlayer はビルド中の値で ProjectSettings.asset を保存する。メモリ上で戻すだけだと
            // 次に Editor が保存するまでディスクにビルド時の値 (ビルド番号・署名など) が残り、
            // そのままコミットされうるので、戻した値をすぐに書き出す。
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>ビルドを始める前の入力不備。Unity のビルド失敗とは区別する。</summary>
    public sealed class BuildRequestException : Exception
    {
        public BuildRequestException(string message) : base(message)
        {
        }
    }
}
