using System;
using UnityEngine;

namespace ProjectPenguin.Data
{
    /// <summary>Per-session detached saves. Never opens or writes a file.</summary>
    public sealed class ProductionVillageMemoryStore : IProductionVillageStore
    {
        private readonly string _profile;
        private string _json;
        public ProductionVillageMemoryStore(bool development=false){_profile=development?"development":"production";}
        public ProductionVillageSave Load()
        {
            if(_json==null)return null;
            var copy=JsonUtility.FromJson<ProductionVillageSave>(_json);copy.Validate(_profile);return copy;
        }
        public void Save(ProductionVillageSave save)
        {
            if(save==null)throw new ArgumentNullException(nameof(save));
            save.Validate(_profile);
            var json=JsonUtility.ToJson(save);
            // Validate the detached representation as well, before replacing the previous save.
            JsonUtility.FromJson<ProductionVillageSave>(json).Validate(_profile);_json=json;
        }
    }
}
