namespace Content.Shared.Ember.Doors;

/// <summary>
/// Marks a firelock as using the SierraBay hazard shutter sprite set, which orients itself to the
/// surrounding walls instead of relying on the mapper-set rotation.
/// </summary>
/// <remarks>
/// Purely visual, so nothing here is networked. <see cref="Content.Client.Ember.Doors.EmberProceduralFirelockSystem"/>
/// does the work.
/// </remarks>
[RegisterComponent]
public sealed partial class EmberProceduralFirelockComponent : Component
{
    /// <summary>
    /// Lets subtypes that keep a vanilla sprite sheet (the edge firelock) opt out without having to drop the
    /// component, which entity prototypes cannot do.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// State shown on <see cref="Content.Shared.Doors.Components.DoorVisualLayers.BaseUnlit"/> while the firelock
    /// is holding an atmospheric alarm. Bay calls this the pressure alert light.
    /// </summary>
    [DataField]
    public string AlertState = "palert";

    /// <summary>
    /// State flicked on the base layer when access is denied.
    /// </summary>
    [DataField]
    public string DenyState = "deny";
}
