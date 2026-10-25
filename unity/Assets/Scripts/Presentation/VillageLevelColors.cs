using UnityEngine;

namespace ProjectPenguin.Presentation
{
    /// <summary>Shared by the facility tiles and the upgrade comparison.</summary>
    public static class VillageLevelColors
    {
        public static Color Tile(int level) => Mathf.Clamp(level, 1, 5) switch
        {
            2 => new Color32(104, 191, 138, 205),
            3 => new Color32(75, 169, 224, 205),
            4 => new Color32(165, 138, 220, 205),
            5 => new Color32(235, 195, 102, 230),
            _ => new Color32(144, 197, 216, 180)
        };
    }
}
