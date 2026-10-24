using System;

namespace ProjectPenguin.Domain.Raid
{
    public sealed class RaidLaunch
    {
        public bool Requested { get; private set; }
        public bool Valid { get; private set; }
        public string EventId { get; private set; }
        public bool Venue { get; private set; }
        public bool Demo { get; private set; }
        public bool Classroom { get; private set; }
        public static bool ValidEventId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            if (!((id[0]>='a' && id[0]<='z') || (id[0]>='0' && id[0]<='9'))) return false;
            foreach (var c in id)
                if (!(c >= 'a' && c <= 'z') &&
                    !(c >= '0' && c <= '9') && c != '-' && c != '_') return false;
            return true;
        }
        public static bool ValidGeneratedId(string id)
        {
            if(id==null || id.Length!=32) return false;
            foreach(var c in id) if(!(c>='0' && c<='9') && !(c>='a' && c<='f')) return false;
            return true;
        }
        public static RaidLaunch Parse(string url)
        {
            var result = new RaidLaunch();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return result;
            var raidCount = 0; var classroomCount=0; var venueCount = 0; var demoCount = 0; var invalid = false;
            try
            {
                foreach (var part in uri.Query.TrimStart('?').Split('&'))
                {
                    var pair = part.Split(new[] { '=' }, 2);
                    var key = Uri.UnescapeDataString(pair[0]);
                    var value = pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : "";
                    if (key == "raid") { raidCount++; result.EventId = value; }
                    if (key == "classroom") { classroomCount++; result.EventId=value; result.Classroom=true; }
                    if (key == "venue") { venueCount++; result.Venue = value == "1"; invalid |= value != "1" && value != "0"; }
                    if (key == "raidDemo") { demoCount++; result.Demo = value == "1"; invalid |= value != "1" && value != "0"; }
                }
            }
            catch (UriFormatException) { invalid = true; }
            result.Requested = raidCount+classroomCount > 0;
            result.Valid = raidCount+classroomCount == 1 && venueCount <= 1 && demoCount <= 1 &&
                !(classroomCount>0 && demoCount>0) && !invalid && ValidEventId(result.EventId);
            return result;
        }
    }
}
