using System.Linq;
using Content.Shared.Ember.Skills;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Server.EUI;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Skills;

public sealed class SkillsSystem : EntitySystem
{
    private static readonly ProtoId<AntagSkillSetPrototype> GenericAntagSkillSet = "GenericAntag";

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly EuiManager _eui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoleAddedEvent>(OnRoleAdded);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        ApplyProfileSkills(ev.Mob, ev.JobId, ev.Profile);

        if (_mind.TryGetMind(ev.Mob, out _, out var mind))
            InitializeAntagSkills(ev.Mob, mind, false);
    }

    private void OnRoleAdded(RoleAddedEvent ev)
    {
        if (ev.Mind.OwnedEntity is not { } mob)
            return;

        InitializeAntagSkills(mob, ev.Mind, true);
    }

    /// <summary>
    /// Applies the character's own skills. The job is no longer part of this: a character
    /// carries one set of skills whatever post they are filling, and the job's minimums are a
    /// requirement they already met rather than levels handed out on spawn.
    /// </summary>
    public void ApplyProfileSkills(
        EntityUid mob,
        string? jobId,
        HumanoidCharacterProfile profile)
    {
        _prototype.TryIndex(profile.Species, out SpeciesPrototype? species);

        var finalValues = SharedSkillsSystem.SanitizeAllocation(
            _prototype,
            profile.Skills,
            species,
            profile.Age);

        var component = EnsureComp<SkillSetComponent>(mob);

        component.BaseSkills.Clear();
        component.Modifiers.Clear();
        foreach (var (skill, level) in finalValues)
        {
            component.BaseSkills[skill] = level;
        }

        Dirty(mob, component);
        RaiseLocalEvent(mob, new SkillsLoadedEvent(mob, component));
    }

    private void InitializeAntagSkills(EntityUid mob, MindComponent mind, bool openPicker)
    {
        if (!TryGetAntagSkillSet(mind, out var antagSkills))
            return;

        var skills = _prototype.EnumeratePrototypes<SkillPrototype>().ToArray();
        var component = EnsureComp<SkillSetComponent>(mob);
        var selection = EnsureComp<AntagSkillSelectionComponent>(mob);

        if (selection.Set != antagSkills.ID)
        {
            selection.Set = antagSkills.ID;
            selection.Selected.Clear();
            selection.Committed = false;
        }

        foreach (var skill in skills)
        {
            RaiseSkillTo(component, skill, antagSkills.DefaultLevel);
        }

        foreach (var (skillId, level) in antagSkills.Skills)
        {
            if (_prototype.TryIndex(skillId, out var skill))
                RaiseSkillTo(component, skill, level);
        }

        Dirty(mob, component);
        RaiseLocalEvent(mob, new SkillsLoadedEvent(mob, component));

        if (openPicker && !selection.Committed && mind.Session != null)
        {
            var ui = new AntagSkillsEui(mob, antagSkills.ID);
            _eui.OpenEui(ui, mind.Session);
            ui.StateDirty();
        }
    }

    private bool TryGetAntagSkillSet(MindComponent mind, out AntagSkillSetPrototype antagSkills)
    {
        var antagPrototypes = new HashSet<ProtoId<AntagPrototype>>();
        var hasAntagRole = false;

        foreach (var role in mind.MindRoles)
        {
            if (!TryComp<MindRoleComponent>(role, out var roleComp))
                continue;

            hasAntagRole |= roleComp.Antag || roleComp.ExclusiveAntag || roleComp.AntagPrototype != null;
            if (roleComp.AntagPrototype != null)
                antagPrototypes.Add(roleComp.AntagPrototype.Value);
        }

        if (!hasAntagRole)
        {
            antagSkills = default!;
            return false;
        }

        foreach (var set in _prototype.EnumeratePrototypes<AntagSkillSetPrototype>())
        {
            if (set.ID == GenericAntagSkillSet)
                continue;

            if (set.Antags.Any(antagPrototypes.Contains))
            {
                antagSkills = set;
                return true;
            }
        }

        if (_prototype.TryIndex(GenericAntagSkillSet, out var genericSet))
        {
            antagSkills = genericSet;
            return true;
        }

        antagSkills = default!;
        return false;
    }

    private static void RaiseSkillTo(SkillSetComponent component, SkillPrototype skill, SkillLevel target)
    {
        var level = ClampToSkill(skill, target);
        if (level <= SkillLevels.Min)
            return;

        var current = component.BaseSkills.GetValueOrDefault(skill.ID, SkillLevels.Min);
        if (current < level)
            component.BaseSkills[skill.ID] = level;
    }

    private static SkillLevel ClampToSkill(SkillPrototype skill, SkillLevel level)
    {
        var max = Math.Min((int) SkillLevels.Max, (int) SkillLevels.Min + skill.Levels.Count - 1);
        return (SkillLevel) Math.Clamp((int) level, (int) SkillLevels.Min, max);
    }
}
