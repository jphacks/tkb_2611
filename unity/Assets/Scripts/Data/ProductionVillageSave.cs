using System;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Data
{
    [Serializable] public sealed class ProductionVillageSave
    {
        public const int CurrentVersion=4;
        public int Version=CurrentVersion;
        public long UtcMs;
        public string Profile="production", VillageName="ペンギンの村";
        public VillageState State;
        public bool Running=true;
        public double TimeScale=1;
        /// <summary>チュートリアルの段 (VillageTutorial)。0 = 未開始、-1 = 終わり。古い保存には無いので 0 として読む。範囲外は終わりとして扱い、保存全体は拒まない。</summary>
        public int Tutorial;
        public void Validate(string profile)
        {
            if(Version<1||Version>CurrentVersion)throw new InvalidOperationException("未対応の保存バージョンです。上書きを停止しました");
            if(Profile!=profile||UtcMs<0||UtcMs>253402300799999L||string.IsNullOrWhiteSpace(VillageName)||VillageName.Length>80||!VillageGame.Finite(TimeScale)||TimeScale<0||TimeScale>100)throw new InvalidOperationException("保存プロフィールまたは時計が不正です");
            VillageGame.Restore(State);
            if(profile=="production"&&(State.Unlimited||State.FreeResources||State.Speed!=1||State.DeliveryMs!=1500||!Running||TimeScale!=1))throw new InvalidOperationException("製品保存に開発設定が含まれています");
        }
    }
    public interface IProductionVillageStore
    {
        ProductionVillageSave Load();
        void Save(ProductionVillageSave save);
    }
}
