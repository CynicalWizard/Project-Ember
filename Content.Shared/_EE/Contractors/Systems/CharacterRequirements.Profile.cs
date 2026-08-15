using System.Linq;
using Content.Shared._EE.Contractors.Prototypes;
using Content.Shared.Ember.Background;
using Content.Shared.CCVar;
using Content.Shared.Clothing.Loadouts.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Preferences;
using Content.Shared.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Traits;
using JetBrains.Annotations;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Customization.Systems;

/// <summary>
///     Requires the profile to have one of a list of backgrounds on a given axis.
/// </summary>
/// <remarks>
///     Ember: replaces the Contractors module's CharacterNationalityRequirement and
///     CharacterLifepathRequirement. Those were one class per profile field, which would have meant
///     four classes once the axes were split; the axis is data here instead.
/// </remarks>
[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterBackgroundRequirement : CharacterRequirement
{
    [DataField(required: true)]
    public EmberBackgroundAxis Axis;

    [DataField(required: true)]
    public HashSet<ProtoId<EmberBackgroundPrototype>> Backgrounds;

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
        MindComponent? mind = null
    )
    {
        const string color = "green";
        reason = Loc.GetString(
            $"character-background-requirement-{Axis.ToString().ToLowerInvariant()}",
            ("inverted", Inverted),
            ("background", $"[color={color}]{string.Join($"[/color], [color={color}]",
                Backgrounds.Select(s => Loc.GetString(prototypeManager.Index(s).Name)))}[/color]"));

        var chosen = Axis switch
        {
            EmberBackgroundAxis.Homeworld => profile.Homeworld,
            EmberBackgroundAxis.Culture => profile.Culture,
            EmberBackgroundAxis.Faction => profile.Faction,
            EmberBackgroundAxis.Religion => profile.Religion,
            _ => throw new ArgumentOutOfRangeException(nameof(Axis), Axis, null),
        };

        return Backgrounds.Contains(chosen);
    }
}


/// <summary>
///     Requires the profile to have one of a list of employers
/// </summary>
[UsedImplicitly, Serializable, NetSerializable]
public sealed partial class CharacterEmployerRequirement : CharacterRequirement
{
    [DataField(required: true)]
    public HashSet<ProtoId<EmployerPrototype>> Employers;

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
        MindComponent? mind = null
    )
    {
        if (!configManager.GetCVar(CCVars.ContractorsEnabled) ||
            !configManager.GetCVar(CCVars.ContractorsCharacterRequirementsEnabled))
        {
            reason = "";
            return true;
        }

        var localeString = "character-employer-requirement";
        const string color = "green";
        reason = Loc.GetString(
            localeString,
            ("inverted", Inverted),
            ("employers", $"[color={color}]{string.Join($"[/color], [color={color}]",
                Employers.Select(s => Loc.GetString(prototypeManager.Index(s).NameKey)))}[/color]"));
        return Employers.Any(o => o == profile.Employer);
    }
}

