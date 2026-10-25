using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Game;

namespace ProjectPenguin.Domain.Island
{
    /// <summary>写真や記録日とは独立した、自己申告の短いエコ行動。</summary>
    public sealed class QuickEcoActionDef
    {
        public QuickEcoActionDef(string id, string title, string description)
        {
            Id = id;
            Title = title;
            Description = description;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
    }

    public static class QuickEcoActions
    {
        public const double BaseElectricity = 3;
        public const int DailyLimit = 3;
        public static IReadOnlyList<QuickEcoActionDef> All { get; } = Array.AsReadOnly(new[]
        {
            new QuickEcoActionDef("switch_off_unused_light", "使っていない照明を消す", "自分で操作できる、使っていない照明を消してみよう。"),
            new QuickEcoActionDef("unplug_unused_device", "不要な機器のプラグを抜く", "抜いてよい、使っていない機器だけを選ぼう。常時通電が必要な機器はそのままで。"),
            new QuickEcoActionDef("adjust_air_conditioner", "冷暖房の設定を調整する", "無理のない範囲で、冷房は設定を上げ、暖房は下げてみよう。体調を優先してね。"),
        });

        public static QuickEcoActionDef Find(string id) => All.FirstOrDefault(a => a.Id == id);
    }

    /// <summary>写真に写ったエコ活動の分類。AI (Hack Day で作る backend) かモックが決める。</summary>
    public enum EcoCategory
    {
        /// <summary>電気を節約する (消灯・冷暖房・待機電力)。</summary>
        SaveElectricity,

        /// <summary>水を節約する (シャワー・洗い物・節水)。</summary>
        SaveWater,

        /// <summary>移動を変える (徒歩・自転車・公共交通)。</summary>
        Move,

        /// <summary>食べ物 (食べきり・地元の食材・植物性の食事)。</summary>
        Food,

        /// <summary>物を大事にする (マイボトル・エコバッグ・リユース・分別)。</summary>
        Goods,

        /// <summary>エコ活動が写っていない、または判定できない。</summary>
        Unknown,

        /// <summary>
        /// ガスを節約する (鍋にふた・まとめて調理・重ね着で暖房を控える)。インフラ (電気・ガス・水道) それぞれにまつわるエコで
        /// それぞれの資源が取れるよう 2026-10-17 に足した。保存 (受け取りの記録) は数値なので末尾に置く。
        /// </summary>
        SaveGas,
    }

    /// <summary>
    /// 写真に写っているもの。もらえる資源は <see cref="EcoSubjects"/> の表で決まる
    /// (docs/production-island/resources.md の「写真の表」)。判定 (AI・モック) は、このどれかを返す。
    /// </summary>
    public enum EcoSubject
    {
        /// <summary>表のどれでもない (カテゴリだけで決める古い判定、または判定できない写真)。</summary>
        None,
        LightsOff,
        AirConditioner,
        ShortShower,
        SaveWater,
        MyBottle,
        EcoBag,
        Walk,
        Bicycle,
        Train,
        CleanPlate,
        LocalFood,
        Recycle,
        LidCooking,
        WarmClothes,
    }

    /// <summary>写真の表の 1 行: 写っているもの・名前・カテゴリ・もらえる資源。</summary>
    public sealed class EcoSubjectDef
    {
        public EcoSubjectDef(EcoSubject subject, string id, string name, EcoCategory category, params (Resource Resource, double Amount)[] grants)
        {
            Subject = subject;
            Id = id;
            Name = name;
            Category = category;
            Grants = grants;
        }

        public EcoSubject Subject { get; }

        /// <summary>backend の API で使う ID (「my_bottle」など)。</summary>
        public string Id { get; }

        /// <summary>画面に出す名前 (「マイボトル」など)。</summary>
        public string Name { get; }

        public EcoCategory Category { get; }
        public IReadOnlyList<(Resource Resource, double Amount)> Grants { get; }

        public ResourceBundle Bundle()
        {
            var b = new ResourceBundle();
            foreach (var (r, v) in Grants)
            {
                b[r] += v;
            }

            return b;
        }
    }

