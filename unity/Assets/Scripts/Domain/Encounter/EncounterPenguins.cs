using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProjectPenguin.Domain.Encounter
{
    /// <summary>すれ違いで獲得するペンギンの種類と、試作で効かせる資源。</summary>
    public enum EncounterResource
    {
        /// <summary>試作に対象の資源が無い (魚・海藻・塩)。所持だけを記録する。</summary>
        None,
        Gas,
        Electricity,
        Water,
    }

    public sealed class EncounterSpecies
    {
        internal EncounterSpecies(string id, string name, EncounterResource resource, int weight)
        {
            Id = id;
            Name = name;
            Resource = resource;
            Weight = weight;
        }

        /// <summary>既存の図鑑の種 ID。</summary>
        public string Id { get; }

        public string Name { get; }

        /// <summary>1 羽あたり +10% (加算) を効かせる資源。</summary>
        public EncounterResource Resource { get; }

        /// <summary>通常の抽選の重み (全解放後の出現率の % × 100)。</summary>
        public int Weight { get; }
    }

    /// <summary>
    /// docs/product-direction/penguin-effects-proposal.md の「S1：R1の段階」で開く基本 6 種と、その重み。
    /// 抽選の種は pair_id にするので、すれ違った両方の端末で同じ種類になる (相手の種類をコピーはしない)。
    /// 新種保証・段階による解放は未実装 (試作では S1 の 6 種が最初から開いている)。
    /// </summary>
    public static class EncounterPenguins
    {
        public static readonly IReadOnlyList<EncounterSpecies> S1 = Array.AsReadOnly(new[]
        {
            new EncounterSpecies("adelie", "アデリー", EncounterResource.None, 1400),
            new EncounterSpecies("chinstrap", "ヒゲ", EncounterResource.None, 1000),
            new EncounterSpecies("galapagos", "ガラパゴス", EncounterResource.None, 800),
            new EncounterSpecies("western_rockhopper", "ミナミイワトビ", EncounterResource.Gas, 1000),
            new EncounterSpecies("king", "オウサマ", EncounterResource.Electricity, 800),
            new EncounterSpecies("magellanic", "マゼラン", EncounterResource.Water, 1000),
        });

        /// <summary>1 羽あたりの加算 (R0 の資源の +10%) を 10 分の 1 単位で表したもの。</summary>
        public const int BonusTenthsPerBird = 1;

        /// <summary>pair_id の先頭 8 桁 (32 ビット) を重みの合計で割った余りで選ぶ。</summary>
        public static EncounterSpecies Draw(string pairId, IReadOnlyList<EncounterSpecies> species = null)
        {
            species ??= S1;
            if (pairId == null || pairId.Length < 8 ||
                !uint.TryParse(pairId.Substring(0, 8), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var seed))
            {
                throw new ArgumentException("pair_id の形が違う", nameof(pairId));
            }

            var total = 0;
            foreach (var s in species)
            {
                total += s.Weight;
            }

            var point = (int)(seed % (uint)total);
            foreach (var s in species)
            {
                if (point < s.Weight)
                {
                    return s;
                }

                point -= s.Weight;
            }

            throw new InvalidOperationException("重みの合計を超えた");
        }

        public static EncounterSpecies Find(string id)
        {
            foreach (var s in S1)
            {
                if (s.Id == id)
                {
                    return s;
                }
            }

            return null;
        }
    }
}
