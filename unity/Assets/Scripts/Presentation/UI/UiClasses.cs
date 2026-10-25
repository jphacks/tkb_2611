using System;
using ProjectPenguin.Domain.Island;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// C# から付け外しする USS のクラス名。<c>AddToClassList("pp-hidden")</c> のように文字列で書かず、ここの定数を使う。
    /// 部品 (BEM のブロック) ごとに入れ子のクラスを 1 つ置き、ブロックそのものは <c>Root</c> にする。
    /// どの定数も USS のセレクタか UXML の class 属性に実在することを EditMode の UiNamesTests が確かめる。
    /// 部品の見た目と使い方は docs/design-system.md を参照。
    /// </summary>
    public static class UiClasses
    {
        /// <summary>要素を隠す (display: none)。</summary>
        public const string Hidden = "pp-hidden";

        public const string Card = "pp-card";
        public const string Elevated = "pp-elevated";
        public const string QuickEcoList = "home__quick-eco-list";
        public const string QuickEcoCard = "home__quick-eco-card";
        public const string RaidVenue="raid--venue", RaidOption="raid__option", Button="pp-btn", ButtonBlock="pp-btn--block";
        public const string RaidReading = "raid--reading";
        public const string ClassroomLandscape = "classroom-landscape";
        public const string ClassroomSheet = "classroom-sheet";
        public const string ClassroomReading = "classroom-reading";
        public const string SectionTitle = "pp-section-title";

        /// <summary>アイコンと数の横並び (Game.uss)。</summary>
        public const string Counts = "pp-counts";

        /// <summary>文字の大きさ・太さ・色。</summary>
        public static class Text
        {
            public const string Title = "pp-text-title";
            public const string Label = "pp-text-label";
            public const string Caption = "pp-text-caption";
            public const string Bold = "pp-text-bold";
            public const string Heavy = "pp-text-heavy";
            public const string Sub = "pp-text-sub";
        }

        /// <summary>アイコンの形 (SVG)。色は <see cref="Tint"/> と組み合わせる。</summary>
        public static class Icon
        {
            public const string Root = "pp-icon";

            // 資源
            public const string Electricity = "pp-icon--electricity";
            public const string Water = "pp-icon--water";
            public const string Gas = "pp-icon--gas";
            public const string AllMighty = "pp-icon--allmighty";
            public const string Pearl = "pp-icon--pearl";

            // 品目
            public const string Fish = "pp-icon--fish";
            public const string Surimi = "pp-icon--surimi";
            public const string Salt = "pp-icon--salt";
            public const string Kelp = "pp-icon--kelp";
            public const string Kamaboko = "pp-icon--kamaboko";
            public const string Chikuwa = "pp-icon--chikuwa";
            public const string DriedFish = "pp-icon--dried-fish";
            public const string Oden = "pp-icon--oden";

            // 操作と施設
            public const string Build = "pp-icon--build";
            public const string Camera = "pp-icon--camera";
            public const string Deliver = "pp-icon--deliver";
            public const string ExpandIsland = "pp-icon--expand-island";
            public const string Flag = "pp-icon--flag";
            public const string Leaf = "pp-icon--leaf";
            public const string Penguin = "pp-icon--penguin";
            public const string Production = "pp-icon--production";
            public const string RecipeHigh = "pp-icon--recipe-high";
            public const string Research = "pp-icon--research";
            public const string TimeForward = "pp-icon--time-forward";

            // 状態 (止まった理由)
            public const string Check = "pp-icon--check";
            public const string Warning = "pp-icon--warning";
            public const string NoCoast = "pp-icon--no-coast";
            public const string WorkerShortage = "pp-icon--worker-shortage";
            public const string GasEmpty = "pp-icon--gas-empty";
            public const string MaterialWait = "pp-icon--material-wait";
            public const string StorageFull = "pp-icon--storage-full";

            // ゲームの画面 (Game.uss)
            public const string Close = "pp-icon--close";
            public const string Lock = "pp-icon--lock";
            public const string Clock = "pp-icon--clock";
            public const string Boat = "pp-icon--boat";
            public const string ArrowRight = "pp-icon--arrow-right";
            public const string Upgrade = "pp-icon--upgrade";

            /// <summary>氷・氷室 (雪の結晶)。</summary>
            public const string Snowflake = "pp-icon--snowflake";

            // 村のホーム (ProductionApp.uxml) のシート・工業モード
            public const string Move = "pp-icon--move";
            public const string Help = "pp-icon--help";
            public const string Connect = "pp-icon--connect";
            public const string Book = "pp-icon--book";
            public const string Demolish = "pp-icon--demolish";
            public const string Undo = "pp-icon--undo";
            public const string Rotate = "pp-icon--rotate";
            public const string Crown = "pp-icon--crown";
            public const string Chevrons = "pp-icon--chevrons";
            public const string TopDown = "pp-icon--topdown";

            public const string Large = "pp-icon--lg";
            public const string ExtraLarge = "pp-icon--xl";
        }

        /// <summary>アイコンの色。</summary>
        public static class Tint
        {
            public const string Primary = "pp-icon--primary";
            public const string Eco = "pp-icon--eco";
            public const string Point = "pp-icon--point";
            public const string Danger = "pp-icon--danger";
            public const string Sub = "pp-icon--sub";

            /// <summary>SVG の色のまま (白で掛ける)。</summary>
            public const string Original = "pp-icon--original";

            public const string Electricity = "pp-icon--res-electricity";
            public const string Water = "pp-icon--res-water";
            public const string Gas = "pp-icon--res-gas";
            public const string AllMighty = "pp-icon--res-allmighty";
            public const string Fish = "pp-icon--res-fish";
            public const string Pearl = "pp-icon--res-pearl";
        }

        public static class IconBadge
        {
            public const string Root = "pp-icon-badge";
            public const string Eco = "pp-icon-badge--eco";
        }

        public static class Badge
        {
            public const string Root = "pp-badge";
            public const string Eco = "pp-badge--eco";
            public const string Accent = "pp-badge--accent";
        }

        /// <summary>ボタンの変種 (<see cref="Button"/> と組み合わせる)。</summary>
        public static class Btn
        {
            /// <summary>下縁で厚みを出し、押すと沈む主ボタン。</summary>
            public const string Raised = "pp-btn--raised";
            public const string Ghost = "pp-btn--ghost";
            public const string Icon = "pp-btn__icon";
        }

        /// <summary>シートの 1 行。</summary>
        public static class Row
        {
            public const string Root = "pp-row";
            public const string Body = "pp-row__body";
            public const string Title = "pp-row__title";
            public const string Sub = "pp-row__sub";
            public const string Last = "pp-row--last";
            public const string Locked = "pp-row--locked";
        }

        /// <summary>コストの並び。</summary>
        public static class Cost
        {
            public const string Root = "pp-cost";
            public const string Item = "pp-cost__item";
            public const string ItemShort = "pp-cost__item--short";
            public const string Icon = "pp-cost__icon";
            public const string Value = "pp-cost__value";
        }

        /// <summary>施設の状態の札。</summary>
        public static class Status
        {
            public const string Root = "pp-status";
            public const string Icon = "pp-status__icon";
            public const string Label = "pp-status__label";
            public const string Warn = "pp-status--warn";
            public const string Bad = "pp-status--bad";
        }

        /// <summary>小さな丸いボタン。</summary>
        public static class Pill
        {
            public const string Root = "pp-pill";
            public const string Primary = "pp-pill--primary";
            public const string Danger = "pp-pill--danger";
            public const string Selected = "pp-pill--selected";
        }

        public static class Stepper
        {
            public const string Root = "pp-stepper";
            public const string Button = "pp-stepper__button";
            public const string Value = "pp-stepper__value";
        }

        public static class Progress
        {
            public const string Root = "pp-progress";
            public const string Eco = "pp-progress--eco";
            public const string Point = "pp-progress--point";
        }

        /// <summary>写真の判定中の段階。</summary>
        public static class Step
        {
            public const string Active = "pp-step--active";
            public const string Done = "pp-step--done";
        }

        /// <summary>記録画面の棒グラフ。</summary>
        public static class BarChart
        {
            public const string Column = "pp-bar-chart__col";
            public const string Bar = "pp-bar-chart__bar";
            public const string BarToday = "pp-bar-chart__bar--today";
            public const string Value = "pp-bar-chart__value";
        }

        /// <summary>ホームの資源の帯の 1 つ。</summary>
        public static class Resource
        {
            public const string Bump = "pp-resource--bump";
            public const string Empty = "pp-resource--empty";
        }

        /// <summary>受け取った資源が飛んでいく「+20」。</summary>
        public static class Gain
        {
            public const string Root = "pp-gain";
            public const string Value = "pp-gain__value";
            public const string Gone = "pp-gain--gone";
        }

        public static class Toast
        {
            public const string Root = "pp-toast";
            public const string Hidden = "pp-toast--hidden";
        }

        public static class Sheet
        {
            public const string Closed = "pp-sheet--closed";
        }

        public static class Screen
        {
            public const string Hidden = "pp-screen--hidden";

            /// <summary>表示した直後だけ付け、外すと下からふわっと入る。</summary>
            public const string Entering = "pp-screen--entering";
        }

        /// <summary>ホームの右下の丸い道具。</summary>
        public static class Tool
        {
            public const string Active = "pp-tool--active";
        }

        public static class Tab
        {
            public const string Active = "pp-tab--active";
        }

        public static class SafeArea
        {
            public const string Root = "pp-safe-area";
            public const string Bottom = "pp-safe-area__bottom";
        }

        public static class DexCard
        {
            public const string Root = "pp-dex-card";
            public const string Locked = "pp-dex-card--locked";
            public const string Image = "pp-dex-card__image";
            public const string Name = "pp-dex-card__name";
            public const string Fact = "pp-dex-card__fact";
            public const string Bonus = "pp-dex-card__bonus";
        }

        public static class MissionCard
        {
            public const string Root = "pp-mission-card";
            public const string Title = "pp-mission-card__title";
            public const string Body = "pp-mission-card__body";
        }

        // ---- 画面ごとのクラス (各画面の USS) ----

        /// <summary>HomeScreen.uss</summary>
        public static class Home
        {
            public const string DemoVisible = "home__demo--visible";

            /// <summary>生産の島のシーンでだけ出すデモの操作 (目印。見た目は付けていない)。</summary>
            public const string DemoIsland = "home__demo-island";

            public const string StatNoteDanger = "home__stat-note--danger";

            /// <summary>置き場所を選んでいる間 (吹き出しと道具を隠す)。</summary>
            public const string Placing = "home--placing";

            /// <summary>はじめての案内を出している間 (HUD を隠して島だけを見せる)。</summary>
            public const string Onboarding = "home--onboarding";
        }

        /// <summary>HomeScreen.uss の「資源の集め方」シート。</summary>
        public static class IslandGuide
        {
            public const string Lead = "island-guide__lead";
            public const string Today = "island-guide__today";
            public const string Amount = "island-guide__amount";
        }

        /// <summary>MissionScreen.uss</summary>
        public static class Mission
        {
            public const string Card = "mission__card";
            public const string CardSelected = "mission__card--selected";
            public const string CardDone = "mission__card--done";
            public const string Hint = "mission__hint";
            public const string Answers = "mission__answers";
        }

        /// <summary>Onboarding.uss</summary>
        public static class Onboarding
        {
            public const string DotActive = "onboarding__dot--active";
        }

        /// <summary>FacilityReview.uss (施設のデザイン確認用の画面。開発用)。</summary>
        public static class FacilityReview
        {
            public const string Panel = "review-panel";
            public const string Choice = "facility-choice";
            public const string ChoiceSelected = "selected";
        }

        // ---- ゲームの画面の部品 (Game.uss) ----

        /// <summary>建物の一覧の棚のカード。</summary>
        public static class ShelfCard
        {
            public const string Root = "pp-shelf-card";
            public const string Thumb = "pp-shelf-card__thumb";
            public const string Name = "pp-shelf-card__name";
            public const string Badge = "pp-shelf-card__badge";
            public const string New = "pp-shelf-card__new";
            public const string Locked = "pp-shelf-card--locked";
        }

        /// <summary>鍵とレベル (まだ建てられない)。</summary>
        public static class Lock
        {
            public const string Root = "pp-lock";
            public const string Label = "pp-lock__label";
        }

        /// <summary>大きな写真のカード。</summary>
        public static class PhotoCard
        {
            public const string Locked = "pp-photo-card--locked";
            public const string Swap = "pp-photo-card--swap";
        }

        /// <summary>「アイコン … 30」。</summary>
        public static class Count
        {
            public const string Root = "pp-count";
            public const string Icon = "pp-count__icon";
            public const string Value = "pp-count__value";
            public const string Short = "pp-count--short";
        }

        public static class Recipe
        {
            public const string Root = "pp-recipe";
            public const string Arrow = "pp-recipe__arrow";
        }

        /// <summary>大きな丸いボタン。</summary>
        public static class RoundButton
        {
            public const string Root = "pp-round-button";
            public const string Icon = "pp-round-button__icon";
            public const string Label = "pp-round-button__label";
            public const string Small = "pp-round-button--small";
            public const string Primary = "pp-round-button--primary";
        }

        /// <summary>小さな丸い道具。</summary>
        public static class MiniButton
        {
            public const string Root = "pp-mini-button";
        }

        /// <summary>注文などの横長のカード。</summary>
        public static class Tile
        {
            public const string Root = "pp-tile";
            public const string Art = "pp-tile__art";
            public const string Body = "pp-tile__body";
            public const string Big = "pp-tile__big";
            public const string Small = "pp-tile__small";
            public const string Waiting = "pp-tile--waiting";
        }

        /// <summary>品物の一覧のカード。</summary>
        public static class ItemCard
        {
            public const string Root = "pp-item-card";
            public const string Value = "pp-item-card__value";
            public const string Rate = "pp-item-card__rate";
            public const string Short = "pp-item-card--short";
            public const string Idle = "pp-item-card--idle";
        }

        public static class Worker
        {
            public const string Root = "pp-worker";
            public const string Empty = "pp-worker--empty";
        }

        public static class LevelPip
        {
            public const string Root = "pp-level-pip";
            public const string On = "pp-level-pip--on";
        }

        /// <summary>
        /// 建物の絵 (Thumbnails.uss)。PNG と USS は Production Island &gt; Render Facility Thumbnails が描き出す。
        /// 描き出しもここの定数を使うので、施設を足したらここに足してから描き直す。
        /// </summary>
        public static class Thumb
        {
            public const string Root = "pp-thumb";

            public const string Fishery = "pp-thumb--fishery";
            public const string Preparation = "pp-thumb--preparation";
            public const string Saltworks = "pp-thumb--saltworks";
            public const string KelpFarm = "pp-thumb--kelp-farm";
            public const string Kamaboko = "pp-thumb--kamaboko";
            public const string Chikuwa = "pp-thumb--chikuwa";
            public const string Drying = "pp-thumb--drying";
            public const string Oden = "pp-thumb--oden";
            public const string Warehouse = "pp-thumb--warehouse";
            public const string Dormitory = "pp-thumb--dormitory";
            public const string Research = "pp-thumb--research";
            public const string Sorter = "pp-thumb--sorter";
            public const string ElectricAmplifier = "pp-thumb--electric-amplifier";
            public const string WaterAmplifier = "pp-thumb--water-amplifier";
            public const string GasAmplifier = "pp-thumb--gas-amplifier";
            public const string IceHouse = "pp-thumb--ice-house";

            public const string Harbor0 = "pp-thumb--harbor-0";
            public const string Harbor1 = "pp-thumb--harbor-1";
            public const string Harbor2 = "pp-thumb--harbor-2";
            public const string Harbor3 = "pp-thumb--harbor-3";
            public const string Harbor4 = "pp-thumb--harbor-4";
            public const string Harbor5 = "pp-thumb--harbor-5";

            /// <summary>島を広げる (凍らせた海のマス)。</summary>
            public const string Expand = "pp-thumb--expand";

            public static string Of(FacilityKind kind) => kind switch
            {
                FacilityKind.Fishery => Fishery,
                FacilityKind.Preparation => Preparation,
                FacilityKind.Saltworks => Saltworks,
                FacilityKind.KelpFarm => KelpFarm,
                FacilityKind.Kamaboko => Kamaboko,
                FacilityKind.Chikuwa => Chikuwa,
                FacilityKind.Drying => Drying,
                FacilityKind.Oden => Oden,
                FacilityKind.Warehouse => Warehouse,
                FacilityKind.Dormitory => Dormitory,
                FacilityKind.Research => Research,
                FacilityKind.Sorter => Sorter,
                FacilityKind.ElectricAmplifier => ElectricAmplifier,
                FacilityKind.WaterAmplifier => WaterAmplifier,
                FacilityKind.GasAmplifier => GasAmplifier,
                FacilityKind.IceHouse => IceHouse,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };

            public static readonly string[] Harbors = { Harbor0, Harbor1, Harbor2, Harbor3, Harbor4, Harbor5 };

            /// <summary>港の段階 (0〜5) の絵。</summary>
            public static string OfHarbor(int stage) => Harbors[Math.Max(0, Math.Min(Harbors.Length - 1, stage))];

            public const string ItemFish = "pp-thumb--item-fish";
            public const string ItemSurimi = "pp-thumb--item-surimi";
            public const string ItemSalt = "pp-thumb--item-salt";
            public const string ItemKelp = "pp-thumb--item-kelp";
            public const string ItemKamaboko = "pp-thumb--item-kamaboko";
            public const string ItemChikuwa = "pp-thumb--item-chikuwa";
            public const string ItemDriedFish = "pp-thumb--item-dried-fish";
            public const string ItemOden = "pp-thumb--item-oden";

            public static string OfItem(Item item) => item switch
            {
                Item.Fish => ItemFish,
                Item.Surimi => ItemSurimi,
                Item.Salt => ItemSalt,
                Item.Kelp => ItemKelp,
                Item.Kamaboko => ItemKamaboko,
                Item.Chikuwa => ItemChikuwa,
                Item.DriedFish => ItemDriedFish,
                Item.Oden => ItemOden,
                _ => ItemFish,
            };
        }
    }
}
