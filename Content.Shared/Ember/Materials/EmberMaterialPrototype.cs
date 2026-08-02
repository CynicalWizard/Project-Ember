using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Materials;

[Prototype("emberMaterial")]
public sealed partial class EmberMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField(required: true)]
    public string Key { get; set; } = default!;

    [DataField]
    public string DisplayName { get; set; } = string.Empty;

    [DataField]
    public string Category { get; set; } = "other";

    [DataField]
    public Color Color { get; set; } = Color.White;

    [DataField]
    public float Opacity { get; set; } = 1f;

    [DataField]
    public EntProtoId? StackEntity { get; set; }

    [DataField]
    public string? StackType { get; set; }

    [DataField]
    public string SheetSingularName { get; set; } = "sheet";

    [DataField]
    public string SheetPluralName { get; set; } = "sheets";

    [DataField]
    public string SheetIconBase { get; set; } = "sheet";

    [DataField]
    public string SheetIconReinforced { get; set; } = "reinf-overlay";

    [DataField]
    public bool SheetHasPluralIcon { get; set; } = true;

    [DataField]
    public string WallName { get; set; } = "wall";

    [DataField]
    public string WallIconBase { get; set; } = "metal";

    [DataField]
    public string WallIconReinforced { get; set; } = "reinf_metal";

    [DataField]
    public bool WallPaintableMain { get; set; } = true;

    [DataField]
    public bool WallPaintableStripe { get; set; }

    [DataField]
    public bool WallPaintableDetail { get; set; }

    [DataField]
    public bool WallPaintableWindow { get; set; }

    [DataField]
    public bool WallHasEdges { get; set; }

    [DataField]
    public Dictionary<string, bool> WallBlendIcons { get; set; } = new();

    [DataField]
    public string DoorIconBase { get; set; } = "metal";

    [DataField]
    public string TableIconBase { get; set; } = "metal";

    [DataField]
    public string TableIconReinforced { get; set; } = "reinf_metal";

    [DataField]
    public int Integrity { get; set; } = 150;

    [DataField]
    public int BruteArmor { get; set; } = 2;

    [DataField]
    public int BurnArmor { get; set; } = 2;

    [DataField]
    public int Hardness { get; set; } = 60;

    [DataField]
    public int Weight { get; set; } = 20;

    [DataField]
    public int MeltingPoint { get; set; } = 1800;

    [DataField]
    public float ExplosionResistance { get; set; } = 5f;

    [DataField]
    public bool Conductive { get; set; } = true;

    [DataField]
    public float? Radioactivity { get; set; }

    [DataField]
    public float? IgnitionPoint { get; set; }

    [DataField]
    public float? Luminescence { get; set; }

    /// <summary>What this material leaves behind when something made of it is smashed.</summary>
    [DataField]
    public EmberShardType ShardType { get; set; } = EmberShardType.Shrapnel;

    /// <summary>
    /// Whether a welder turns the debris back into a sheet. Bay says no to splinters, since you cannot weld
    /// wood back together.
    /// </summary>
    [DataField]
    public bool ShardCanRepair { get; set; } = true;

    [DataField]
    public int ConstructionDifficulty { get; set; }

    [DataField]
    public int UnitsPerSheet { get; set; } = 2000;

    [DataField]
    public bool Brittle { get; set; }

    [DataField]
    public bool Padding { get; set; }

    [DataField]
    public bool Unmeltable { get; set; }

    [DataField]
    public string? OreName { get; set; }

    [DataField]
    public string? OreDescription { get; set; }

    [DataField]
    public string? OreSmeltsTo { get; set; }

    [DataField]
    public string? OreCompressesTo { get; set; }

    [DataField]
    public int OreResultAmount { get; set; } = 1;

    [DataField]
    public int? OreSpreadChance { get; set; }

    [DataField]
    public string? OreScanIcon { get; set; }

    [DataField]
    public string? OreIconOverlay { get; set; }

    [DataField]
    public Dictionary<string, int> AlloyMaterials { get; set; } = new();

    [DataField]
    public bool AlloyProduct { get; set; }

    [DataField]
    public int? SalePrice { get; set; }

    [DataField]
    public int Value { get; set; } = 1;

    [DataField]
    public bool HiddenFromCodex { get; set; }
}
