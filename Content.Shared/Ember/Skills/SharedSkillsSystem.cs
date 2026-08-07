using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Skills;

public sealed class SharedSkillsSystem : EntitySystem
{
    public static int GetLevelCost(SkillPrototype skill, SkillLevel level)
    {
        return level switch
        {
            SkillLevel.Basic or SkillLevel.Trained => skill.Difficulty,
            SkillLevel.Experienced or SkillLevel.Master => 2 * skill.Difficulty,
            _ => 0,
        };
    }

    public static SkillLevel GetMinSkill(JobPrototype? job, SkillPrototype skill)
    {
        if (job != null && job.MinSkills.TryGetValue(skill.ID, out var min))
            return ClampLevel((int) min);

        return SkillLevels.Min;
    }

    public static SkillLevel GetMaxSkill(JobPrototype? job, SkillPrototype skill)
    {
        var min = GetMinSkill(job, skill);
        var max = skill.DefaultMax;

        if (job != null && job.MaxSkills.TryGetValue(skill.ID, out var jobMax))
            max = jobMax;

        return MaxLevel(min, ClampLevel((int) max));
    }

    public static int GetLevelCost(JobPrototype? job, SkillPrototype skill, SkillLevel level)
    {
        var min = GetMinSkill(job, skill);
        var target = ClampLevel((int) level);
        var cost = 0;

        for (var current = (byte) min + 1; current <= (byte) target; current++)
        {
            cost += GetLevelCost(skill, (SkillLevel) current);
        }

        return cost;
    }

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

    public static int GetSkillPointBudget(JobPrototype job, SpeciesPrototype? species, int age)
    {
        var budget = job.SkillPoints;
        if (!job.NoSkillBuffs)
            budget += GetAgeSkillPoints(species, age);

        return Math.Max(0, budget);
    }

    public static int GetRemainingPoints(
        JobPrototype job,
        IEnumerable<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, byte> allocation)
    {
        return GetRemainingPoints(job, skills, allocation, job.SkillPoints);
    }

    public static int GetRemainingPoints(
        JobPrototype job,
        IEnumerable<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, byte> allocation,
        int skillPointBudget)
    {
        var spent = 0;

        foreach (var skill in skills)
        {
            if (!allocation.TryGetValue(skill.ID, out var addedLevels) || addedLevels == 0)
                continue;

            var min = GetMinSkill(job, skill);
            var level = ClampLevel((int) min + addedLevels);
            spent += GetLevelCost(job, skill, level);
        }

        return skillPointBudget - spent;
    }

    public static Dictionary<ProtoId<JobPrototype>, Dictionary<ProtoId<SkillPrototype>, byte>> SanitizeAllocations(
        IPrototypeManager prototype,
        IReadOnlyDictionary<ProtoId<JobPrototype>, Dictionary<ProtoId<SkillPrototype>, byte>> allocations)
    {
        return SanitizeAllocations(prototype, allocations, null, 0);
    }

    public static Dictionary<ProtoId<JobPrototype>, Dictionary<ProtoId<SkillPrototype>, byte>> SanitizeAllocations(
        IPrototypeManager prototype,
        IReadOnlyDictionary<ProtoId<JobPrototype>, Dictionary<ProtoId<SkillPrototype>, byte>> allocations,
        SpeciesPrototype? species,
        int age)
    {
        var sanitized = new Dictionary<ProtoId<JobPrototype>, Dictionary<ProtoId<SkillPrototype>, byte>>();
        var skills = prototype.EnumeratePrototypes<SkillPrototype>()
            .OrderBy(skill => skill.ID)
            .ToArray();

        foreach (var (jobId, jobAllocation) in allocations)
        {
            if (!prototype.TryIndex(jobId, out var job))
                continue;

            var budget = GetSkillPointBudget(job, species, age);
            var clean = SanitizeAllocation(job, skills, jobAllocation, budget);
            if (clean.Count > 0)
                sanitized[jobId] = clean;
        }

        return sanitized;
    }

    public static Dictionary<ProtoId<SkillPrototype>, byte> SanitizeAllocation(
        JobPrototype job,
        IReadOnlyCollection<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, byte> allocation)
    {
        return SanitizeAllocation(job, skills, allocation, job.SkillPoints);
    }

    public static Dictionary<ProtoId<SkillPrototype>, byte> SanitizeAllocation(
        JobPrototype job,
        IReadOnlyCollection<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, byte> allocation,
        int skillPointBudget)
    {
        var values = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>();

        foreach (var skill in skills)
        {
            var min = GetMinSkill(job, skill);
            var max = GetMaxSkill(job, skill);
            var level = min;

            if (allocation.TryGetValue(skill.ID, out var addedLevels))
                level = ClampLevel((int) min + addedLevels);

            values[skill.ID] = MinLevel(level, max);
        }

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var skill in skills)
            {
                if (values[skill.ID] <= GetMinSkill(job, skill))
                    continue;

                if (CheckPrerequisites(skill, values))
                    continue;

                values[skill.ID] = GetMinSkill(job, skill);
                changed = true;
            }
        }

        var remaining = skillPointBudget;
        var clean = new Dictionary<ProtoId<SkillPrototype>, byte>();

        foreach (var skill in skills)
        {
            var min = GetMinSkill(job, skill);
            var level = values[skill.ID];
            if (level <= min)
                continue;

            var cost = GetLevelCost(job, skill, level);
            if (remaining - cost < 0)
                continue;

            remaining -= cost;
            clean[skill.ID] = (byte) ((int) level - (int) min);
        }

        return clean;
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

    public static Dictionary<ProtoId<SkillPrototype>, SkillLevel> GetFinalSkillValues(
        JobPrototype? job,
        IEnumerable<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, byte> allocation)
    {
        var values = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>();

        foreach (var skill in skills)
        {
            var min = GetMinSkill(job, skill);
            var max = GetMaxSkill(job, skill);
            var level = min;

            if (allocation.TryGetValue(skill.ID, out var addedLevels))
                level = ClampLevel((int) min + addedLevels);

            values[skill.ID] = MinLevel(level, max);
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

    private static SkillLevel ClampLevel(int level)
    {
        return (SkillLevel) Math.Clamp(level, (int) SkillLevels.Min, (int) SkillLevels.Max);
    }

    private static SkillLevel MaxLevel(SkillLevel first, SkillLevel second)
    {
        return first > second ? first : second;
    }

    private static SkillLevel MinLevel(SkillLevel first, SkillLevel second)
    {
        return first < second ? first : second;
    }
}
