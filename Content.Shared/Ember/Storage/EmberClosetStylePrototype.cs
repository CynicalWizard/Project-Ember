using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.Ember.Storage;

/// <summary>
/// What a closet or a crate looks like: one of a handful of shapes, a colour, and a list of markings.
/// </summary>
/// <remarks>
/// Bay draws eighty-six distinct lockers and crates out of six pictures by compositing them at round start —
/// <c>/singleton/closet_appearance</c> — rather than by drawing each one. We had fifty-nine hand-drawn sheets
/// doing the same job. The parts are the same here; only the compositing moved, from an icon built once on the
/// server to sprite layers built once on the client.
/// </remarks>
[Prototype("emberClosetStyle")]
public sealed partial class EmberClosetStylePrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<EmberClosetStylePrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    /// <summary>Which of Bay's six shapes this is drawn on.</summary>
    [DataField]
    public EmberClosetShape Shape = EmberClosetShape.Closet;

    /// <summary>
    /// Which sheet the markings come from, which is not always the one the shape came from.
    /// </summary>
    /// <remarks>
    /// Bay keeps <c>base_icon</c> and <c>decal_icon</c> as separate fields and the cabinet overrides only the
    /// first, so it is drawn as a cabinet and marked out of the closet sheet. There is no cabinet sheet at all.
    /// </remarks>
    [DataField]
    public EmberClosetShape Markings = EmberClosetShape.Closet;

    /// <summary>The colour the shape is painted, and the default colour of every marking on it.</summary>
    [DataField]
    public Color Color = DefaultColor;

    /// <summary>Whether the shape shows a lock and its light.</summary>
    [DataField]
    public bool CanLock;

    /// <summary>
    /// The markings, replaced wholesale by a child rather than added to — the same split Bay has, and for the
    /// same reason: something that wants a different set of vents cannot say so by adding to the old set.
    /// </summary>
    [DataField]
    public List<EmberClosetDecal> Decals = new()
    {
        new EmberClosetDecal { State = "upper_vent" },
        new EmberClosetDecal { State = "lower_vent" },
    };

    /// <summary>Markings laid over <see cref="Decals"/>, which is where a child usually puts its own.</summary>
    [DataField]
    public List<EmberClosetDecal> ExtraDecals = new();

    /// <summary>Bay's <c>COLOR_GRAY40</c>, which is what a closet is if nothing says otherwise.</summary>
    public static readonly Color DefaultColor = Color.FromHex("#666666");

    /// <summary>Every marking, in the order they are drawn.</summary>
    public IEnumerable<EmberClosetDecal> AllDecals()
    {
        foreach (var decal in Decals)
        {
            yield return decal;
        }

        foreach (var decal in ExtraDecals)
        {
            yield return decal;
        }
    }
}

[DataDefinition]
public sealed partial class EmberClosetDecal
{
    /// <summary>
    /// The name of the marking. The sheet holds an open and a closed version of most of them, and the drawing
    /// falls back to a single unsuffixed state for the ones that do not change.
    /// </summary>
    [DataField(required: true)]
    public string State = default!;

    /// <summary>Its colour, or the style's own if it has none — which is how Bay's null entries behave.</summary>
    [DataField]
    public Color? Color;
}

/// <summary>The shapes Bay draws a container on, each a base sheet and a sheet of markings that fit it.</summary>
public enum EmberClosetShape : byte
{
    Closet,
    Crate,
    LargeCrate,
    Cabinet,
    Cart,
    Wall,
}
