using Content.Shared.Ember.Localization;
using Content.Shared.Ember.Skills;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Roles;

/// <summary>
/// One of the names a job may be held under. SierraBay12's <c>alt_titles</c>.
/// </summary>
/// <remarks>
/// The point is menu length. An engineer aboard is an engine technician, a damage control
/// technician, an electrician or an atmospheric technician depending on what they trained in,
/// and all four do the same work with the same tools and the same access. Four entries in the
/// job list would be four times the reading for no decision; one entry with four names is the
/// same information at a quarter of the width.
///
/// So a title is a label, not a job. It cannot change access, gear or supervisors, and if a
/// variant needs any of those it is a job and belongs in the list as one.
///
/// What it <em>can</em> do is narrow. A title may refuse a species and may ask for skills the
/// job itself does not, which is the whole reason this is data rather than a display string: a
/// Unathi may be a doctor and may not be a surgeon, and that restriction has to live on the name
/// the player picks, because the name is where the difference is.
/// </remarks>
[DataDefinition]
public sealed partial class EmberJobTitle
{
    /// <summary>
    /// Unique within its job. This is what a character profile stores, so renaming it orphans
    /// every character that had chosen it — they fall back to the job's own name.
    /// </summary>
    [DataField(required: true)]
    public string Id { get; set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; set; } = default!;

    /// <summary>
    /// Forms of <see cref="Name"/> for a character of that gender, where the language has them.
    /// Left unset the name is used as written, which is the ordinary case.
    /// </summary>
    /// <remarks>
    /// Not a choice the player makes — the form follows from the character, so both of these
    /// render the same single entry in the picker. See <see cref="EmberGenderedName"/> for why
    /// this is three flat fields rather than something tidier.
    /// </remarks>
    [DataField]
    public LocId? NameMale { get; set; }

    [DataField]
    public LocId? NameFemale { get; set; }

    /// <summary>
    /// Shown in place of the job's description when this title is selected. Optional, and worth
    /// setting only where the variant actually does something different.
    /// </summary>
    [DataField]
    public LocId? Description { get; set; }

    /// <summary>
    /// Years of schooling the name stands for, over and above whatever the job asks.
    /// </summary>
    /// <remarks>
    /// A commission encodes an education — the Corps does not make an officer of someone without
    /// a degree — but a contract encodes nothing at all: a company rank is a job title, not a
    /// career. So any post open to contractors has to state its own floor, and any *name* that
    /// means more schooling than its post has to state that too. A surgeon is a physician plus
    /// several more years, and this is where those years are written down.
    ///
    /// No maximum. A rank brackets its holders because a service expects movement; a
    /// qualification does not expire.
    /// </remarks>
    [DataField]
    public int MinAge { get; set; }

    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> SpeciesWhitelist { get; set; } = new();

    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> SpeciesBlacklist { get; set; } = new();

    /// <summary>
    /// Extra floors on top of the job's own, for a variant that is genuinely harder. Merged the
    /// same way a branch's floors are: the higher of the two wins.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, SkillLevel> MinSkills { get; set; } = new();
}
