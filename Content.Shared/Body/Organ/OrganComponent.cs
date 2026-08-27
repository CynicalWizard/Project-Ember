using Content.Shared.Body.Systems;
using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes; // Ember
using Content.Shared.Ember.Medical.Surgery; // Ember
using Content.Shared.Ember.Medical.Surgery.Tools; // Ember

namespace Content.Shared.Body.Organ;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedBodySystem), typeof(SharedEmberSurgerySystem))] // Ember
public sealed partial class OrganComponent : Component, IEmberSurgeryToolComponent // Ember
{
    /// <summary>
    /// Relevant body this organ is attached to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Body;

    /// <summary>
    ///     Ember:Relevant body this organ originally belonged to.
    ///     FOR WHATEVER FUCKING REASON AUTONETWORKING THIS CRASHES GIBTEST AAAAAAAAAAAAAAA
    /// </summary>
    [DataField]
    public EntityUid? OriginalBody;

    // Ember Start
    /// <summary>
    ///     Ember: Shitcodey solution to not being able to know what name corresponds to each organ's slot ID
    ///     without referencing the prototype or hardcoding.
    /// </summary>

    [DataField]
    public string SlotId = string.Empty;

    [DataField]
    public string ToolName { get; set; } = "An organ";

    [DataField]
    public float Speed { get; set; } = 1f;

    /// <summary>
    ///     Ember: If true, the organ will not heal an entity when transplanted into them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool? Used { get; set; }


    /// <summary>
    ///     When attached, the organ will ensure these components on the entity, and delete them on removal.
    /// </summary>
    [DataField]
    public ComponentRegistry? OnAdd;

    /// <summary>
    ///     When removed, the organ will ensure these components on the entity, and delete them on insertion.
    /// </summary>
    [DataField]
    public ComponentRegistry? OnRemove;

    /// <summary>
    ///     Is this organ working or not?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    ///     Can this organ be enabled or disabled? Used mostly for prop, damaged or useless organs.
    /// </summary>
    [DataField]
    public bool CanEnable = true;
    // Ember End

    /// <summary>
    ///     These functions are called when this organ is added/implanted to an entity.
    /// </summary>
    [DataField(serverOnly: true)]
    public TraitFunction[] OnImplantFunctions { get; private set; } = Array.Empty<TraitFunction>();

    /// <summary>
    ///     These functions are called when this organ is removed from an entity.
    /// </summary>
    [DataField(serverOnly: true)]
    public TraitFunction[] OnRemoveFunctions { get; private set; } = Array.Empty<TraitFunction>();
}
