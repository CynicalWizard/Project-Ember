using System.Linq;
using Content.Shared.Customization.Systems;
using Content.Shared.Ember.Skills;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Ranks;

/// <summary>
/// Requires the character to serve in one of a list of branches.
/// </summary>
/// <remarks>
/// SierraBay12 puts this on the job as <c>allowed_branches</c>. Here it is a
/// <see cref="CharacterRequirement"/> instead, next to the employer and nationality ones, so
/// that the lobby already knows how to grey the job out and say why — the same machinery that
/// locks a job behind playtime locks it behind service.
/// </remarks>
[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterBranchRequirement : CharacterRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<EmberBranchPrototype>> Branches = new();

    public override bool IsValid(
        JobPrototype job,
        HumanoidCharacterProfile profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        bool whitelisted,
        IPrototype prototype,
        IEntityManager entityManager,
        IPrototypeManager prototypeManager,
        IConfigurationManager configManager,
        out string? reason,
        int depth = 0,
        MindComponent? mind = null)
    {
        reason = Loc.GetString(
            "character-branch-requirement",
            ("inverted", Inverted),
            ("branches", string.Join(", ", Branches
                .Select(id => prototypeManager.TryIndex(id, out var branch)
                    ? Loc.GetString(branch.Name)
                    : id.Id))));

        return profile.Branch is { } branch && Branches.Contains(branch);
    }
}

/// <summary>
/// Requires the character to hold one of a list of ranks. Bay's <c>allowed_ranks</c>.
/// </summary>
/// <remarks>
/// Separate from <see cref="CharacterBranchRequirement"/> because a job usually admits several
/// branches but only a narrow band of ranks in each: the executive officer is a Commander of
/// the Corps or a Lieutenant Commander of the Fleet, not anyone from either.
/// </remarks>
[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterRankRequirement : CharacterRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<EmberRankPrototype>> Ranks = new();

    public override bool IsValid(
        JobPrototype job,
        HumanoidCharacterProfile profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        bool whitelisted,
        IPrototype prototype,
        IEntityManager entityManager,
        IPrototypeManager prototypeManager,
        IConfigurationManager configManager,
        out string? reason,
        int depth = 0,
        MindComponent? mind = null)
    {
        reason = Loc.GetString(
            "character-rank-requirement",
            ("inverted", Inverted),
            ("ranks", string.Join(", ", Ranks
                .Select(id => prototypeManager.TryIndex(id, out var rank)
                    ? Loc.GetString(rank.Name)
                    : id.Id))));

        return profile.Rank is { } rank && Ranks.Contains(rank);
    }
}

/// <summary>
/// Requires the character's own skills to clear the job's <see cref="JobPrototype.MinSkills"/>.
/// </summary>
/// <remarks>
/// Deliberately reads the floors off the job rather than repeating them: the requirement exists
/// to surface in the lobby what the job already declares, and a second copy of the numbers would
/// drift from the first.
///
/// This is what makes overlapping roles work without a table of them. Two jobs asking for the
/// same skills accept the same characters, and nobody has to write down that a paramedic and a
/// field medic are similar.
/// </remarks>
[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterSkillRequirement : CharacterRequirement
{
    public override bool IsValid(
        JobPrototype job,
        HumanoidCharacterProfile profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        bool whitelisted,
        IPrototype prototype,
        IEntityManager entityManager,
        IPrototypeManager prototypeManager,
        IConfigurationManager configManager,
        out string? reason,
        int depth = 0,
        MindComponent? mind = null)
    {
        var missing = job.MinSkills
            .Where(pair => profile.GetSkill(pair.Key) < pair.Value)
            .Select(pair => Loc.GetString(
                "character-skill-requirement-entry",
                ("skill", prototypeManager.TryIndex(pair.Key, out var skill)
                    ? Loc.GetString(skill.Name)
                    : pair.Key.Id),
                ("level", GetLevelName(prototypeManager, pair.Key, pair.Value))))
            .ToList();

        reason = Loc.GetString(
            "character-skill-requirement",
            ("inverted", Inverted),
            ("skills", string.Join(", ", missing)));

        return missing.Count == 0;
    }

    /// <summary>
    /// A skill names its own ladder, because not every skill has five rungs — exosuit training
    /// runs civilian then combat, and calling that "Trained" would be nonsense.
    /// </summary>
    private static string GetLevelName(
        IPrototypeManager prototypeManager,
        ProtoId<SkillPrototype> skillId,
        SkillLevel level)
    {
        if (!prototypeManager.TryIndex(skillId, out var skill))
            return level.ToString();

        var index = (int) level - (int) SkillLevels.Min;
        return index >= 0 && index < skill.Levels.Count
            ? Loc.GetString(skill.Levels[index])
            : level.ToString();
    }
}
