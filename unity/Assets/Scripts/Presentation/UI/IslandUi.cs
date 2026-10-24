using System;
using System.Globalization;
using ProjectPenguin.Domain.Island;
using UnityEngine.UIElements;

namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// 島の画面で使う要素の組み立てと文言。見た目は USS のクラス (Components.uss) に任せ、ここではクラスを付けるだけ。
    /// </summary>
    public static class IslandUi
    {

        /// <summary>大きい数を短く (1,234 / 12.3万)。</summary>
        public static string Number(double value)
        {
            var v = Math.Floor(value + 1e-9);
            if (Math.Abs(v) >= 100_000_000)
            {
                return (v / 100_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "億";
            }

            if (Math.Abs(v) >= 100_000)
            {
                return (v / 10_000).ToString("0.#", CultureInfo.InvariantCulture) + "万";
            }

            return v.ToString("#,0", CultureInfo.InvariantCulture);
        }

        /// <summary>毎時の量 (小数 1 桁まで)。</summary>
        public static string Rate(double perHour) =>
            perHour >= 100 ? Number(perHour) : perHour.ToString("0.#", CultureInfo.InvariantCulture);

        public static string QualityAreaDescription(Item item, bool onLand, double speed)
        {
            var kind = item switch
            {
                Item.Fish => FacilityKind.Fishery,
                Item.Salt => FacilityKind.Saltworks,
                Item.Kelp => FacilityKind.KelpFarm,
                _ => (FacilityKind?)null,
            };
            if (!kind.HasValue) return null;
            var title = item == Item.Fish ? "豊漁のエリア" : item == Item.Salt ? "塩の結晶のエリア" : "海藻のエリア";
            var multiplier = speed.ToString("0.##", CultureInfo.InvariantCulture);
            return $"{title}\nこのマスに{IslandCatalog.Get(kind.Value).Name}を重ねると生産速度 ×{multiplier}" +
                   (onLand ? "" : "\n島を広げると使えるよ");
        }

        public static string IconClass(Item item) => item switch
        {
            Item.Fish => UiClasses.Icon.Fish,
            Item.Surimi => UiClasses.Icon.Surimi,
            Item.Salt => UiClasses.Icon.Salt,
            Item.Kelp => UiClasses.Icon.Kelp,
            Item.Kamaboko => UiClasses.Icon.Kamaboko,
            Item.Chikuwa => UiClasses.Icon.Chikuwa,
            Item.DriedFish => UiClasses.Icon.DriedFish,
            Item.Oden => UiClasses.Icon.Oden,
            _ => UiClasses.Icon.Fish,
        };

        public static string IconClass(Resource r) => r switch
        {
            Resource.Electricity => UiClasses.Icon.Electricity,
            Resource.Water => UiClasses.Icon.Water,
            Resource.Gas => UiClasses.Icon.Gas,
            Resource.AllMighty => UiClasses.Icon.AllMighty,
            Resource.Pearl => UiClasses.Icon.Pearl,
            _ => UiClasses.Icon.AllMighty,
        };

        public static string TintClass(Resource r) => r switch
        {
            Resource.Electricity => UiClasses.Tint.Electricity,
            Resource.Water => UiClasses.Tint.Water,
            Resource.Gas => UiClasses.Tint.Gas,
            Resource.AllMighty => UiClasses.Tint.AllMighty,
            Resource.Pearl => UiClasses.Tint.Pearl,
            _ => UiClasses.Tint.Primary,
        };

        /// <summary>施設を表すアイコン (作る品目か役割)。</summary>
        public static string IconClass(FacilityDef def)
        {
            if (def.Amplifies.HasValue)
            {
                return IconClass(def.Amplifies.Value);
            }

            return def.Kind switch
            {
                FacilityKind.Warehouse => UiClasses.Icon.StorageFull,
                FacilityKind.Dormitory => UiClasses.Icon.Penguin,
                FacilityKind.Research => UiClasses.Icon.Research,
                FacilityKind.Sorter => UiClasses.Icon.Deliver,
                FacilityKind.IceHouse => UiClasses.Icon.Snowflake,
                _ => def.Recipes.Count > 0 ? IconClass(def.Recipes[0].Output.Item) : UiClasses.Icon.Build,
            };
        }

        public static string TintClass(FacilityDef def) =>
            def.Amplifies.HasValue ? TintClass(def.Amplifies.Value)
            : def.Kind == FacilityKind.Research ? UiClasses.Tint.Pearl
            : def.Kind == FacilityKind.Dormitory ? UiClasses.Tint.Original
            : def.Kind == FacilityKind.IceHouse ? UiClasses.Tint.Water
            : UiClasses.Tint.Primary;

        /// <summary>状態の文言・アイコン・色のクラス (問題なしは null、<see cref="UiClasses.Status.Warn"/>、<see cref="UiClasses.Status.Bad"/>)。</summary>
        public static (string Text, string Icon, string Variant) Status(Facility f)
        {
            if (f.Def.Role != FacilityRole.Producer && f.Status == FacilityStatus.Running)
            {
                return ("はたらいているよ", UiClasses.Icon.Check, null);
            }

            return f.Status switch
            {
                FacilityStatus.Running => ("つくっているよ", UiClasses.Icon.Production, null),
                FacilityStatus.Submerged => ("沈んで止まっている", UiClasses.Icon.Warning, UiClasses.Status.Bad),
                FacilityStatus.NoCoast => ("海に面していない", UiClasses.Icon.NoCoast, UiClasses.Status.Bad),
                FacilityStatus.NoWorker => ("ペンギンが足りない", UiClasses.Icon.WorkerShortage, UiClasses.Status.Warn),
                FacilityStatus.NoGas => ("ガスが切れている", UiClasses.Icon.GasEmpty, UiClasses.Status.Bad),
                FacilityStatus.WaitingInput => ("材料を待っている", UiClasses.Icon.MaterialWait, UiClasses.Status.Warn),
                FacilityStatus.StorageFull => ("置き場がいっぱい", UiClasses.Icon.StorageFull, UiClasses.Status.Warn),
                FacilityStatus.NoFish => ("ごはんのお魚が足りない", UiClasses.Icon.Fish, UiClasses.Status.Bad),
                _ => ("", UiClasses.Icon.Check, null),
            };
        }

        /// <summary>操作に失敗したときにペンギンが言うこと。</summary>
        public static string Message(IslandActionResult r) => r switch
        {
            IslandActionResult.Ok => "",
            IslandActionResult.Locked => "まだ建てられないよ。お魚を増やすと新しい施設が開くよ",
            IslandActionResult.LimitReached => "これ以上は増やせないよ",
            IslandActionResult.CannotPlace => "そこには置けないよ",
            IslandActionResult.NotEnoughElectricity => "電気が足りないよ。エコ写真で集めよう",
            IslandActionResult.NotEnoughWater => "水が足りないよ。エコ写真で集めよう",
            IslandActionResult.NotEnoughFish => "お魚が足りないよ。港に届けて増やそう",
            IslandActionResult.NotEnoughPearls => "真珠が足りないよ。研究所で作れるよ",
            IslandActionResult.NotEnoughItems => "品物がまだそろっていないよ",
            IslandActionResult.NotEnoughIce => "氷が足りないよ。記録を続けると広げられるよ",
            IslandActionResult.NoFreeWorker => "手のあいているペンギンがいないよ",
            IslandActionResult.AlreadyReceived => "今日はもう受け取ったよ",
            IslandActionResult.NotArrived => "舟がまだ着いていないよ",
            IslandActionResult.MaxLevel => "もう最大のレベルだよ",
            _ => "うまくいかなかったよ",
        };

        // ------------------------------------------------------------------
        // 組み立て
        // ------------------------------------------------------------------

        public static VisualElement Icon(string iconClass, string tintClass, string sizeClass = null)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(UiClasses.Icon.Root);
            e.AddToClassList(iconClass);
            if (tintClass != null)
            {
                e.AddToClassList(tintClass);
            }

            if (sizeClass != null)
            {
                e.AddToClassList(sizeClass);
            }

            return e;
        }

        public static VisualElement IconBadge(string iconClass, string tintClass, bool eco = false)
        {
            var badge = new VisualElement { pickingMode = PickingMode.Ignore };
            badge.AddToClassList(UiClasses.IconBadge.Root);
            if (eco)
            {
                badge.AddToClassList(UiClasses.IconBadge.Eco);
            }

            badge.Add(Icon(iconClass, tintClass));
            return badge;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var c in classes)
            {
                l.AddToClassList(c);
            }

            return l;
        }

        /// <summary>シートの 1 行 (アイコン・見出し・補足・右端)。</summary>
        public static VisualElement Row(VisualElement leading, string title, string sub, VisualElement trailing = null)
        {
            var row = new VisualElement();
            row.AddToClassList(UiClasses.Row.Root);
            if (leading != null)
            {
                row.Add(leading);
            }

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList(UiClasses.Row.Body);
            body.Add(Text(title, UiClasses.Row.Title));
            if (!string.IsNullOrEmpty(sub))
            {
                body.Add(Text(sub, UiClasses.Row.Sub));
            }

            row.Add(body);
            if (trailing != null)
            {
                row.Add(trailing);
            }

            return row;
        }

        /// <summary>コストの並び。足りないものは赤くする。</summary>
        public static VisualElement Cost()
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(UiClasses.Cost.Root);
            return e;
        }

        public static void AddCost(VisualElement cost, string iconClass, string tintClass, double amount, bool short_)
        {
            if (amount <= 0)
            {
                return;
            }

            var item = new VisualElement { pickingMode = PickingMode.Ignore };
            item.AddToClassList(UiClasses.Cost.Item);
            if (short_)
            {
                item.AddToClassList(UiClasses.Cost.ItemShort);
            }

            var icon = Icon(iconClass, tintClass);
            icon.AddToClassList(UiClasses.Cost.Icon);
            item.Add(icon);
            item.Add(Text(Number(Math.Ceiling(amount)), UiClasses.Cost.Value));
            cost.Add(item);
        }

        public static VisualElement StatusPill(Facility f)
        {
            var (text, icon, variant) = Status(f);
            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList(UiClasses.Status.Root);
            if (variant != null)
            {
                pill.AddToClassList(variant);
            }

            var i = Icon(icon, null);
            i.AddToClassList(UiClasses.Status.Icon);
            pill.Add(i);
            pill.Add(Text(text, UiClasses.Status.Label));
            return pill;
        }

        /// <param name="variant">色のクラス (<see cref="UiClasses.Pill.Primary"/> など)。null なら白。</param>
        public static Button Pill(string text, Action onClick, string variant = null)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList(UiClasses.Pill.Root);
            if (variant != null)
            {
                b.AddToClassList(variant);
            }

            return b;
        }

        public static void SetVisible(VisualElement e, bool visible) => e?.EnableInClassList(UiClasses.Hidden, !visible);

        // ------------------------------------------------------------------
        // ゲームの画面 (Game.uss)
        // ------------------------------------------------------------------

        /// <summary>「アイコン … 30」。足りないときは数字を赤くする。</summary>
        public static VisualElement Count(string iconClass, string tintClass, string value, bool short_ = false)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(UiClasses.Count.Root);
            e.EnableInClassList(UiClasses.Count.Short, short_);
            var icon = Icon(iconClass, tintClass);
            icon.AddToClassList(UiClasses.Count.Icon);
            e.Add(icon);
            e.Add(Text(value, UiClasses.Count.Value));
            return e;
        }

        public static VisualElement Count(string iconClass, string tintClass, double amount, bool short_ = false) =>
            Count(iconClass, tintClass, "… " + Number(Math.Ceiling(amount)), short_);

        /// <summary>要素の建物の絵を差し替える (前の絵のクラスは外す)。</summary>
        public static void SetThumb(VisualElement e, string thumbClass)
        {
            if (e == null)
            {
                return;
            }

            if (e.userData is string previous && previous != thumbClass)
            {
                e.RemoveFromClassList(previous);
            }

            e.AddToClassList(UiClasses.Thumb.Root);
            if (thumbClass != null)
            {
                e.AddToClassList(thumbClass);
            }

            e.userData = thumbClass;
        }

        /// <summary>作り方を「材料 … 3 → できるもの」のアイコンで並べる。材料の無いものは「→ できるもの」だけ。</summary>
        public static void FillRecipe(VisualElement container, FacilityKind kind, Recipe recipe, Func<Item, bool> isShort = null)
        {
            container.Clear();
            if (recipe == null)
            {
                return;
            }

            foreach (var input in recipe.Inputs)
            {
                container.Add(Count(IconClass(input.Item), UiClasses.Tint.Fish, $"… {input.Count}", isShort?.Invoke(input.Item) ?? false));
            }

            var arrow = Icon(UiClasses.Icon.ArrowRight, UiClasses.Tint.Primary, UiClasses.Icon.Large);
            arrow.AddToClassList(UiClasses.Recipe.Arrow);
            container.Add(arrow);
            container.Add(IslandCatalog.ProducesPearl(kind)
                ? Count(UiClasses.Icon.Pearl, UiClasses.Tint.Pearl, "")
                : Count(IconClass(recipe.Output.Item), UiClasses.Tint.Fish, recipe.Output.Count > 1 ? $"× {recipe.Output.Count}" : ""));
        }

        /// <summary>作らない施設の効果のアイコン。</summary>
        public static string EffectIcon(FacilityDef def) => def.Role switch
        {
            FacilityRole.Storage => UiClasses.Icon.StorageFull,
            FacilityRole.Housing => UiClasses.Icon.Penguin,
            FacilityRole.Amplifier => IconClass(def.Amplifies.GetValueOrDefault()),
            FacilityRole.AutoDeliver => UiClasses.Icon.Deliver,
            FacilityRole.IceGuard => UiClasses.Icon.Snowflake,
            _ => IconClass(def),
        };

        /// <summary>作らない施設の効果を短く (建てる前の説明用。レベル 1 の値)。</summary>
        public static string EffectShort(FacilityDef def, IslandParameters p) => def.Role switch
        {
            FacilityRole.Storage => $"置き場 +{p.StoragePerWarehouse}",
            FacilityRole.Housing => $"住める数 +{p.HousingPerDormitory}",
            FacilityRole.Amplifier => $"{IslandCatalog.NameOf(def.Amplifies.GetValueOrDefault())} +{p.AmplifierBonus * 100:0}%",
            FacilityRole.AutoDeliver => "自動でとどける",
            FacilityRole.IceGuard => $"溶ける氷 −{p.IceHouseReduction * 100:0}%",
            _ => "",
        };

        /// <summary>解放の条件 (「お魚 1,000 匹で開く」など)。</summary>
        public static string UnlockText(long fish) => $"お魚 {Number(fish)} 匹で開く";

        /// <summary>鍵の上に出す短い数 (「🐟 1,000」の数字だけ)。</summary>
        public static string UnlockShort(long fish) => Number(fish);

        /// <summary>ペンギンのやる気 (×1.15 など)。</summary>
        public static string Motivation(double m) => "×" + m.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
