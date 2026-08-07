namespace Content.Shared.Ember.Materials;

/// <summary>
/// Bay's <c>shard_type</c>: what a material breaks into, which decides both what the debris is called and which
/// set of sprites it draws from.
/// </summary>
public enum EmberShardType : byte
{
    /// <summary>Leaves nothing behind, only sheets. Holographic materials and the like.</summary>
    None = 0,

    /// <summary>Glass, phoron, diamond.</summary>
    Shard,

    /// <summary>The default for metals.</summary>
    Shrapnel,

    /// <summary>Sandstone, marble, cult stone.</summary>
    Piece,

    /// <summary>Wood.</summary>
    Splinters,
}

public static class EmberShardTypes
{
    /// <summary>
    /// Bay builds the icon state as the shard type plus a size, which is why the sheet carries three of each.
    /// </summary>
    public static readonly string[] Sizes = { "large", "medium", "small" };

    /// <summary>
    /// The sprite prefix, matching Bay's <c>shard_icon</c>, which defaults to the shard type's own name.
    /// </summary>
    public static string? GetIconBase(EmberShardType type)
    {
        return type switch
        {
            EmberShardType.Shard => "shard",
            EmberShardType.Shrapnel => "shrapnel",
            EmberShardType.Piece => "piece",
            EmberShardType.Splinters => "splinters",
            _ => null,
        };
    }

    /// <summary>
    /// The Fluent id naming the debris, so "steel shrapnel" and "sandstone piece" read correctly in both
    /// languages rather than being pasted together in code.
    /// </summary>
    public static string? GetNameId(EmberShardType type)
    {
        return GetIconBase(type) is { } iconBase ? $"ember-shard-name-{iconBase}" : null;
    }

    public static string? GetDescriptionId(EmberShardType type)
    {
        return GetIconBase(type) is { } iconBase ? $"ember-shard-desc-{iconBase}" : null;
    }
}
