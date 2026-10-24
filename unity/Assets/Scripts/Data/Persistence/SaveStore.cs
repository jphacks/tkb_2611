using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProjectPenguin.Data.Persistence
{
    /// <summary><see cref="SaveStore.Load"/> の結果の種類。</summary>
    public enum SaveLoadStatus
    {
        /// <summary>保存が無い (初回起動)。</summary>
        NotFound,

        /// <summary>本体を読めた。</summary>
        Loaded,

        /// <summary>本体が壊れていた (または無かった) ので、1 世代前のバックアップから読んだ。次の保存で本体が作り直される。</summary>
        RestoredFromBackup,

        /// <summary>本体が無い (または壊れている) が、完全な一時ファイルが残っていた (置き換えの途中で止まった。本体より新しい)。</summary>
        RestoredFromTemp,

        /// <summary>ファイルはあるが、どれもチェックサムが合わない・形式が違う・検証に通らない。</summary>
        Corrupt,

        /// <summary>このアプリより新しい版で書かれている (アプリを古い版に戻した)。上書きしないよう、以後の <see cref="SaveStore.Save"/> は拒む。</summary>
        TooNew,

        /// <summary>古い版から今の版への変換が無い、または変換が失敗した。</summary>
        MigrationFailed,
    }

    /// <summary><see cref="SaveStore.Load"/> の結果。</summary>
    public sealed class SaveLoadResult
    {
        internal SaveLoadResult(SaveLoadStatus status, string payload, int storedVersion, bool migrated, string detail)
        {
            Status = status;
            Payload = payload;
            StoredVersion = storedVersion;
            Migrated = migrated;
            Detail = detail;
        }

        public SaveLoadStatus Status { get; }

        /// <summary>今の版に変換済みの JSON。読めなかったときは null。</summary>
        public string Payload { get; }

        /// <summary>ファイルに書かれていた版。読めなかったときは 0。</summary>
        public int StoredVersion { get; }

        /// <summary>古い版から変換した。呼び出し側はすぐ保存し直してよい。</summary>
        public bool Migrated { get; }

        /// <summary>診断用の説明 (ログ用。中身の JSON は含めない)。</summary>
        public string Detail { get; }

        public bool HasPayload => Payload != null;
    }

    /// <summary>
    /// 版の変換表 (移行の入口)。<c>Register(n, f)</c> は「版 n の JSON を版 n+1 の JSON にする」関数を登録する。
    /// 読んだ版が今の版より古ければ、n → n+1 → … と順に通す。今は版 1 だけなので何も登録しない。
    /// </summary>
    public sealed class SaveMigrations
    {
        private readonly Dictionary<int, Func<string, string>> _steps = new Dictionary<int, Func<string, string>>();

        public SaveMigrations Register(int fromVersion, Func<string, string> toNextVersion)
        {
            if (fromVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fromVersion));
            }

            _steps[fromVersion] = toNextVersion ?? throw new ArgumentNullException(nameof(toNextVersion));
            return this;
        }

        internal bool TryMigrate(string payload, int from, int to, out string migrated, out string error)
        {
            migrated = payload;
            error = null;
            for (var v = from; v < to; v++)
            {
                if (!_steps.TryGetValue(v, out var step))
                {
                    error = "no migration from version " + v.ToString(CultureInfo.InvariantCulture);
                    return false;
                }

                try
                {
                    migrated = step(migrated);
                }
                catch (Exception e)
                {
                    error = "migration from version " + v.ToString(CultureInfo.InvariantCulture) + " threw " + e.GetType().Name;
                    return false;
                }

                if (migrated == null)
                {
                    error = "migration from version " + v.ToString(CultureInfo.InvariantCulture) + " returned null";
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// セーブデータ 1 本を、壊れにくく保存する (v5、16d §1.3)。UnityEngine に依存しない。Unity では
    /// <c>Application.persistentDataPath</c> (iOS は Documents) をディレクトリに渡す。
    ///
    /// <para><b>書き方。</b>一時ファイル (<c>.tmp</c>) に全部書いて <c>Flush(true)</c> で記憶装置まで押し出してから、
    /// <see cref="File.Replace(string, string, string, bool)"/> で本体と入れ替える。本体だったものはバックアップ (<c>.bak</c>) になる。
    /// 本体がまだ無いときは <see cref="File.Move(string, string)"/>。本体が壊れているときは、良いバックアップを壊れた本体で
    /// 上書きしないよう、バックアップを作らずに入れ替える。</para>
    ///
    /// <para><b>形式。</b>ファイル全体が 1 つの JSON で、中身 (payload) をそのまま埋め込む。
    /// <c>{"format":"ProjectPenguin.Save","envelope":1,"schemaVersion":1,"sha256":"…","payload":{…}}</c>。
    /// sha256 は <c>"schemaVersion=1\n" + payload</c> の UTF-8 の SHA-256 (小文字 16 進)。書き手と読み手がこのクラスだけなので、
    /// JSON ライブラリを使わず、決まった並びとして書き・読む。並びが違えば壊れたものとして扱う。</para>
    ///
    /// <para><b>読み方。</b>本体 → 一時ファイル → バックアップの順に、チェックサムが合い、呼び出し側の検証 (任意) にも通った
    /// 最初のものを使う。書きかけの一時ファイルはチェックサムで落ちる。</para>
    ///
    /// <para>改ざんは防がない (鍵が端末にある以上、防げない。16d §1.3)。防ぐのは、書きかけで止まった・記憶装置で化けたファイルを
    /// 読んでしまうことと、版の違うファイルを黙って読むこと。</para>
    /// </summary>
    public sealed class SaveStore
    {
        public const string FormatName = "ProjectPenguin.Save";
        public const int EnvelopeVersion = 1;

        private const string HeadPrefix = "{\"format\":\"" + FormatName + "\",\"envelope\":1,\"schemaVersion\":";
        private const string ShaKey = ",\"sha256\":\"";
        private const string PayloadKey = "\",\"payload\":";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false, true);

        private readonly int _currentVersion;
        private readonly SaveMigrations _migrations;
        private readonly bool _useReplace;
        private bool _blockedByNewerFile;

        /// <param name="directory">保存先 (Unity では Application.persistentDataPath)。</param>
        /// <param name="fileName">本体のファイル名。バックアップは <c>+ ".bak"</c>、一時ファイルは <c>+ ".tmp"</c>。</param>
        /// <param name="currentVersion">このアプリが書く中身の版 (GameSessionState.CurrentVersion)。</param>
        /// <param name="migrations">古い版を読むための変換。null なら変換なし。</param>
        /// <param name="useReplace">false にすると File.Replace を使わない代替手順を通す (File.Replace が使えない環境の確認用)。</param>
        public SaveStore(string directory, string fileName, int currentVersion, SaveMigrations migrations = null, bool useReplace = true)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("directory is required", nameof(directory));
            }

            if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("invalid file name", nameof(fileName));
            }

            if (currentVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(currentVersion));
            }

            Directory = directory;
            MainPath = Path.Combine(directory, fileName);
            BackupPath = MainPath + ".bak";
            TempPath = MainPath + ".tmp";
            _currentVersion = currentVersion;
            _migrations = migrations ?? new SaveMigrations();
            _useReplace = useReplace;
        }

        public string Directory { get; }

        public string MainPath { get; }

        public string BackupPath { get; }

        public string TempPath { get; }

        /// <summary>直前の保存で File.Replace が使えず代替手順に切り替えたか (診断用)。</summary>
        public bool UsedFallbackOnLastSave { get; private set; }

        /// <summary>
        /// 中身 (今の版の JSON) を保存する。失敗したら例外 (IOException など) をそのまま投げる。そのときも本体かバックアップの
        /// どちらかは前の内容のまま残る。<see cref="Load"/> が <see cref="SaveLoadStatus.TooNew"/> を返した後は、新しい版のファイルを
        /// 古いアプリで上書きしないよう <see cref="InvalidOperationException"/> を投げる。
        /// </summary>
        public void Save(string payloadJson)
        {
            if (payloadJson == null)
            {
                throw new ArgumentNullException(nameof(payloadJson));
            }

            if (_blockedByNewerFile)
            {
                throw new InvalidOperationException("the save file was written by a newer app version; refusing to overwrite it");
            }

            System.IO.Directory.CreateDirectory(Directory);
            var bytes = Utf8NoBom.GetBytes(Encode(payloadJson, _currentVersion));

            using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }

            UsedFallbackOnLastSave = false;
            if (!File.Exists(MainPath))
            {
                File.Move(TempPath, MainPath);
                return;
            }

            // 本体が壊れていれば、それをバックアップにしない (良いバックアップが消えるため)。
            var backup = ReadCandidate(MainPath, null).Ok ? BackupPath : null;
            if (_useReplace)
            {
                try
                {
                    File.Replace(TempPath, MainPath, backup, true);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // 下の代替手順へ
                }
            }

            ReplaceWithoutFileReplace(backup);
        }

        /// <summary>
        /// 保存を読む。<paramref name="validate"/> を渡すと、チェックサムが合っても中身が使えない (逆シリアライズできない) ものを飛ばして
        /// 次の候補を試す。検証は変換後 (今の版) の JSON に対して行う。
        /// </summary>
        public SaveLoadResult Load(Func<string, bool> validate = null)
        {
            _blockedByNewerFile = false;
            var mainExists = File.Exists(MainPath);
            if (!mainExists && !File.Exists(BackupPath) && !File.Exists(TempPath))
            {
                return new SaveLoadResult(SaveLoadStatus.NotFound, null, 0, false, "no save files");
            }

            var details = new StringBuilder();
            // 一時ファイルは、書き終えて Flush した後でしか完全な形にならず、置き換えが済めば消える。
            // 完全な一時ファイルが残っているのは置き換えの途中で止まったときだけで、その内容は本体より新しい。
            // (Unity の Mono の File.Replace は「本体→バックアップ」「一時→本体」の 2 回の rename なので、その間に止まると本体が無い。)
            var candidates = new List<KeyValuePair<string, SaveLoadStatus>>
            {
                new KeyValuePair<string, SaveLoadStatus>(MainPath, SaveLoadStatus.Loaded),
                new KeyValuePair<string, SaveLoadStatus>(TempPath, SaveLoadStatus.RestoredFromTemp),
                new KeyValuePair<string, SaveLoadStatus>(BackupPath, SaveLoadStatus.RestoredFromBackup),
            };

            SaveLoadResult firstHardFailure = null;
            foreach (var candidate in candidates)
            {
                if (!File.Exists(candidate.Key))
                {
                    continue;
                }

                var read = ReadCandidate(candidate.Key, validate);
                if (read.Ok)
                {
                    return new SaveLoadResult(candidate.Value, read.Payload, read.StoredVersion, read.Migrated, details + read.Detail);
                }

                details.Append(Path.GetFileName(candidate.Key)).Append(": ").Append(read.Detail).Append("; ");
                if (firstHardFailure == null && (read.Status == SaveLoadStatus.TooNew || read.Status == SaveLoadStatus.MigrationFailed))
                {
                    firstHardFailure = new SaveLoadResult(read.Status, null, read.StoredVersion, false, read.Detail);
                }
            }

            if (firstHardFailure != null)
            {
                if (firstHardFailure.Status == SaveLoadStatus.TooNew)
                {
                    _blockedByNewerFile = true;
                }

                return new SaveLoadResult(firstHardFailure.Status, null, firstHardFailure.StoredVersion, false, details.ToString());
            }

            return new SaveLoadResult(SaveLoadStatus.Corrupt, null, 0, false, details.ToString());
        }

        /// <summary>
        /// 読めなかったファイルを <c>.corrupt</c> を付けた名前に退避し、初期状態から始め直せるようにする (調査用に中身は残す)。
        /// <see cref="SaveLoadStatus.TooNew"/> のときは呼ばない (新しいアプリに戻せば読めるため)。
        /// </summary>
        public void QuarantineAll()
        {
            foreach (var path in new[] { MainPath, BackupPath, TempPath })
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                var target = path + ".corrupt";
                if (File.Exists(target))
                {
                    File.Delete(target);
                }

                File.Move(path, target);
            }

            _blockedByNewerFile = false;
        }

        /// <summary>保存を消す (「データを消してはじめから」用)。</summary>
        public void DeleteAll()
        {
            foreach (var path in new[] { MainPath, BackupPath, TempPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }

            _blockedByNewerFile = false;
        }

        /// <summary>保存形式の文字列を作る (テスト用に公開)。</summary>
        public static string Encode(string payloadJson, int schemaVersion)
        {
            return HeadPrefix + schemaVersion.ToString(CultureInfo.InvariantCulture) + ShaKey + Checksum(payloadJson, schemaVersion)
                + PayloadKey + payloadJson + "}\n";
        }

        /// <summary>中身と版から求めるチェックサム (小文字 16 進 64 文字)。</summary>
        public static string Checksum(string payloadJson, int schemaVersion)
        {
            var bytes = Utf8NoBom.GetBytes("schemaVersion=" + schemaVersion.ToString(CultureInfo.InvariantCulture) + "\n" + payloadJson);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        /// <summary>保存形式を読む。形式・チェックサムが合えば true (版の変換はしない)。</summary>
        public static bool TryDecode(string text, out string payloadJson, out int schemaVersion, out string error)
        {
            payloadJson = null;
            schemaVersion = 0;
            error = null;
            if (text == null || !text.StartsWith(HeadPrefix, StringComparison.Ordinal))
            {
                error = "unknown format";
                return false;
            }

            var at = HeadPrefix.Length;
            var versionEnd = at;
            while (versionEnd < text.Length && versionEnd - at < 9 && text[versionEnd] >= '0' && text[versionEnd] <= '9')
            {
                versionEnd++;
            }

            if (versionEnd == at || !int.TryParse(text.Substring(at, versionEnd - at), NumberStyles.None, CultureInfo.InvariantCulture, out schemaVersion))
            {
                error = "bad schemaVersion";
                return false;
            }

            if (string.CompareOrdinal(text, versionEnd, ShaKey, 0, ShaKey.Length) != 0)
            {
                error = "bad layout";
                return false;
            }

            var shaStart = versionEnd + ShaKey.Length;
            if (text.Length < shaStart + 64 + PayloadKey.Length)
            {
                error = "truncated";
                return false;
            }

            var sha = text.Substring(shaStart, 64);
            var payloadStart = shaStart + 64 + PayloadKey.Length;
            if (string.CompareOrdinal(text, shaStart + 64, PayloadKey, 0, PayloadKey.Length) != 0)
            {
                error = "bad layout";
                return false;
            }

            var end = text.Length;
            if (end > 0 && text[end - 1] == '\n')
            {
                end--;
            }

            if (end <= payloadStart || text[end - 1] != '}')
            {
                error = "truncated";
                return false;
            }

            var payload = text.Substring(payloadStart, end - 1 - payloadStart);
            if (!string.Equals(sha, Checksum(payload, schemaVersion), StringComparison.Ordinal))
            {
                error = "checksum mismatch";
                return false;
            }

            payloadJson = payload;
            return true;
        }

        private CandidateRead ReadCandidate(string path, Func<string, bool> validate)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Utf8NoBom);
            }
            catch (DecoderFallbackException)
            {
                return CandidateRead.Fail(SaveLoadStatus.Corrupt, 0, "not UTF-8");
            }
            catch (IOException e)
            {
                return CandidateRead.Fail(SaveLoadStatus.Corrupt, 0, "read failed: " + e.GetType().Name);
            }

            if (!TryDecode(text, out var payload, out var version, out var error))
            {
                return CandidateRead.Fail(SaveLoadStatus.Corrupt, version, error);
            }

            if (version > _currentVersion)
            {
                return CandidateRead.Fail(SaveLoadStatus.TooNew, version, "written by newer version " + version.ToString(CultureInfo.InvariantCulture));
            }

            var migrated = false;
            if (version < _currentVersion)
            {
                if (!_migrations.TryMigrate(payload, version, _currentVersion, out var converted, out var migrationError))
                {
                    return CandidateRead.Fail(SaveLoadStatus.MigrationFailed, version, migrationError);
                }

                payload = converted;
                migrated = true;
            }

            if (validate != null)
            {
                bool ok;
                try
                {
                    ok = validate(payload);
                }
                catch (Exception e)
                {
                    return CandidateRead.Fail(SaveLoadStatus.Corrupt, version, "validation threw " + e.GetType().Name);
                }

                if (!ok)
                {
                    return CandidateRead.Fail(SaveLoadStatus.Corrupt, version, "validation failed");
                }
            }

            return new CandidateRead(true, SaveLoadStatus.Loaded, payload, version, migrated, migrated ? "migrated" : "ok");
        }

        // File.Replace が使えない環境向け。本体を消してから一時ファイルを移す間に止まると、本体が無く一時ファイルとバックアップだけが残る。
        // Load はその場合、完全な一時ファイル (新しい内容) を先に使う。
        private void ReplaceWithoutFileReplace(string backup)
        {
            UsedFallbackOnLastSave = true;
            if (backup != null)
            {
                File.Copy(MainPath, BackupPath, true);
            }

            File.Delete(MainPath);
            File.Move(TempPath, MainPath);
        }

        private readonly struct CandidateRead
        {
            public CandidateRead(bool ok, SaveLoadStatus status, string payload, int storedVersion, bool migrated, string detail)
            {
                Ok = ok;
                Status = status;
                Payload = payload;
                StoredVersion = storedVersion;
                Migrated = migrated;
                Detail = detail;
            }

            public bool Ok { get; }

            public SaveLoadStatus Status { get; }

            public string Payload { get; }

            public int StoredVersion { get; }

            public bool Migrated { get; }

            public string Detail { get; }

            public static CandidateRead Fail(SaveLoadStatus status, int version, string detail)
            {
                return new CandidateRead(false, status, null, version, false, detail);
            }
        }
    }
}
