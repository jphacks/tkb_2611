namespace ProjectPenguin.Presentation.UI.ProductionVillage
{
    /// <summary>ProductionApp.uxml の要素名と、ProductionApp.uss の村専用のクラス。部品 (pp-*) のクラスは <see cref="UiClasses"/> を使う。</summary>
    public static class ProductionUiNames
    {
        public const string Root="villageApp",Header="villageHeader",Hero="villageHero",RankBadge="villageRankBadge",RankText="villageRankText",
            Profile="villageProfile",IndustrialSwitch="villageIndustrialSwitch",IndustrialSwitchLabel="villageIndustrialSwitchLabel",
            Fish="villageFish",FishRate="villageFishRate",RankProgress="villageRankProgress",
            Resources="villageResources",Gas="villageGas",Electricity="villageElectricity",Water="villageWater",GasBox="villageGasBox",ElectricityBox="villageElectricityBox",WaterBox="villageWaterBox",
            ZoomDistrict="villageZoomDistrict",ZoomOverview="villageZoomOverview",ZoomDetail="villageZoomDetail",NextStep="villageNextStep",TacticalStatus="villageTacticalStatus",CameraRail="villageCameraRail",GroupState="villageGroupState",TacticalBar="villageTacticalBar",TacticalGas="villageTacticalGas",TopDownToggle="villageTopDownToggle",TopDownLabel="villageTopDownLabel",RotateCamera="villageRotateCamera",
            Error="villageError",ErrorText="villageErrorText",Retry="villageRetry",
            SpatialLayer="villageSpatialLayer",
            InsightBanner="villageInsightBanner",InsightText="villageInsightText",
            Tools="villageTools",Move="villageMove",Placement="villagePlacement",Production="villageProduction",Harbor="villageHarbor",
            QuickDock="villageQuickDock",DockTabs="villageDockTabs",DockScroll="villageDockScroll",DockCatalogBtn="villageDockCatalogBtn",
            Tray="villagePlacementTray",Reason="villagePlacementReason",PlacementThumb="villagePlacementThumb",Confirm="villageConfirm",Cancel="villageCancel",
            ToastLayer="villageToastLayer",
            Sheet="villageSheet",Scrim="villageSheetScrim",Title="villageSheetTitle",Close="villageClose",Content="villageSheetContent",
            Navigation="villageNavigation",HomeTab="villageHomeTab",RecordTab="villageRecordTab",ScanTab="villageScanTab",ManageTab="villageManageTab",MenuTab="villageMenuTab",
            VillageProductionReport="villageProductionReport",
            WorkshopPage="villageWorkshopPage",WorkshopMicro="villageWorkshopMicro",WorkshopTitle="villageWorkshopTitle",WorkshopClose="villageWorkshopClose",WorkshopContent="villageWorkshopContent",
            Coach="villageCoach",CoachStep="villageCoachStep",CoachTitle="villageCoachTitle",CoachBody="villageCoachBody",CoachNext="villageCoachNext",CoachSkip="villageCoachSkip",
            SoftPage="villageSoftPage",SoftBack="villageSoftBack",SoftTitle="villageSoftTitle",SoftSub="villageSoftSub",SoftContent="villageSoftContent",
            WaterCamera="villageWaterCamera",WaterGuide="waterGuide",WaterCameraClose="waterCameraClose",WaterShutter="waterShutter",
            WaterResult="waterResult",WaterTitle="waterTitle",WaterAmount="waterAmount",WaterComment="waterComment";

        /// <summary>やわらかい全画面ページ (メニュー・記録) の部品 (ProductionApp.uss)。</summary>
        public const string SoftCard="village-soft-card",SoftCardHead="village-soft-card__head",SoftCardTitle="village-soft-card__title",
            SoftHero="village-soft-hero",SoftHeroMascot="village-soft-hero__mascot",SoftHeroName="village-soft-hero__name",SoftHeroRank="village-soft-hero__rank",
            SoftTiles="village-soft-tiles",SoftTile="village-soft-tile",SoftTileCircle="village-soft-tile__circle",SoftTileName="village-soft-tile__name",SoftTileSub="village-soft-tile__sub",
            SoftTips="village-soft-tips",SoftTip="village-soft-tip",SoftTipText="village-soft-tip__text",
            SoftNote="village-soft-note",
            RecordToday="village-record-today",RecordRing="village-record-ring",RecordRingCenter="village-record-ring__center",RecordSteps="village-record-steps",RecordStepsUnit="village-record-steps__unit",RecordRingHint="village-record-ring__hint",
            RecordChips="village-record-chips",RecordChip="village-record-chip",RecordChipValue="village-record-chip__value",RecordChipLabel="village-record-chip__label",
            RecordWeek="village-record-week",RecordWeekEmpty="village-record-week--empty",RecordBarCol="village-record-bar",RecordBarStack="village-record-bar__stack",RecordBarToday="village-record-bar--today",RecordBarLabel="village-record-bar__label",
            RecordBarGas="village-record-bar__gas",RecordBarElectricity="village-record-bar__electricity",RecordBarWater="village-record-bar__water",RecordBarDot="village-record-bar__dot",
            RecordDay="village-record-day",RecordLine="village-record-line",RecordLineTitle="village-record-line__title",RecordLineAmount="village-record-line__amount",
            RecordEmpty="village-record-empty",RecordEmptyMascot="village-record-empty__mascot";

        /// <summary>チュートリアルの吹き出しと光らせる枠 (ProductionApp.uss)。</summary>
        public const string CoachClass="village-coach",CoachTopClass="village-coach--top",CoachTargetClass="village-coach-target",CoachTargetOnClass="village-coach-target--on";

        /// <summary>ProductionApp.uss のクラス。</summary>
        public const string Hint="village-hint",HintBad="village-hint--bad",Card="village-card",CardHead="village-card__head",CardTitle="village-card__title",CardActions="village-card__actions",
            Subtle="village-subtle",Stat="village-stat",Empty="village-empty",EmptyText="village-empty__text",
            Shelf="village-shelf",ShelfRecipe="village-shelf-card__recipe",ShelfCost="village-shelf-card__cost",ShelfSoon="village-shelf-card__soon",
            Facility="village-facility",FacilityThumb="village-facility__thumb",FacilityBody="village-facility__body",FacilityProgress="village-facility__progress",
            Items="village-items",
            RankBadgeClass="village-rank-badge",RankLabelClass="village-rank-label",RankValueClass="village-rank-value",RankProgressClass="village-rank-progress",
            IndustrialSwitchClass="village-industrial-switch",
            TacticalBarClass="village-tactical-bar",TacticalTitleClass="village-tactical-title",TacticalGasClass="village-tactical-gas",TacticalBtnClass="village-tactical-btn",TacticalBtnActiveClass="village-tactical-btn--active",
            TacticalRowClass="village-tactical-row",NextStepClass="village-next-step",TacticalStatusClass="village-tactical-status",CameraRailClass="village-camera-rail",TacticalStatusBadClass="village-tactical-status--bad",TacticalGasEmptyClass="village-tactical-gas--empty",NextStepWarnClass="village-next-step--warn",GroupStateClass="village-group-state",GroupMarkClass="village-group-mark",GroupMarkWaitingClass="village-group-mark--waiting",SpatialLayerClass="village-spatial-layer",SpatialBadgeClass="village-spatial-badge",SpatialBadgeIconClass="village-spatial-badge__icon",SpatialBadgeWarnClass="village-spatial-badge--warn",SpatialBadgeSynergyClass="village-spatial-badge--synergy",
            InsightBannerClass="village-insight-banner",InsightBannerWarnClass="village-insight-banner--warn",
            QuickDockClass="village-quick-dock",DockHeaderClass="village-dock-header",DockTabsClass="village-dock-tabs",DockTabClass="village-dock-tab",DockTabActiveClass="village-dock-tab--active",
            DockScrollClass="village-dock-scroll",DockSlotClass="village-dock-slot",DockSlotSuggestedClass="village-dock-slot--suggested",DockSlotThumbClass="village-dock-slot__thumb",DockSlotNameClass="village-dock-slot__name",
            ProductionReportClass="village-production-report",WorkshopPageClass="village-workshop-page",WorkshopPageBodyClass="village-workshop-page__body";

        /// <summary>主役の数字が増えたときに一瞬ふくらませる (Components.uss)。</summary>
        public const string HeroBump="pp-hero--bump";
        public const string FacilitySheetClass="village-sheet--facility";
        public const string ComboDiagram="village-combo-diagram",ComboRow="village-combo-row",ComboCell="village-combo-cell",
            ComboCenter="village-combo-center",ComboDirection="village-combo-direction",ComboDirectionActive="village-combo-direction--active",
            ComboMaximum="village-combo--maximum",ComboHint="village-combo-hint",ComboBadge="village-combo-badge",SpatialBadgeCombo="village-spatial-badge--combo";
        public const string Effort="village-effort",EffortHeader="village-effort__header",EffortValue="village-effort__value",
            EffortFlame="village-effort__flame",EffortEffect="village-effort__effect",EffortHint="village-effort__hint",EffortActions="village-effort__actions",
            FacilityEffort="village-facility__effort";
    }
}