    /// <summary>
    /// 写真の表。「マイボトルなら水 +10・オールマイティ +10」のように、写っているものごとに量を決める。
    /// 量を変えるときはここだけ直す (ゲーム内の「資源の集め方」もここから読む)。
    /// </summary>
    public static class EcoSubjects
    {
        public static readonly IReadOnlyList<EcoSubjectDef> All = new[]
        {
            new EcoSubjectDef(EcoSubject.LightsOff, "lights_off", "消した電気", EcoCategory.SaveElectricity, (Resource.Electricity, 20)),
            new EcoSubjectDef(EcoSubject.AirConditioner, "air_conditioner", "エアコンの温度設定", EcoCategory.SaveElectricity, (Resource.Electricity, 20)),
            new EcoSubjectDef(EcoSubject.ShortShower, "short_shower", "短いシャワー", EcoCategory.SaveWater, (Resource.Water, 15), (Resource.Gas, 10)),
            new EcoSubjectDef(EcoSubject.SaveWater, "save_water", "蛇口をしめる・ため洗い", EcoCategory.SaveWater, (Resource.Water, 15)),
            new EcoSubjectDef(EcoSubject.MyBottle, "my_bottle", "マイボトル", EcoCategory.Goods, (Resource.Water, 10), (Resource.AllMighty, 10)),
            new EcoSubjectDef(EcoSubject.EcoBag, "eco_bag", "エコバッグ", EcoCategory.Goods, (Resource.AllMighty, 15)),
            new EcoSubjectDef(EcoSubject.LidCooking, "lid_cooking", "鍋にふた・まとめて調理", EcoCategory.SaveGas, (Resource.Gas, 20)),
            new EcoSubjectDef(EcoSubject.WarmClothes, "warm_clothes", "重ね着で暖房をひかえる", EcoCategory.SaveGas, (Resource.Gas, 15)),
            new EcoSubjectDef(EcoSubject.Walk, "walk", "歩いて移動", EcoCategory.Move, (Resource.Gas, 20)),
            new EcoSubjectDef(EcoSubject.Bicycle, "bicycle", "自転車", EcoCategory.Move, (Resource.Gas, 20)),
            new EcoSubjectDef(EcoSubject.Train, "train", "電車・バス", EcoCategory.Move, (Resource.Gas, 15)),
            new EcoSubjectDef(EcoSubject.CleanPlate, "clean_plate", "食べきったお皿", EcoCategory.Food, (Resource.AllMighty, 15)),
            new EcoSubjectDef(EcoSubject.LocalFood, "local_food", "地元の食材", EcoCategory.Food, (Resource.AllMighty, 10), (Resource.Water, 5)),
            new EcoSubjectDef(EcoSubject.Recycle, "recycle", "分別・リサイクル", EcoCategory.Goods, (Resource.AllMighty, 10), (Resource.Electricity, 5)),
        };

        /// <summary>エコ活動が写っていない・判定できない写真でも、記録にはなる。少しだけ渡す。</summary>
        public static readonly (Resource Resource, double Amount)[] UnknownGrants =
        {
            (Resource.Electricity, 3), (Resource.Water, 3), (Resource.Gas, 3),
        };

        private static readonly Dictionary<EcoSubject, EcoSubjectDef> BySubject = All.ToDictionary(d => d.Subject);
        private static readonly Dictionary<string, EcoSubjectDef> ById = All.ToDictionary(d => d.Id);

        public static EcoSubjectDef Find(EcoSubject subject) => BySubject.TryGetValue(subject, out var d) ? d : null;

        /// <summary>backend の ID から引く。知らない ID は null (判定できない写真として扱う)。</summary>
        public static EcoSubjectDef FindById(string id) => id != null && ById.TryGetValue(id, out var d) ? d : null;
    }

    /// <summary>写真の判定結果。画像そのものは持たない (端末の外にも残さない)。</summary>
    public sealed class EcoPhotoVerdict
    {
        public EcoPhotoVerdict(EcoCategory category, string label, string comment, double confidence)
        {
            Category = category;
            Label = label ?? "";
            Comment = comment ?? "";
            Confidence = confidence;
        }

        /// <summary>写真の表のどれかとして判定した結果。カテゴリと名前は表から決まる。</summary>
        public EcoPhotoVerdict(EcoSubject subject, string comment, double confidence, EcoPhotoReview review = null)
        {
            var def = EcoSubjects.Find(subject);
            Subject = def != null ? subject : EcoSubject.None;
            Category = def?.Category ?? EcoCategory.Unknown;
            Label = def?.Name ?? "";
            Comment = comment ?? "";
            Confidence = confidence;
            Review = review ?? EcoPhotoReview.None;
        }

        public EcoCategory Category { get; }

        /// <summary>写真の表のどれか。<see cref="EcoSubject.None"/> ならカテゴリだけで資源を決める。</summary>
        public EcoSubject Subject { get; }

        /// <summary>写っていたもの (「マイボトル」など)。</summary>
        public string Label { get; }

