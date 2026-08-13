using Content.Shared.Ember.Skills;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Ranks;

/// <summary>
/// An organisation a character serves in, together with the ladder of ranks inside it. A
/// character picks a branch and then a rank within it, and their job restricts which of those
/// combinations are legal.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's <c>/datum/mil_branch</c> (code/datums/mil_ranks.dm). Nothing here
/// assumes the SCG: a fleet, an army, a police agency, a foreign navy, a mercenary company or a
/// plain "civilian" placeholder are all branches. The SCG ones simply happen to be first,
/// taken from maps/torch/torch_ranks.dm — the SEV Torch rather than the Sierra, since the
/// Sierra is a corporate ship whose "ranks" are employment statuses.
///
/// Three axes are deliberately kept apart, and a branch is only the first:
/// <list type="bullet">
/// <item>who you serve in — this prototype, and the only one of the three that has ranks</item>
/// <item>who pays you — the Einstein Engines <c>EmployerPrototype</c>; a contractor serves in
/// a civilian branch while being paid by a corporation</item>
/// <item>where you are from — culture, homeworld, clan, faith. Bay's
/// <c>/singleton/cultural_info</c>. Never has ranks</item>
/// </list>
/// </remarks>
[Prototype("emberBranch")]
public sealed partial class EmberBranchPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    /// <summary>Fluent id of the full branch name. Bay: <c>name</c>.</summary>
    [DataField(required: true)]
    public LocId Name { get; set; } = default!;

    /// <summary>Fluent id of the abbreviation: SCGEC, SCGF. Bay: <c>name_short</c>.</summary>
    [DataField(required: true)]
    public LocId ShortName { get; set; } = default!;

    /// <summary>
    /// Every rank this branch has, including ones no player can pick.
    /// Bay: <c>rank_types</c>.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<EmberRankPrototype>> Ranks { get; set; } = new();

    /// <summary>
    /// The subset of <see cref="Ranks"/> a player may choose at character creation. Admiral
    /// exists so that admins and events can use it, not so that someone spawns as one.
    /// Bay: <c>spawn_rank_types</c>.
    /// </summary>
    [DataField]
    public List<ProtoId<EmberRankPrototype>> SpawnRanks { get; set; } = new();

    /// <summary>
    /// Skills everyone in the branch has regardless of job. This is what answers "why does any
    /// crewman know how to work a voidsuit" without repeating it in every job's minimums.
    /// Bay: <c>min_skill</c>.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<SkillPrototype>, SkillLevel> MinSkills { get; set; } = new();

    /// <summary>
    /// Fallback job for members of this branch who cannot get the one they wanted.
    /// Bay: <c>assistant_job</c>.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? AssistantJob { get; set; }

    /// <summary>Domain for the character's email address. Flavour. Bay: <c>email_domain</c>.</summary>
    [DataField]
    public string EmailDomain { get; set; } = "freemail.net";

    /// <summary>
    /// Whether members may set their own email address. True for civilians, false for anyone
    /// issued an address by an organisation. Bay: <c>allow_custom_email</c>.
    /// </summary>
    [DataField]
    public bool AllowCustomEmail { get; set; }

    /// <summary>
    /// Species allowed into this branch. Empty means no whitelist. Bay keeps this on the map
    /// datum as <c>species_to_branch_whitelist</c>.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> SpeciesWhitelist { get; set; } = new();

    /// <summary>Species barred from this branch. Bay: <c>species_to_branch_blacklist</c>.</summary>
    [DataField]
    public HashSet<ProtoId<SpeciesPrototype>> SpeciesBlacklist { get; set; } = new();

    /// <summary>
    /// Whether members of this branch may also be employed by an outside company.
    /// </summary>
    /// <remarks>
    /// False for anything a state posts people to, and that is the whole point: serving in an
    /// organisation and being on a company's payroll are alternatives, not layers. A government
    /// does not hire its own people through a firm — it assigns them.
    ///
    /// The distinction is easy to lose because the SCG's exploration effort has both. The
    /// Expeditionary Corps is a government service; the Expeditionary Corps Organisation is a
    /// state-owned limited company set up in 2302 as a joint platform for corporations and
    /// government bodies. Someone on that company's payroll is a contractor in the civilian
    /// branch, not a member of the Corps, however similar the badge on their arm looks.
    /// </remarks>
    [DataField]
    public bool AllowsEmployer { get; set; }

    /// <summary>
    /// Whether this branch is an armed force rather than a civil or civilian one.
    /// </summary>
    /// <remarks>
    /// Organisations differ on who they will arm, so this flag states a fact about the branch
    /// and leaves the policy to whatever reads it. For the SCG the policy is that only citizens
    /// serve, which is why no xeno species reaches an SCG military branch — but that is the
    /// SCG's rule, not a property of this field.
    /// </remarks>
    [DataField]
    public bool Military { get; set; }
}
