using System;
using System.Collections.Generic;

namespace ProjectPenguin.Domain.Village
{
    [Serializable] public sealed class HealthRecordEntry
    {
        public string Id, Kind;
        public int Day;
        public double Amount;
    }
    [Serializable] public sealed class HealthRecordLedger
    {
        public int LastStepDay = -1, MaxSteps;
        public List<HealthRecordEntry> Entries = new List<HealthRecordEntry>();
        public HealthRecordLedger Copy()
        {
            var result = new HealthRecordLedger { LastStepDay = LastStepDay, MaxSteps = MaxSteps };
            foreach (var e in Entries) result.Entries.Add(new HealthRecordEntry { Id=e.Id, Kind=e.Kind, Day=e.Day, Amount=e.Amount });
            return result;
        }
    }
}
