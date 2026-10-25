namespace ProjectPenguin.Domain.Village
{
    /// <summary>チュートリアルを進める合図。画面の状態の変化から作る (VillageTutorialPresenter)。</summary>
    public enum VillageTutorialSignal
    {
        Next, Skip, IndustrialOpened, FacilityOpened, PageClosed, Built, ZoomedDetail, ZoomedOverview, ReportOpened, ReportClosed
    }

    /// <summary>
    /// 村のチュートリアル (操作しながら進む 7 段) の進み方。段の番号だけを保存し、0 = 未開始、-1 = 終わり。
    /// 1 ようこそ (次へ) → 2 工房モードを開く → 3 施設を押す → 4 ページを閉じて流れを見る (次へ) →
    /// 5 施設を 1 棟建てる (建てられないときは次へ) → 6 詳細へ寄って全体へ戻る → 7 生産レポートを開いて閉じる → 終わり。
    /// 関係ない合図では進まない。飛ばすといつでも終わり。
    /// </summary>
    public sealed class VillageTutorial
    {
        public const int NotStarted = 0, Done = -1, Count = 7;
        public const int Welcome = 1, OpenWorkshop = 2, TapFacility = 3, Flow = 4, Build = 5, Zoom = 6, Report = 7;

        /// <summary>今の段 (1〜7)。未開始は 0、終わりは -1。</summary>
        public int Step { get; private set; }

        /// <summary>
        /// 段の前半を終えたか。段 4 はページを閉じた、段 6 は詳細へ寄った、段 7 は生産レポートを開いた。
        /// 保存はしない (途中で終了すると、その段の始めからやり直す)。
        /// </summary>
        public bool Halfway { get; private set; }

        public bool Active => Step >= Welcome && Step <= Count;

        /// <summary>保存の値から作る。範囲外の値は「終わり」として扱い、勝手に始めない。</summary>
        public VillageTutorial(int saved)
        {
            Step = IsValid(saved) ? saved : Done;
        }

        public static bool IsValid(int step) => step == NotStarted || step == Done || step >= Welcome && step <= Count;

        /// <summary>最初から始める (初回起動とメニューの「もう一度」)。</summary>
        public void Start()
        {
            Step = Welcome;
            Halfway = false;
        }

        /// <summary>合図を渡す。段か前半の状態が変わったら true。</summary>
        public bool Handle(VillageTutorialSignal signal)
        {
            if (!Active) return false;
            if (signal == VillageTutorialSignal.Skip) return Finish();
            switch (Step)
            {
                case Welcome: return signal == VillageTutorialSignal.Next && Advance();
                case OpenWorkshop: return signal == VillageTutorialSignal.IndustrialOpened && Advance();
                case TapFacility: return signal == VillageTutorialSignal.FacilityOpened && Advance();
                case Flow:
                    if (signal == VillageTutorialSignal.PageClosed && !Halfway) return Half();
                    return signal == VillageTutorialSignal.Next && Halfway && Advance();
                case Build: return (signal == VillageTutorialSignal.Built || signal == VillageTutorialSignal.Next) && Advance();
                case Zoom:
                    if (signal == VillageTutorialSignal.ZoomedDetail && !Halfway) return Half();
                    return signal == VillageTutorialSignal.ZoomedOverview && Halfway && Advance();
                case Report:
                    if (signal == VillageTutorialSignal.ReportOpened && !Halfway) return Half();
                    return signal == VillageTutorialSignal.ReportClosed && Halfway && Finish();
                default: return false;
            }
        }

        private bool Advance()
        {
            Step = Step == Count ? Done : Step + 1;
            Halfway = false;
            return true;
        }

        private bool Half()
        {
            Halfway = true;
            return true;
        }

        private bool Finish()
        {
            Step = Done;
            Halfway = false;
            return true;
        }
    }
}
