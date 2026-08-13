using System.Linq;
using Content.Shared.Ember.Ranks;
using Content.Shared.Ember.Roles;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Skills;

/// <summary>
/// Skills belong to the character, not to the job.
/// </summary>
/// <remarks>
/// This departs from SierraBay12 on purpose. Bay allocates points per job: the same character
/// carries one set of skills as an engineer and a different set as a surgeon, and a job hands
/// out its minimums for free. That is incompatible with a character being a person — someone is
/// not simultaneously a surgeon and a reactor technician with two different sets of hands.
///
/// So: one allocation per character, spent once, and a job states what it <em>requires</em>
/// rather than what it grants. Which jobs a character can take then falls out of the data
/// instead of being listed by hand — the paramedic and the field medic overlap because they ask
/// for the same things, not because someone wrote down that they are similar.
///
/// The cost curve and the delay/failure formulas are still Bay's.
/// </remarks>
public sealed class SharedSkillsSystem : EntitySystem
{
    /// <summary>
    /// Points every character starts with, before age is taken into account.
    /// </summary>
    /// <remarks>
    /// Derived rather than picked: it is the cost of the most demanding job's requirements. The
    /// Chief Medical Officer and the Physician both need 32 points' worth of medicine and
    /// anatomy, so a character who is exactly that spends the whole allowance and has nothing
    /// left over — which is the correct shape for someone who is all job. Cheaper jobs leave
    /// room for a second trade, and age adds a little on top.
    ///
    /// Age gating is not this number's business: a thirty-year-old cannot be the Chief Medical
    /// Officer because the post requires an O-3 commission, not because the points run out.
    ///
    /// EMBER-TODO: wants to be a CVar once there is anything to balance against — right now no
    /// system reads a skill except construction, so tuning this would be tuning nothing.
    /// </remarks>
    public const int BaseSkillPoints = 32;

    #region Cost

    /// <summary>
    /// Cost of the single step up to <paramref name="level"/>. Bay's curve: the lower two levels
    /// cost the skill's difficulty, the upper two cost double it.
    /// </summary>
    public static int GetLevelCost(SkillPrototype skill, SkillLevel level)
    {
        return level switch
        {
            SkillLevel.Basic or SkillLevel.Trained => skill.Difficulty,
            SkillLevel.Experienced or SkillLevel.Master => 2 * skill.Difficulty,
            _ => 0,
        };
    }

    /// <summary>
    /// Cost of reaching <paramref name="level"/> from scratch. Everyone starts Unskilled and
    /// there is no job minimum to begin from, so every step is paid for.
    /// </summary>
    public static int GetTotalCost(SkillPrototype skill, SkillLevel level)
    {
        var cost = 0;

        for (var current = (int) SkillLevels.Min + 1; current <= (int) ClampLevel((int) level); current++)
        {
            cost += GetLevelCost(skill, (SkillLevel) current);
        }

        return cost;
    }

    public static int GetSpentPoints(
        IEnumerable<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> allocation)
    {
        var spent = 0;

        foreach (var skill in skills)
        {
            if (allocation.TryGetValue(skill.ID, out var level))
                spent += GetTotalCost(skill, level);
        }

        return spent;
    }

    public static int GetRemainingPoints(
        IEnumerable<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> allocation,
        int budget)
    {
        return budget - GetSpentPoints(skills, allocation);
    }

    #endregion

    #region Budget

    /// <summary>
    /// Extra points for age. This is how time served becomes competence without a separate
    /// experience system: an officer in their forties simply has more to spend than a recruit.
    /// </summary>
    public static int GetAgeSkillPoints(SpeciesPrototype? species, int age)
    {
        if (species == null)
            return 0;

        if (species.SkillAgePoints.Count > 0)
            return GetConfiguredAgeSkillPoints(species, age);

        if (age < Math.Max(0, species.YoungAge - 7))
            return 0;

        if (age <= species.YoungAge)
            return 3;

        var experiencedThreshold = ((species.YoungAge + species.OldAge) / 2) + 1;
        if (age < experiencedThreshold)
            return 6;

        return 8;
    }

