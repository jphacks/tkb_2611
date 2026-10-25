namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>ProductionApp.uxml の要素名と、ProductionApp.uss の村専用のクラス。部品 (pp-*) のクラスは <see cref="UiClasses"/> を使う。</summary>
    public static class ProductionUiNames
    {
        public const string Root="villageApp",Header="villageHeader",Hero="villageHero",RankBadge="villageRankBadge",RankText="villageRankText",
            Profile="villageProfile",IndustrialSwitch="villageIndustrialSwitch",IndustrialSwitchLabel="villageIndustrialSwitchLabel",
            Fish="villageFish",FishRate="villageFishRate",RankProgress="villageRankProgress",
            Resources="villageResources",Gas="villageGas",Electricity="villageElectricity",Water="villageWater",GasBox="villageGasBox",ElectricityBox="villageElectricityBox",WaterBox="villageWaterBox",
            ZoomOverview="villageZoomOverview",ZoomDetail="villageZoomDetail",NextStep="villageNextStep",TacticalStatus="villageTacticalStatus",GroupState="villageGroupState",TacticalBar="villageTacticalBar",TacticalGas="villageTacticalGas",TopDownToggle="villageTopDownToggle",TopDownLabel="villageTopDownLabel",RotateCamera="villageRotateCamera",
            Error="villageError",ErrorText="villageErrorText",Retry="villageRetry",
            SpatialLayer="villageSpatialLayer",
            InsightBanner="villageInsightBanner",InsightText="villageInsightText",
            Tools="villageTools",Move="villageMove",Placement="villagePlacement",Production="villageProduction",Harbor="villageHarbor",
            QuickDock="villageQuickDock",DockTabs="villageDockTabs",DockScroll="villageDockScroll",DockCatalogBtn="villageDockCatalogBtn",
            Tray="villagePlacementTray",Reason="villagePlacementReason",PlacementThumb="villagePlacementThumb",Confirm="villageConfirm",Cancel="villageCancel",
            ToastLayer="villageToastLayer",
            Sheet="villageSheet",Scrim="villageSheetScrim",Title="villageSheetTitle",Close="villageClose",Content="villageSheetContent",
            Navigation="villageNavigation",HomeTab="villageHomeTab",RecordTab="villageRecordTab",ScanTab="villageScanTab",ManageTab="villageManageTab",MenuTab="villageMenuTab",
            VillageProductionReport="villageProductionReport";

        /// <summary>ProductionApp.uss のクラス。</summary>
        public const string Hint="village-hint",HintBad="village-hint--bad",Card="village-card",CardTitle="village-card__title",CardActions="village-card__actions",
            Subtle="village-subtle",Stat="village-stat",Empty="village-empty",EmptyText="village-empty__text",
            Shelf="village-shelf",ShelfRecipe="village-shelf-card__recipe",ShelfCost="village-shelf-card__cost",ShelfSoon="village-shelf-card__soon",
            Facility="village-facility",FacilityThumb="village-facility__thumb",FacilityBody="village-facility__body",FacilityProgress="village-facility__progress",
            Items="village-items",
            RankBadgeClass="village-rank-badge",RankLabelClass="village-rank-label",RankValueClass="village-rank-value",RankProgressClass="village-rank-progress",
            IndustrialSwitchClass="village-industrial-switch",
            TacticalBarClass="village-tactical-bar",TacticalTitleClass="village-tactical-title",TacticalGasClass="village-tactical-gas",TacticalBtnClass="village-tactical-btn",TacticalBtnActiveClass="village-tactical-btn--active",
            TacticalRowClass="village-tactical-row",NextStepClass="village-next-step",TacticalStatusClass="village-tactical-status",TacticalStatusBadClass="village-tactical-status--bad",TacticalGasEmptyClass="village-tactical-gas--empty",NextStepWarnClass="village-next-step--warn",GroupStateClass="village-group-state",GroupMarkClass="village-group-mark",GroupMarkWaitingClass="village-group-mark--waiting",SpatialLayerClass="village-spatial-layer",SpatialBadgeClass="village-spatial-badge",SpatialBadgeWarnClass="village-spatial-badge--warn",SpatialBadgeSynergyClass="village-spatial-badge--synergy",
            InsightBannerClass="village-insight-banner",InsightBannerWarnClass="village-insight-banner--warn",
            QuickDockClass="village-quick-dock",DockHeaderClass="village-dock-header",DockTabsClass="village-dock-tabs",DockTabClass="village-dock-tab",DockTabActiveClass="village-dock-tab--active",
            DockScrollClass="village-dock-scroll",DockSlotClass="village-dock-slot",DockSlotSuggestedClass="village-dock-slot--suggested",DockSlotThumbClass="village-dock-slot__thumb",DockSlotNameClass="village-dock-slot__name",
            ProductionReportClass="village-production-report";

        /// <summary>主役の数字が増えたときに一瞬ふくらませる (Components.uss)。</summary>
        public const string HeroBump="pp-hero--bump";
        public const string Upgrade="village-upgrade",UpgradeHeader="village-upgrade__header",UpgradeLevel="village-upgrade__level",
            UpgradeGrid="village-upgrade__grid",UpgradeStage="village-upgrade__stage",UpgradeNext="village-upgrade__stage--next",
            UpgradeCaption="village-upgrade__caption",UpgradeEffect="village-upgrade__effect",UpgradeCost="village-upgrade__cost",
            UpgradeHint="village-upgrade__hint",UpgradeWarning="village-upgrade__hint--warning",UpgradeConfirm="village-upgrade__confirm",
            UpgradeActions="village-upgrade__actions",FacilityLevel="village-facility__level";
    }
}