        /// <summary>ペンギンのひとこと。</summary>
        public string Comment { get; }

        public double Confidence { get; }

        /// <summary>判定サーバーの検証の結果。モックと古い形の応答では <see cref="EcoPhotoReview.None"/>。</summary>
        public EcoPhotoReview Review { get; } = EcoPhotoReview.None;

        public bool IsEco => Category != EcoCategory.Unknown;
    }

    /// <summary>資源の増減の組。</summary>
    public sealed class ResourceBundle
    {
        private readonly double[] _values = new double[Enum.GetValues(typeof(Resource)).Length];

        public double this[Resource r]
        {
            get => _values[(int)r];
            set => _values[(int)r] = value;
        }

        public bool IsEmpty => _values.All(v => Math.Abs(v) < 1e-9);

        public IEnumerable<KeyValuePair<Resource, double>> NonZero()
        {
            for (var i = 0; i < _values.Length; i++)
            {
                if (Math.Abs(_values[i]) > 1e-9)
                {
                    yield return new KeyValuePair<Resource, double>((Resource)i, _values[i]);
                }
            }
        }

        public static ResourceBundle Of(Resource r, double v)
        {
            var b = new ResourceBundle();
            b[r] = v;
            return b;
        }
    }

    /// <summary>カテゴリごとの説明と、どの資源に効くか。</summary>
    public static class EcoCategories
    {
        public static string NameOf(EcoCategory c) => c switch
        {
            EcoCategory.SaveElectricity => "電気の節約",
            EcoCategory.SaveWater => "水の節約",
            EcoCategory.SaveGas => "ガスの節約",
            EcoCategory.Move => "エコな移動",
            EcoCategory.Food => "食べ物を大切に",
            EcoCategory.Goods => "物を大切に",
            _ => "エコ活動",
        };

        /// <summary>主に増える資源。食べ物と物はどれにでも使えるオールマイティ。</summary>
        public static Resource MainResource(EcoCategory c) => c switch
        {
            EcoCategory.SaveElectricity => Resource.Electricity,
            EcoCategory.SaveWater => Resource.Water,
            EcoCategory.SaveGas => Resource.Gas,
            EcoCategory.Move => Resource.Gas,
            _ => Resource.AllMighty,
        };
    }

    /// <summary>ミッション 1 件 (アプリ独自の文言。値の出典を要するものは載せない)。</summary>
    public sealed class MissionDef
    {
        public MissionDef(string id, string title, string hint, EcoCategory category)
        {
            Id = id;
            Title = title;
            Hint = hint;
            Category = category;
        }

        public string Id { get; }
        public string Title { get; }
        public string Hint { get; }
        public EcoCategory Category { get; }
    }

    /// <summary>ミッションの一覧と、日付で決まる今日の 3 件。</summary>
    public static class MissionCatalog
    {
        public const int DailyChoices = 3;

        public static readonly IReadOnlyList<MissionDef> All = new[]
        {
            new MissionDef("M01", "マイボトルを持ち歩く", "飲み物はボトルに入れて出かけよう", EcoCategory.Goods),
            new MissionDef("M02", "エコバッグで買い物する", "レジ袋をもらわずに帰ろう", EcoCategory.Goods),
            new MissionDef("M03", "使っていない部屋の電気を消す", "出るときにスイッチを確かめよう", EcoCategory.SaveElectricity),
            new MissionDef("M04", "シャワーを 1 分短くする", "髪を洗う間はお湯を止めよう", EcoCategory.SaveWater),
            new MissionDef("M05", "ごはんを食べきる", "食べられる分だけよそおう", EcoCategory.Food),
            new MissionDef("M06", "ひと駅ぶん歩く", "天気が良ければ歩いてみよう", EcoCategory.Move),
            new MissionDef("M07", "エレベーターより階段を使う", "2〜3 階なら階段で", EcoCategory.Move),
            new MissionDef("M08", "冷暖房の温度を 1℃ ゆるめる", "服で調整してみよう", EcoCategory.SaveElectricity),
            new MissionDef("M09", "肉の代わりに豆腐や豆を食べる", "1 食だけ植物のたんぱく質に", EcoCategory.Food),
            new MissionDef("M10", "地元でとれた野菜を選ぶ", "産地の表示を見てみよう", EcoCategory.Food),
            new MissionDef("M11", "コンセントを抜いて待機電力を減らす", "使わない家電のプラグを抜こう", EcoCategory.SaveElectricity),
            new MissionDef("M12", "洗い物のときに水を流しっぱなしにしない", "おけにためて洗おう", EcoCategory.SaveWater),
            new MissionDef("M13", "ペットボトルを分別して出す", "キャップとラベルを外そう", EcoCategory.Goods),
            new MissionDef("M14", "自転車で出かける", "近い所は自転車で", EcoCategory.Move),
            new MissionDef("M15", "冷蔵庫を開ける回数を減らす", "何を取るか決めてから開けよう", EcoCategory.SaveElectricity),
            new MissionDef("M16", "歯みがき中に水を止める", "コップに水をくもう", EcoCategory.SaveWater),
        };

