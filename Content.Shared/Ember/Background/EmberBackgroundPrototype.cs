using Content.Shared.Customization.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Background;

/// <summary>
/// One answer on one axis of a character's background: where they live, how they were raised, who
/// they belong to, what they believe.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's <c>/singleton/cultural_info</c>
/// (code/modules/culture_descriptor/). Bay splits the same base type into four subtypes and tags
/// each with a <c>category</c>; one prototype kind with a category field says the same thing and
/// lets the editor build its four dropdowns from one enumeration rather than four.
///
/// This replaces the single `nationality` dropdown inherited from Einstein Engines' Contractors
/// module. That asked one question - which country's passport do you hold - and answered several
/// at once by implication. Bay asks the four separately because they genuinely come apart: a
/// Tajaran raised on Mars in a human household, holding SCG residency and following the Faith of
/// the Weeping Sun, is four answers that no single list can hold.
///
/// Economic power is deliberately not carried across. Bay uses it to scale loadout budgets, and we
/// have no economy hanging off a character's homeworld; porting the number without the thing it
/// feeds would be data that lies about being used.
/// </remarks>
[Prototype("emberBackground")]
public sealed partial class EmberBackgroundPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Which of the four questions this answers. Bay: the <c>category</c> var, set per subtype.
    /// </summary>
    [DataField(required: true)]
    public EmberBackgroundAxis Axis { get; private set; }

    /// <summary>Fluent id of the displayed name.</summary>
    [DataField(required: true)]
    public LocId Name { get; private set; } = default!;

    /// <summary>Fluent id of the long description shown beside the list.</summary>
    [DataField(required: true)]
    public LocId Description { get; private set; } = default!;

    /// <summary>
    /// Fluent id of a short metadata line shown above the description, or null where there is
    /// nothing to say. Capital, distance from Sol, who governs it.
    /// </summary>
    /// <remarks>
    /// Bay keeps these as three separate vars and formats them in <c>get_text_details()</c>. One
    /// pre-written line instead, because all three are display-only and the alternative is three
    /// Fluent keys per entry across two languages for text that is always read as one sentence.
    /// </remarks>
    [DataField]
    public LocId? Details { get; private set; }

    /// <summary>
    /// Who may pick this. Ordinary <see cref="CharacterRequirement"/>s, so species, age, branch and
    /// rank all work here with nothing new written.
    /// </summary>
    /// <remarks>
    /// Bay gates these by hand - a species' culture list is built from its own datum, and the
    /// hidden ones are filtered at the UI. Requirements say the same thing in the form the rest of
    /// this codebase already understands, and the lobby renders the reason a locked row is locked.
    /// </remarks>
    [DataField]
    public List<CharacterRequirement> Requirements = new();

    /// <summary>
    /// Not offered in character creation. Bay: <c>hidden</c>, which covers the entries that exist
    /// so an antagonist or an event character has something true to point at.
    /// </summary>
    [DataField]
    public bool Hidden { get; private set; }

    /// <summary>
    /// The passport this background issues, where it issues one. Meaningful on
    /// <see cref="EmberBackgroundAxis.Faction"/> and ignored everywhere else.
    /// </summary>
    /// <remarks>
    /// Bay hangs the passport off the location axis, because Bay's location axis is its citizenship
    /// axis in practice. Ours are separate questions, and a passport is issued by a state rather
    /// than by a birthplace - so it hangs here, and the homeworld is what gets printed inside it as
    /// the place of birth.
    ///
    /// Null means this allegiance issues no passport at all. That is not an omission: it is what
    /// "stateless" means, and the absence of the document is the whole point of the entry.
    /// </remarks>
    [DataField]
    public EntProtoId? Passport { get; private set; }

    /// <summary>
    /// Sort weight within the axis. Higher first, then alphabetically by displayed name.
    /// </summary>
    /// <remarks>
    /// Bay has no equivalent and its lists are in file order, which puts Mars first because Mars
    /// happens to be the base type. The four lists here run to dozens of entries each and the ones
    /// most characters will pick should not be halfway down.
    /// </remarks>
    [DataField]
    public int Weight { get; private set; }
}

/// <summary>
/// The four questions a background answers. Bay: TAG_HOMEWORLD, TAG_CULTURE, TAG_FACTION,
/// TAG_RELIGION in code/__defines/culture.dm.
/// </summary>
public enum EmberBackgroundAxis : byte
{
    /// <summary>Where the character lives, and what passport that gets them.</summary>
    Homeworld,

    /// <summary>How they were raised. Bay's culture, which also decides how names are generated.</summary>
    Culture,

    /// <summary>Who they belong to: a government, a company, a service.</summary>
    Faction,

    /// <summary>What they believe, if anything.</summary>
    Religion,
}