    /// <summary>
    /// One allowance per character, spent once. No job contributes to it.
    /// </summary>
    public static int GetSkillPointBudget(SpeciesPrototype? species, int age)
    {
        return Math.Max(0, BaseSkillPoints + GetAgeSkillPoints(species, age));
    }

    private static int GetConfiguredAgeSkillPoints(SpeciesPrototype species, int age)
    {
        var points = 0;
        var bestMinimum = int.MinValue;

        foreach (var bracket in species.SkillAgePoints)
        {
            if (age < bracket.MinimumAge || bracket.MinimumAge < bestMinimum)
                continue;

            points = bracket.Points;
            bestMinimum = bracket.MinimumAge;
        }

        return points;
    }

    #endregion

    #region Sanitising

    public static Dictionary<ProtoId<SkillPrototype>, SkillLevel> SanitizeAllocation(
        IPrototypeManager prototype,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> allocation,
        SpeciesPrototype? species,
        int age)
    {
        var skills = GetOrderedSkills(prototype);
        return SanitizeAllocation(skills, allocation, GetSkillPointBudget(species, age));
    }

    /// <summary>
    /// Clamps an allocation to what the character could actually have: no level above the
    /// skill's own ceiling, no skill whose prerequisites are unmet, and nothing beyond the
    /// budget. Every skill appears in the result, Unskilled if it was not bought.
    /// </summary>
    public static Dictionary<ProtoId<SkillPrototype>, SkillLevel> SanitizeAllocation(
        IReadOnlyCollection<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> allocation,
        int budget)
    {
        var values = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>();

        foreach (var skill in skills)
        {
            var level = allocation.GetValueOrDefault(skill.ID, SkillLevels.Min);
            values[skill.ID] = MinLevel(ClampLevel((int) level), skill.DefaultMax);
        }

        // Dropping one skill can invalidate another that depended on it, so this runs until
        // nothing moves rather than once down the list.
        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var skill in skills)
            {
                if (values[skill.ID] <= SkillLevels.Min || CheckPrerequisites(skill, values))
                    continue;

                values[skill.ID] = SkillLevels.Min;
                changed = true;
            }
        }

        var remaining = budget;

        foreach (var skill in skills)
        {
            var level = values[skill.ID];
            if (level <= SkillLevels.Min)
                continue;

            var cost = GetTotalCost(skill, level);
            if (remaining - cost < 0)
            {
                values[skill.ID] = SkillLevels.Min;
                continue;
            }

            remaining -= cost;
        }

        return values;
    }

    public static bool CheckPrerequisites(
        SkillPrototype skill,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> values)
    {
        foreach (var (prerequisite, level) in skill.Prerequisites)
        {
            if (!values.TryGetValue(prerequisite, out var current) || current < level)
                return false;
        }

        return true;
    }

    public static SkillPrototype[] GetOrderedSkills(IPrototypeManager prototype)
    {
        return prototype.EnumeratePrototypes<SkillPrototype>()
            .OrderBy(skill => skill.ID)
            .ToArray();
    }

    #endregion

    #region Job requirements

    /// <summary>
    /// Everything a character must clear to hold <paramref name="job"/> while serving in
    /// <paramref name="branch"/>: the job's own floors, raised wherever the branch asks for more.
    /// </summary>
    /// <remarks>
    /// A branch's <see cref="EmberBranchPrototype.MinSkills"/> is what everyone in it is expected
    /// to know whatever their posting — voidsuit work aboard a ship, small arms in an armed
    /// service — and it exists so that thirty job prototypes do not each repeat the same three
    /// lines. That only holds if something actually merges the two, which is what this does.
    ///
    /// Note that it is a floor and not a grant. Skills belong to the character and are paid for
    /// out of the character's own points, so joining a service costs a few of them before a job
    /// is even chosen. Granting them instead would hand every serviceman free points and quietly
    /// undo the budget.
    ///
    /// The branch is the character's, not the job's: a post open to two branches asks each of
    /// its people for what their own service expects, not for the union of both.
    ///
    /// A job title, where the character has picked one, raises the floor the same way. Most
    /// titles are only names and add nothing; the ones that are not — surgeon rather than
    /// doctor — are exactly what this exists for.
    /// </remarks>
    public static Dictionary<ProtoId<SkillPrototype>, SkillLevel> GetRequiredSkills(
        JobPrototype job,
        EmberBranchPrototype? branch,
        EmberJobTitle? title = null)
    {
        var required = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>(job.MinSkills);

        if (branch != null)
            Raise(required, branch.MinSkills);

        if (title != null)
            Raise(required, title.MinSkills);

        return required;
    }

    private static void Raise(
        Dictionary<ProtoId<SkillPrototype>, SkillLevel> required,
        Dictionary<ProtoId<SkillPrototype>, SkillLevel> floors)
    {
        foreach (var (skill, level) in floors)
        {
            if (!required.TryGetValue(skill, out var floor) || floor < level)
                required[skill] = level;
        }
    }

    /// <summary>
    /// Whether a character with these skills may take this job. A job's minimums are a floor to
    /// clear, not levels it hands out.
    /// </summary>
    public static bool MeetsRequirements(
        JobPrototype job,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> skills,
        EmberBranchPrototype? branch = null,
        EmberJobTitle? title = null)
    {
        foreach (var (skill, required) in GetRequiredSkills(job, branch, title))
        {
            if (skills.GetValueOrDefault(skill, SkillLevels.Min) < required)
                return false;
        }

        return true;
    }

    #endregion

    #region Runtime

    public SkillLevel GetSkillValue(EntityUid uid, ProtoId<SkillPrototype> skill, SkillSetComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return SkillLevels.Default;

        var value = component.BaseSkills.GetValueOrDefault(skill, component.DefaultLevel);
        value = ClampLevel((int) value + component.Modifiers.GetValueOrDefault(skill));
        return value;
    }

    public bool SkillCheck(
        EntityUid uid,
        ProtoId<SkillPrototype> skill,
        SkillLevel required,
        SkillSetComponent? component = null)
    {
        return GetSkillValue(uid, skill, component) >= required;
    }

    public float GetSkillDelayMultiplier(
        EntityUid uid,
        ProtoId<SkillPrototype> skill,
        float factor = 0.3f,
        SkillSetComponent? component = null)
    {
        return GetDelayMultiplier(GetSkillValue(uid, skill, component), factor);
    }

    public int GetSkillFailChance(
        EntityUid uid,
        ProtoId<SkillPrototype> skill,
        int failChance,
        SkillLevel noMoreFail = SkillLevel.Master,
        float factor = 1f,
        SkillSetComponent? component = null)
    {
        return GetFailChance(GetSkillValue(uid, skill, component), failChance, noMoreFail, factor);
    }

    /// <summary>
    /// Bay's <c>skill_delay_mult</c>. Trained is the baseline that takes the listed time; every level either side
    /// of it moves the time by <paramref name="factor"/>.
    /// </summary>
    public static float GetDelayMultiplier(SkillLevel points, float factor = 0.3f)
    {
        return Math.Max(0f, 1f + ((int) SkillLevels.Baseline - (int) points) * factor);
    }

    /// <summary>
    /// Bay's <c>skill_fail_chance</c>. <paramref name="failChance"/> is the chance at Unskilled and it halves per
    /// level, down to nothing once <paramref name="noMoreFail"/> is reached.
    /// </summary>
    public static int GetFailChance(
        SkillLevel points,
        int failChance,
        SkillLevel noMoreFail = SkillLevel.Master,
        float factor = 1f)
    {
        if (points >= noMoreFail)
            return 0;

        return (int) MathF.Round(failChance * MathF.Pow(2f, factor * ((int) SkillLevels.Min - (int) points)));
    }

    #endregion

    private static SkillLevel ClampLevel(int level)
    {
        return (SkillLevel) Math.Clamp(level, (int) SkillLevels.Min, (int) SkillLevels.Max);
    }

    private static SkillLevel MinLevel(SkillLevel first, SkillLevel second)
    {
        return first < second ? first : second;
    }
}
