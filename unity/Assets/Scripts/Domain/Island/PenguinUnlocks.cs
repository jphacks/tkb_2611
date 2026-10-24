using System;
using System.Collections.Generic;
using System.Linq;
using ProjectPenguin.Domain.Game;

namespace ProjectPenguin.Domain.Island
{
    /// <summary>種の解放条件をどこが確かめるか。</summary>
    public enum UnlockSource
    {
        /// <summary>記録の節目。<see cref="GameSession"/> が来訪として決める (週 1 種まで、docs/game-logic.md §5)。</summary>
        Session,

        /// <summary>島の実績。<see cref="IslandGame"/> が満たした時点で来させる。</summary>
        Island,
    }

    /// <summary>
    /// 1 種の解放条件 (実績)。種はアチーブメントで、働き手の数とは別に数える
    /// (普通のペンギンが 100 羽いて、珍しい種が 1 羽でもよい。2026-10-17 のオーナーの決定)。
    /// </summary>
    public sealed class PenguinUnlock
    {
        private readonly Func<GameSession, IslandGame, double> _progress;

        public PenguinUnlock(string species, UnlockSource source, string condition, double goal,
            Func<GameSession, IslandGame, double> progress)
        {
            Species = species;
            Source = source;
            Condition = condition;
            Goal = goal;
            _progress = progress;
        }

        public string Species { get; }
        public UnlockSource Source { get; }

        /// <summary>図鑑に出す条件 (数字は <see cref="Goal"/> と同じ)。</summary>
        public string Condition { get; }

        public double Goal { get; }

        /// <summary>今の進み具合 (0〜<see cref="Goal"/>)。島の条件は session が null でも測れる。</summary>
        public double Progress(GameSession session, IslandGame island)
        {
            return Math.Max(0.0, Math.Min(Goal, _progress(session, island)));
        }

        public bool IsMet(GameSession session, IslandGame island) => Progress(session, island) >= Goal - 1e-9;
    }

    /// <summary>
    /// 19 種の解放条件。節目の 7 種は <see cref="GameSession"/> の条件をそのまま図鑑に出し、
    /// 残りの 12 種 (週の順の 11 種とマゼラン) は島の実績で解放する。マゼランは「記録の手段を 3 つ使う」が
    /// 写真だけの今は満たしにくいので、島の「写真を 3 つの分類で撮る」で解放する (節目で来ても同じ種)。
    /// </summary>
    public static class PenguinUnlocks
    {
        public static readonly IReadOnlyList<PenguinUnlock> All = new[]
        {
            Session(PenguinCatalog.Onboarding, "島にはじめて来る", 1, s => 1),
            Session(PenguinCatalog.FirstRecord, "はじめて記録する", 1, s => s.TotalRecordDays),
            Session(PenguinCatalog.FirstComeback, "休んで氷が溶けたあと、戻って記録する", 1, s => s.Comebacks),
            Session(PenguinCatalog.FiveMissions, "ミッションを 5 種類やりとげる", 5, s => s.DoneMissionCount),
            Session(PenguinCatalog.Total14, "記録した日が 14 日になる", 14, s => s.TotalRecordDays),
            Session(PenguinCatalog.Total30, "記録した日が 30 日になる", 30, s => s.TotalRecordDays),
            Session(PenguinCatalog.FourWeeks, "週 3 日以上の記録を 4 週続ける", 4, s => s.ConsecutiveWeeks),
            Island(PenguinCatalog.AllKinds, "エコ写真を 3 つの分類で撮る", 3, g => g.PhotoCategoryCount),
            Island("gentoo", "港に 10 回届ける", 10, g => g.Delivered),
            Island("african", "お魚を 1,000 匹まで増やす", 1000, g => g.MaxFishHeld),
            Island("northern_rockhopper", "島を 10 マス広げる", 10, g => g.Board.Expansions.Count),
            Island("western_rockhopper", "施設を 6 種類建てる", 6, g => g.Facilities.Select(f => f.Kind).Distinct().Count()),
            Island("snares", "港を 3 段目まで育てる", 3, g => g.HarborStage),
            Island("macaroni", "かまぼこを 10 個作る", 10, g => g.MadeOf(Item.Kamaboko)),
            Island("royal", "ちくわを 10 個作る", 10, g => g.MadeOf(Item.Chikuwa)),
            Island("humboldt", "干物を 10 個作る", 10, g => g.MadeOf(Item.DriedFish)),
            Island("fiordland", "真珠を作る", 1, g => g.PearlsMade),
            Island("eastern_rockhopper", "おでんを作る", 1, g => g.MadeOf(Item.Oden)),
            Island("erect_crested", "氷室を Lv5 にする", 5,
                g => g.Facilities.Where(f => f.Kind == FacilityKind.IceHouse).Select(f => f.Level).DefaultIfEmpty(0).Max()),
        };

        private static readonly Dictionary<string, PenguinUnlock> BySpecies = All.ToDictionary(u => u.Species);

        public static PenguinUnlock Find(string species) =>
            species != null && BySpecies.TryGetValue(species, out var u) ? u : null;

        private static PenguinUnlock Session(string species, string condition, double goal, Func<GameSession, double> progress) =>
            new PenguinUnlock(species, UnlockSource.Session, condition, goal, (s, g) => s == null ? 0 : progress(s));

        private static PenguinUnlock Island(string species, string condition, double goal, Func<IslandGame, double> progress) =>
            new PenguinUnlock(species, UnlockSource.Island, condition, goal, (s, g) => g == null ? 0 : progress(g));
    }
}
