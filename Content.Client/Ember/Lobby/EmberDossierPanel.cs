using Content.Client.UserInterface.Controls;
using Content.Shared.Ember.Background;
using Content.Shared.Ember.Ranks;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared._EE.Contractors.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Ember.Lobby;

/// <summary>
/// The character as currently defined, printed as a personnel record and kept in view while the
/// rest of the editor changes underneath it.
/// </summary>
/// <remarks>
/// This exists because the editor answers "what are this character's markings" perfectly well and
/// "who is this character" not at all: every fact about them lives on a different tab, so reading
/// the whole person means visiting six of them and remembering. The panel is deliberately
/// read-only - it is the summary, and a summary you can edit is just another form.
///
/// It is also where a decision gets enforced rather than described. Which posting a character
/// holds and who pays them are alternatives, not layers, and printing "служба" and "работодатель"
/// in the same record with only one of them filled says so every time the player looks at it.
/// </remarks>
public sealed class EmberDossierPanel : BoxContainer
{
    private readonly IPrototypeManager _prototypes;

    private readonly BoxContainer _rows;
    private readonly RichTextLabel _status;

    public EmberDossierPanel()
    {
        _prototypes = IoCManager.Resolve<IPrototypeManager>();

        Orientation = LayoutOrientation.Vertical;
        VerticalExpand = true;
        MinWidth = 240;

        AddChild(new Label
        {
            Text = Loc.GetString("ember-dossier-heading"),
            StyleClasses = { "LabelHeading" },
            Margin = new Thickness(8, 8, 8, 6),
        });

        AddChild(_rows = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8, 0, 8, 0),
        });

        AddChild(_status = new RichTextLabel
        {
            Margin = new Thickness(8, 10, 8, 8),
        });

        // The filler goes last so the record stays gathered at the top. Putting it between the
        // rows and the status pushed the readiness line to the bottom of a tall column, which
        // read as two unrelated panels rather than one record.
        AddChild(new Control { VerticalExpand = true });
    }

    /// <summary>
    /// Redraws the record. Safe to call with no profile - the panel then says the slot is empty
    /// rather than disappearing, because a gap where the summary was reads as a bug.
    /// </summary>
    public void Update(HumanoidCharacterProfile? profile)
    {
        _rows.RemoveAllChildren();

        if (profile == null)
        {
            // Markup, not plain text: the string overload of SetMessage calls AddText and would
            // print the colour tags verbatim.
            _status.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("ember-dossier-empty")));
            return;
        }

        // The name gets a line of its own. Every other value is short enough to sit right-aligned
        // beside its label, but a right-aligned label clips from the left when it overflows, and
        // for a name that hides the half people recognise: "Фогельзенгер-Фалькенхорст" became
        // "ельзенгер-Фалькенхорс". Wrapping it across the panel keeps the beginning.
        var name = new RichTextLabel { Margin = new Thickness(0, 0, 0, 6) };

        // SetMessage(string) calls AddText, which is what a player-entered name wants: markup in
        // it stays literal instead of being parsed.
        name.SetMessage(profile.Name);
        _rows.AddChild(name);

        AddRow("ember-dossier-species", DescribeSpecies(profile));
        AddRow("ember-dossier-homeworld", BackgroundName(profile.Homeworld));
        AddRow("ember-dossier-culture", BackgroundName(profile.Culture));
        AddRow("ember-dossier-faction", BackgroundName(profile.Faction));
        AddRow("ember-dossier-religion", BackgroundName(profile.Religion));

        // Service and employer are alternatives. Printing the one that applies, in the place the
        // other would have gone, is the whole point - see the remarks on the class.
        var branch = profile.Branch is { } branchId
            && _prototypes.TryIndex(branchId, out EmberBranchPrototype? branchProto)
                ? branchProto
                : null;

        // Asked of the same helper the editor asks, not of the prototype directly. Holding no
        // branch at all is the case of someone who works for a living, so it allows an employer -
        // and a pattern match on the prototype says the opposite, because there is no prototype.
        // The record then printed "service: not set" beside a form offering an employer field,
        // which is the exact disagreement this panel exists to prevent.
        if (SharedEmberRanksSystem.AllowsEmployer(branch))
            AddRow("ember-dossier-employer", EmployerName(profile));
        else
            AddRow("ember-dossier-branch", Loc.GetString(branch!.ShortName));

        AddRow("ember-dossier-rank", RankName(profile));
        AddRow("ember-dossier-post", PostName(profile));

        _status.SetMessage(Readiness(profile, branch));
    }

    /// <summary>
    /// One entry of the record: a small caption with the value underneath it.
    /// </summary>
    /// <remarks>
    /// Caption above rather than label-and-value on one line, because the one-line form has to
    /// decide what to do when the value is too wide and every answer is bad. Right-aligned and
    /// clipped drops the front of the string - "Другая принадлежность" arrived as
    /// "принадлежность" - and left-aligned and clipped drops the end, which for a name is just as
    /// useless. Given its own line the value wraps instead, and a record that is read rather than
    /// scanned loses nothing by being taller.
    /// </remarks>
    private void AddRow(string labelId, string value)
    {
        _rows.AddChild(new Label
        {
            Text = Loc.GetString(labelId),
            StyleClasses = { "LabelSubText" },
        });

        var text = new RichTextLabel { Margin = new Thickness(8, 0, 0, 6) };
        text.SetMessage(value);
        _rows.AddChild(text);
    }

    private static string Missing() => Loc.GetString("ember-dossier-unset");

    private string DescribeSpecies(HumanoidCharacterProfile profile)
    {
        var species = _prototypes.TryIndex(profile.Species, out SpeciesPrototype? proto)
            ? Loc.GetString(proto.Name)
            : profile.Species.Id;

        var sex = profile.Sex switch
        {
            Sex.Male => "humanoid-profile-editor-sex-male-text",
            Sex.Female => "humanoid-profile-editor-sex-female-text",
            _ => "humanoid-profile-editor-sex-unsexed-text",
        };

        return Loc.GetString("ember-dossier-species-line",
            ("species", species),
            ("sex", Loc.GetString(sex)),
            ("age", profile.Age));
    }

    private string BackgroundName(ProtoId<EmberBackgroundPrototype> id) =>
        _prototypes.TryIndex(id, out EmberBackgroundPrototype? proto) ? Loc.GetString(proto.Name) : Missing();

    private string EmployerName(HumanoidCharacterProfile profile) =>
        _prototypes.TryIndex(profile.Employer, out EmployerPrototype? proto)
            ? Loc.GetString(proto.NameKey)
            : Missing();

    private string RankName(HumanoidCharacterProfile profile) =>
        profile.Rank is { } rank && _prototypes.TryIndex(rank, out EmberRankPrototype? proto)
            ? Loc.GetString(proto.Name)
            : Missing();

    /// <summary>
    /// The post the character is most likely to be given, which is the highest priority they set.
    /// </summary>
    /// <remarks>
    /// Ties are broken by whichever the dictionary hands over first, and that is honest: the
    /// character has genuinely not said which of two equal preferences they want, and inventing an
    /// order here would claim otherwise. Only one is named because the record has one line for it,
    /// and the count says the rest are there.
    /// </remarks>
    private string PostName(HumanoidCharacterProfile profile)
    {
        JobPrototype? best = null;
        var bestPriority = JobPriority.Never;
        var wanted = 0;

        foreach (var (id, priority) in profile.JobPriorities)
        {
            if (priority == JobPriority.Never || !_prototypes.TryIndex(id, out JobPrototype? job))
                continue;

            wanted++;

            if (best != null && priority <= bestPriority)
                continue;

            best = job;
            bestPriority = priority;
        }

        if (best == null)
            return Missing();

        var name = Loc.GetString(best.Name);

        return wanted == 1
            ? name
            : Loc.GetString("ember-dossier-post-more", ("post", name), ("count", wanted - 1));
    }

    /// <summary>
    /// What still stands between this character and a shift, in one line.
    /// </summary>
    /// <remarks>
    /// Only the two things the player can be silently missing are checked. A branch that has ranks
    /// and no rank chosen is a half-filled posting; no post at all means the round starts by
    /// assigning one for them. Everything else the editor already refuses to let them do.
    /// </remarks>
    private FormattedMessage Readiness(HumanoidCharacterProfile profile, EmberBranchPrototype? branch)
    {
        var missing = new List<string>();

        if (branch is { SpawnRanks.Count: > 0 } && profile.Rank == null)
            missing.Add(Loc.GetString("ember-dossier-missing-rank"));

        if (PostName(profile) == Missing())
            missing.Add(Loc.GetString("ember-dossier-missing-post"));

        return FormattedMessage.FromMarkupPermissive(missing.Count == 0
            ? Loc.GetString("ember-dossier-ready")
            : Loc.GetString("ember-dossier-not-ready", ("missing", string.Join(", ", missing))));
    }
}
