using UnityEngine;

/// <summary>Where this troop/power is allowed to be placed.</summary>
public enum PlacementType
{
    LandOnly,       // Centipede, Beetle, Praying Mantis
    WaterOnly,      // Lily Pad and future water-only units
    LandAndWater,   // Frog
}

/// <summary>Which sidebar section this item belongs to.</summary>
public enum TroopCategory
{
    Troop,  // combat unit — tracked in TroopManager.PlacedTroops
    Power,  // terrain / utility — tracked in TroopManager.PlacedPowers
}

/// <summary>
/// Create one TroopData asset per troop/power type via:
/// Right-click in Project → Create → Click n Claw → Troop Data
/// </summary>
[CreateAssetMenu(fileName = "NewTroopData", menuName = "Click n Claw/Troop Data")]
public class TroopData : ScriptableObject
{
    [Header("Identity")]
    public string troopName;
    public Sprite portrait;
    public GameObject prefab;

    [Header("Category")]
    public TroopCategory category = TroopCategory.Troop;

    [Header("Placement")]
    public PlacementType placementType = PlacementType.LandOnly;
    [Tooltip("If true, land troops can be placed on top of this power (e.g. Lily Pad)")]
    public bool isLandPlatform = false;

    [Header("Economy")]
    public int baseCost = 50;

    [Header("Upgrades — add one entry per upgrade tier")]
    public UpgradeTier[] upgrades;

    [System.Serializable]
    public struct UpgradeTier
    {
        public string description;
        public int cost;
    }
}