        private static readonly Dictionary<string, MissionDef> ById = All.ToDictionary(m => m.Id);

        public static MissionDef Find(string id) => id != null && ById.TryGetValue(id, out var m) ? m : null;

        /// <summary>その日の 3 件。日付を種にするので何度開いても同じ (docs/game-logic.md §3)。</summary>
        public static IReadOnlyList<MissionDef> DailyFor(int appDay)
        {
            var rng = new SplitMix64((ulong)(appDay * 7919L + 17));
            var pool = All.ToList();
            var picked = new List<MissionDef>();
            while (picked.Count < DailyChoices && pool.Count > 0)
            {
                var i = rng.NextInt(pool.Count);
                picked.Add(pool[i]);
                pool.RemoveAt(i);
            }

            return picked;
        }
    }

    /// <summary>現実の行動から資源を決める。倍率 (増幅施設・ペンギン) は <see cref="IslandGame"/> が掛ける。</summary>
    public static class EcoGrant
    {
        /// <summary>写真 1 枚の基本の資源。主の資源に多く、ほかの電気・水・ガスに少し。</summary>
        public static ResourceBundle ForPhoto(EcoPhotoVerdict verdict, IslandParameters p)
        {
            var b = new ResourceBundle();
            if (verdict == null || !verdict.Review.Counts)
            {
                return b;
            }

            b = Base(verdict, p);
            if (verdict.Review.Decision == EcoPhotoDecision.Reduce)
            {
                foreach (var kv in b.NonZero())
                {
                    b[kv.Key] = Math.Round(kv.Value * p.PhotoReducedRate, 1);
                }
            }

            return b;
        }

        private static ResourceBundle Base(EcoPhotoVerdict verdict, IslandParameters p)
        {
            var b = new ResourceBundle();

            // 写真の表にあるものは、表の量をそのまま渡す。
            var def = EcoSubjects.Find(verdict.Subject);
            if (def != null)
            {
                return def.Bundle();
            }

            if (!verdict.IsEco)
            {
                foreach (var (r, v) in EcoSubjects.UnknownGrants)
                {
                    b[r] += v;
                }

                return b;
            }

            // 表を使わない判定 (カテゴリだけ) のときは、主の資源に多く、ほかの電気・水・ガスに少し。
            var main = verdict.IsEco ? EcoCategories.MainResource(verdict.Category) : Resource.AllMighty;
            var mainAmount = verdict.IsEco ? p.PhotoMainGrant : p.PhotoSideGrant;
            b[main] += mainAmount;
            foreach (var r in new[] { Resource.Electricity, Resource.Water, Resource.Gas })
            {
                if (r != main)
                {
                    b[r] += p.PhotoSideGrant;
                }
            }

            return b;
        }

        /// <summary>歩数から得るガス。その日にすでに受け取った分を引いて、上限までにする。</summary>
        public static double GasForSteps(int stepsToday, double alreadyGranted, IslandParameters p)
        {
            var total = Math.Min(p.StepGasPerDayMax, Math.Floor(Math.Max(0, stepsToday) / (double)p.StepsPerGas));
            return Math.Max(0.0, total - alreadyGranted);
        }

        /// <summary>ミッションの振り返り 1 件。正直に「できなかった」と答えても少しもらえる。</summary>
        public static ResourceBundle ForCheckIn(CheckInAnswer answer, IslandParameters p) =>
            ResourceBundle.Of(Resource.AllMighty, answer == CheckInAnswer.Done ? p.MissionAllMighty : p.MissionTriedAllMighty);
    }

    /// <summary>決定的な乱数 (実行環境によらず同じ列)。注文と今日のミッションに使う。</summary>
    public sealed class SplitMix64
    {
        private ulong _state;

        public SplitMix64(ulong seed)
        {
            _state = seed;
        }

        public ulong Next()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                var z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>0 以上 <paramref name="max"/> 未満。</summary>
        public int NextInt(int max) => max <= 0 ? 0 : (int)(Next() % (ulong)max);
    }
}
