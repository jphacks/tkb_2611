using System;
using ProjectPenguin.Domain.Village;

namespace ProjectPenguin.Data
{
    [Serializable] public sealed class ProductionVillageSave
    {
        public const int CurrentVersion=2;
        public int Version=CurrentVersion;
        public long UtcMs;
        public string Profile="production", VillageName="ペンギンの村";
        public VillageState State;
        public bool Running=true;
        public double TimeScale=1;
        public void Validate(string profile)
        {
            if(Version<1||Version>CurrentVersion)throw new InvalidOperationException("未対応の保存バージョンです。上書きを停止しました");
            if(Profile!=profile||UtcMs<0||UtcMs>253402300799999L||string.IsNullOrWhiteSpace(VillageName)||VillageName.Length>80||!VillageGame.Finite(TimeScale)||TimeScale<0||TimeScale>100)throw new InvalidOperationException("保存プロフィールまたは時計が不正です");
            VillageGame.Restore(State);
            if(profile=="production"&&(State.Unlimited||State.Speed!=1||State.DeliveryMs!=1500||!Running||TimeScale!=1))throw new InvalidOperationException("製品保存に開発設定が含まれています");
        }
    }
    public interface IProductionVillageStore
    {
        ProductionVillageSave Load();
        void Save(ProductionVillageSave save);
    }
}
