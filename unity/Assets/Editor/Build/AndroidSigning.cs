using System;
using System.IO;
using UnityEditor;

namespace ProjectPenguin.Editor.Build
{
    /// <summary>
    /// Android の署名設定を環境変数から流し込む。
    ///
    /// キーストアのパスワードを ProjectSettings.asset に保存すると、そのまま
    /// リポジトリにコミットされて配布鍵が流出する。値は環境変数からしか読まず、
    /// ビルド後は <see cref="PlayerSettingsScope"/> が元の値へ必ず戻す。
    /// </summary>
    internal static class AndroidSigning
    {
        public const string KeystorePathEnv = "PENGUIN_ANDROID_KEYSTORE";
        public const string KeystorePassEnv = "PENGUIN_ANDROID_KEYSTORE_PASS";
        public const string KeyAliasEnv = "PENGUIN_ANDROID_KEYALIAS";
        public const string KeyAliasPassEnv = "PENGUIN_ANDROID_KEYALIAS_PASS";

        /// <returns>ログに出す署名方式の説明。パスワードは含めない。</returns>
        public static string Apply(BuildRequest request)
        {
            var path = Environment.GetEnvironmentVariable(KeystorePathEnv);

            if (string.IsNullOrWhiteSpace(path))
            {
                // 配布物は必ず自前の鍵で署名する。debug 鍵の AAB は Play に上げられず、
                // 上げられたとしても以降その鍵から変更できなくなる。
                if (request.Kind == BuildKind.AndroidAab)
                {
                    throw new BuildRequestException(
                        $"AAB のビルドには署名鍵が要ります。{KeystorePathEnv} / {KeystorePassEnv} / "
                        + $"{KeyAliasEnv} / {KeyAliasPassEnv} を環境変数に設定してください "
                        + "(鍵ファイルとパスワードはリポジトリに置かないこと)。");
                }

                PlayerSettings.Android.useCustomKeystore = false;
                return "debug 鍵 (動作確認用。配布不可)";
            }

            var full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                throw new BuildRequestException($"{KeystorePathEnv} が指すファイルがありません: {full}");
            }

            var keystorePass = Environment.GetEnvironmentVariable(KeystorePassEnv);
            var alias = Environment.GetEnvironmentVariable(KeyAliasEnv);
            var aliasPass = Environment.GetEnvironmentVariable(KeyAliasPassEnv);

            if (string.IsNullOrEmpty(keystorePass) || string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(aliasPass))
            {
                throw new BuildRequestException(
                    $"{KeystorePathEnv} は設定されていますが、{KeystorePassEnv} / {KeyAliasEnv} / "
                    + $"{KeyAliasPassEnv} のいずれかが空です。");
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = full;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;

            return $"custom 鍵 (alias: {alias})";
        }
    }
}
