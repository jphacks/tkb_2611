using System;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Data
{
    /// <summary>
    /// 端末に保存するプレイヤーの状態。写真そのものと購買の明細は入れない (.claude/rules/privacy.md)。
    /// JsonUtility で読み書きするため public フィールドにしている。フィールド名を変えると古い保存が読めなくなる。
    /// </summary>
    [Serializable]
    public sealed class GameSave
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>最初に起動した時刻。氷の状態機械 (GameSession) はここから操作を再生して戻す。</summary>
        public long createdUnixMs;

        /// <summary>開発ビルドで時計を進めた量。</summary>
        public long devOffsetMs;

        /// <summary>前回開いたアプリの日 (おかえりの判定)。</summary>
        public int lastOpenDay;

        public string playerName = "";
        public bool onboarded;

        /// <summary>氷の状態機械への操作 (起きた順)。</summary>
        public GameOp[] ops = Array.Empty<GameOp>();

        public IslandSave island;

        /// <summary>メモリに持つ受け取りの記録の数。端末の DB には全件が残り、読み込みでは新しいほうからこの数だけ返す。</summary>
        public const int ActivityWindow = 200;

        /// <summary>資源を受け取った記録 (記録画面)。新しいものが後ろ。直近の <see cref="ActivityWindow"/> 件だけ。</summary>
        public ActivityEntry[] activity = Array.Empty<ActivityEntry>();

        /// <summary>これまでに <see cref="activity"/> の窓から落とした件数。窓の i 番目は、全履歴の <c>activityTrimmed + i</c> 番目 (0 始まり)。</summary>
        public int activityTrimmed;
    }

    /// <summary>氷の状態機械への操作 1 つ。</summary>
    [Serializable]
    public sealed class GameOp
    {
        public const string Photo = "photo";
        public const string Select = "select";
        public const string CheckIn = "checkin";

        /// <summary>島の氷室の効き目が変わった (<see cref="value"/> = 溶ける量を減らす割合)。</summary>
        public const string MeltGuard = "meltguard";

        public string kind;
        public long atMs;
        public string id;
        public int printDay;
        public int verification;
        public int answer;
        public bool noShopping;

        /// <summary>数値の引数 (<see cref="MeltGuard"/> の割合)。</summary>
        public double value;
    }

    /// <summary>現実の行動で資源を受け取った記録 1 件。</summary>
    [Serializable]
    public sealed class ActivityEntry
    {
        public const string Photo = "photo";
        public const string Steps = "steps";
        public const string Mission = "mission";
        public const string QuickEco = "quickEco";

        public long atMs;
        public int day;
        public string kind;

        /// <summary>写真のカテゴリ (<see cref="EcoCategory"/>)。写真以外は -1。</summary>
        public int category = -1;

        /// <summary>写っていたもの・ミッション名・歩数など。</summary>
        public string label;

        public double electricity;
        public double water;
        public double gas;
        public double allMighty;
    }
}
