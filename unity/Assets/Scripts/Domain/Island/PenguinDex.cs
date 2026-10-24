using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Game;

namespace ProjectPenguin.Domain.Island
{
    /// <summary>ペンギンが島にくれるボーナスの種類。</summary>
    public enum BonusKind
    {
        /// <summary>現実から受け取る資源の倍率。</summary>
        ResourceGain,

        /// <summary>ある施設の速さの倍率。</summary>
        FacilitySpeed,

        /// <summary>港の報酬の倍率。</summary>
        OrderReward,
    }

    /// <summary>図鑑の 1 種。事実の一行は実在のペンギンの話で、アプリの氷とは関係ない (docs/game-logic.md §5)。</summary>
    public sealed class PenguinEntry
    {
        public PenguinEntry(string species, string fact, BonusKind bonus, double multiplier, Resource? resource = null,
            FacilityKind? facility = null)
        {
            Species = species;
            Fact = fact;
            Bonus = bonus;
            Multiplier = multiplier;
            Resource = resource;
            Facility = facility;
        }

        /// <summary>種のキー (17b の id。<c>"adelie"</c> など)。コードと保存データにはこれだけを使う。</summary>
        public string Species { get; }

        /// <summary>画面に出す和名 (IOC 15.2 の日本語名)。</summary>
        public string Name => PenguinCatalog.DisplayNameJa[Species];

        public string Fact { get; }
        public BonusKind Bonus { get; }
        public double Multiplier { get; }
        public Resource? Resource { get; }
        public FacilityKind? Facility { get; }

        /// <summary>画面に出すボーナスの説明。</summary>
        public string BonusText
        {
            get
            {
                var pct = (int)System.Math.Round((Multiplier - 1.0) * 100);
                return Bonus switch
                {
                    BonusKind.ResourceGain => $"{IslandCatalog.NameOf(Resource.GetValueOrDefault())}を受け取る量 +{pct}%",
                    BonusKind.FacilitySpeed => $"{IslandCatalog.Get(Facility.GetValueOrDefault()).Name}の速さ +{pct}%",
                    BonusKind.OrderReward => $"港の報酬 +{pct}%",
                    _ => "",
                };
            }
        }
    }

    /// <summary>19 種 (IOC v15.2) の図鑑とボーナス。来る順と条件は <see cref="GameSession"/> が決める。</summary>
    public static class PenguinDex
    {
        public static readonly IReadOnlyList<PenguinEntry> Entries = new[]
        {
            new PenguinEntry("adelie", "南極大陸の沿岸で暮らし、小石を集めて巣を作る。", BonusKind.FacilitySpeed, 1.1,
                facility: FacilityKind.Fishery),
            new PenguinEntry("little", "ペンギンの中でいちばん小さく、背丈は 30cm ほど。", BonusKind.FacilitySpeed, 1.1,
                facility: FacilityKind.Preparation),
            new PenguinEntry("magellanic", "南米の海岸で、地面に掘った穴を巣にする。", BonusKind.ResourceGain, 1.1,
                Resource.Water),
            new PenguinEntry("galapagos", "赤道近くのガラパゴス諸島に住む、いちばん北のペンギン。", BonusKind.FacilitySpeed, 1.15,
                facility: FacilityKind.Saltworks),
            new PenguinEntry("chinstrap", "あごの下の黒い線がひげのように見えることから名付けられた。", BonusKind.FacilitySpeed, 1.15,
                facility: FacilityKind.KelpFarm),
            new PenguinEntry("king", "2 番目に大きいペンギン。ひなは茶色い綿毛で 1 年近く育つ。", BonusKind.ResourceGain, 1.1,
                Resource.Electricity),
            new PenguinEntry("emperor", "いちばん大きいペンギン。真冬の南極で、オスが卵を足の上で温める。", BonusKind.ResourceGain, 1.2,
                Resource.Gas),
            new PenguinEntry("yellow_eyed", "ニュージーランドに住み、目のまわりの黄色い帯が目印。数が少ない。", BonusKind.OrderReward, 1.1),
            new PenguinEntry("gentoo", "泳ぐのがとても速く、時速 30km をこえることもある。", BonusKind.FacilitySpeed, 1.15,
                facility: FacilityKind.Fishery),
            new PenguinEntry("humboldt", "ペルーやチリの冷たい海流が流れる海岸に住む。", BonusKind.FacilitySpeed, 1.1,
                facility: FacilityKind.Drying),
            new PenguinEntry("macaroni", "頭のオレンジ色の飾り羽が目印。", BonusKind.FacilitySpeed, 1.1,
                facility: FacilityKind.Kamaboko),
            new PenguinEntry("royal", "マッコーリー島だけで子育てをする。顔が白っぽい。", BonusKind.FacilitySpeed, 1.1,
                facility: FacilityKind.Chikuwa),
            new PenguinEntry("african", "アフリカに住む唯一のペンギン。ロバのような声で鳴く。", BonusKind.ResourceGain, 1.1,
                Resource.Water),
            new PenguinEntry("fiordland", "ニュージーランドの雨の多い森の中で子育てをする。", BonusKind.FacilitySpeed, 1.1,
                facility: FacilityKind.Research),
            new PenguinEntry("snares", "ニュージーランド南のスネアーズ諸島だけで子育てをする。", BonusKind.OrderReward, 1.1),
            new PenguinEntry("northern_rockhopper", "岩場を両足でぴょんぴょん跳ねて移動する。", BonusKind.ResourceGain, 1.1,
                Resource.Electricity),
            new PenguinEntry("western_rockhopper", "フォークランド諸島などに住み、黄色い眉のような飾り羽がある。", BonusKind.ResourceGain, 1.1,
                Resource.Gas),
            new PenguinEntry("eastern_rockhopper", "ニュージーランドの南の島々に住むイワトビペンギンの仲間。", BonusKind.FacilitySpeed, 1.1,
                facility: FacilityKind.Oden),
            new PenguinEntry("erect_crested", "頭の飾り羽がブラシのように上向きに立つ。", BonusKind.OrderReward, 1.15),
        };

        private static readonly Dictionary<string, PenguinEntry> BySpecies = Entries.ToDictionary(e => e.Species);

        public static PenguinEntry Find(string species) => species != null && BySpecies.TryGetValue(species, out var e) ? e : null;

        /// <summary>来たペンギンのボーナスを掛け合わせた資源の倍率。</summary>
        public static double ResourceMultiplier(IEnumerable<string> arrived, Resource resource) =>
            Product(arrived, e => e.Bonus == BonusKind.ResourceGain && e.Resource == resource);

        public static double FacilityMultiplier(IEnumerable<string> arrived, FacilityKind kind) =>
            Product(arrived, e => e.Bonus == BonusKind.FacilitySpeed && e.Facility == kind);

        public static double OrderMultiplier(IEnumerable<string> arrived) => Product(arrived, e => e.Bonus == BonusKind.OrderReward);

        private static double Product(IEnumerable<string> arrived, System.Func<PenguinEntry, bool> match)
        {
            var m = 1.0;
            if (arrived == null)
            {
                return m;
            }

            foreach (var s in arrived.Distinct())
            {
                var e = Find(s);
                if (e != null && match(e))
                {
                    m *= e.Multiplier;
                }
            }

            return m;
        }
    }
}
