namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>
    /// 全画面生産レポート (ProductionReport.uxml) の要素名および USS クラス名。
    /// </summary>
    public static class ProductionReportNames
    {
        // 要素名 (Name)
        public const string Root = "pp-report-root";
        public const string Header = "pp-report-header";
        public const string TitleMicro = "pp-report-micro";
        public const string TitleText = "pp-report-title-text";
        public const string TabPower = "pp-report-tab-power";
        public const string TabItems = "pp-report-tab-items";
        public const string Close = "pp-report-close";

        // 動力タンク部分
        public const string PowerSection = "pp-report-power";
        public const string TankGauge = "pp-report-tank";
        public const string SupplyNum = "pp-report-supply-num";
        public const string ConsumeNum = "pp-report-consume-num";
        public const string GasStock = "pp-report-gas-stock";
        public const string GasCap = "pp-report-gas-cap";
        public const string NetPill = "pp-report-net-pill";
        public const string NetPillText = "pp-report-net-text";
        public const string NetPillNum = "pp-report-net-num";
        public const string EmptyEstimate = "pp-report-empty-estimate";

        // 状態札 (Chips)
        public const string ChipActiveNum = "pp-report-chip-active-num";
        public const string ChipActiveTotal = "pp-report-chip-active-total";
        public const string ChipStoppedNum = "pp-report-chip-stopped-num";
        public const string ChipStoppedReason = "pp-report-chip-stopped-reason";
        public const string ChipCargoNum = "pp-report-chip-cargo-num";

        // 品目表
        public const string SectionTitle = "pp-report-section-title";
        public const string TableHeader = "pp-report-thead";
        public const string Scroll = "pp-report-scroll";
        public const string EmptyNotice = "pp-report-empty-notice";

        // 下部固定操作バー
        public const string Footer = "pp-report-footer";
        public const string SortBtn = "pp-report-btn-sort";
        public const string FilterBtn = "pp-report-btn-filter";
        public const string SpanBtn = "pp-report-btn-span";

        // USS クラス名
        public const string IndustrialClass = "pp-industrial";
        public const string PageClass = "pp-ind-page";
        public const string SegItemActiveClass = "pp-ind-seg__item--active";
        public const string RowClass = "pp-ind-row";
        public const string RowDeficitClass = "pp-ind-row--deficit";
        public const string RowStoppedClass = "pp-ind-row--stopped";
        public const string ChipBadClass = "pp-ind-chip--bad";
    }
}
