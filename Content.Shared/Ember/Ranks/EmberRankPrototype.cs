using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Ranks;

/// <summary>
/// A rank within an <see cref="EmberBranchPrototype"/>. Nothing here is specific to any one
/// organisation: "Petty Officer Second Class", "Senior Explorer", "Marshal" and "Assigned Unit"
/// are all the same kind of thing to this prototype.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's <c>/datum/mil_rank</c> (code/datums/mil_ranks.dm). Bay calls it
/// "mil_rank" rather than "rank" because "rank" is already overloaded there to mean a job;
/// the type is not actually limited to military ranks in Bay either.
///
/// A rank always belongs to an organisation someone serves in. Where a character comes from —
/// culture, homeworld, clan, faith — is a separate axis with no ranks in it, and belongs in a
/// cultural descriptor rather than here. Bay keeps that split too:
/// <c>/singleton/cultural_info</c> with its culture, homeworld, affiliation and religion tags
/// is an entirely different type from <c>/datum/mil_branch</c>.
/// </remarks>
[Prototype("emberRank")]
public sealed partial class EmberRankPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Fluent id of the full rank name. Bay: <c>name</c>.</summary>
    [DataField(required: true)]
    public LocId Name { get; private set; } = default!;

    /// <summary>
    /// Fluent id of the abbreviation used as a prefix to the wearer's name on IDs and the crew
    /// manifest: "PO2 Ivanov". Null for ranks that are not used that way — most civilian and
    /// appointed ones. Bay: <c>name_short</c>.
    /// </summary>
    [DataField]
    public LocId? ShortName { get; private set; }

    /// <summary>
    /// Position in this branch's ladder. Higher is senior; equal is equal; zero means the rank
    /// is not graded at all. Nothing but ordering is implied, so an organisation is free to
    /// number its ladder however it likes.
    /// </summary>
    /// <remarks>
    /// Bay: <c>sort_order</c>. The SCG branches follow Torch's convention — enlisted grades at
    /// 10..90, commissioned at 110..200, alternate ranks of a grade at +1..+4 so they sort
    /// adjacent without claiming the next grade — and <see cref="Category"/> and
    /// <see cref="Grade"/> read that convention by default. An organisation that does not
    /// follow it should set <see cref="CategoryOverride"/> rather than distort its numbers.
    /// </remarks>
    [DataField]
    public int SortOrder { get; private set; }

    /// <summary>
    /// Overrides the class that would otherwise be read off <see cref="SortOrder"/>.
    /// </summary>
    [DataField("category")]
    public EmberRankCategory? CategoryOverride { get; private set; }

    /// <summary>
    /// Insignia attached to the wearer's uniform on spawn: shoulder boards, rate badges, a
    /// warrant badge, a clan token. Bay: <c>accessory</c>, equipped by the job code.
    /// </summary>
    [DataField]
    public List<EntProtoId> Accessories { get; private set; } = new();

    /// <summary>
    /// Species allowed to hold this rank. Empty means "no whitelist" — whatever the branch
    /// admits. Bay keeps this on the map datum as <c>species_to_rank_whitelist</c>; keeping it
    /// on the rank means one place to look instead of two.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> SpeciesWhitelist { get; private set; } = new();

    /// <summary>Species barred from this rank even if the branch admits them.</summary>
    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> SpeciesBlacklist { get; private set; } = new();

    /// <summary>
    /// The rank's class. Explicit if the prototype says so, otherwise read off
    /// <see cref="SortOrder"/> using the convention described there.
    /// </summary>
    public EmberRankCategory Category => CategoryOverride ?? SortOrder switch
    {
        <= 0 => EmberRankCategory.None,
        <= 100 => EmberRankCategory.Enlisted,
        _ => EmberRankCategory.Commissioned,
    };

    /// <summary>
    /// Short grade designation for display: "E-5", "O-3", or empty when the rank is ungraded or
    /// its branch does not use the SCG numbering. Bay: <c>/datum/mil_rank/grade()</c>.
    /// </summary>
    public string Grade => (CategoryOverride is null ? Category : EmberRankCategory.None) switch
    {
        EmberRankCategory.Enlisted => $"E-{SortOrder / 10}",
        EmberRankCategory.Commissioned => $"O-{(SortOrder - 100) / 10}",
        _ => string.Empty,
    };
}
