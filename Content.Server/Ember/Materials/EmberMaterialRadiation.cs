using Content.Server.Radiation.Components;
using Content.Shared.Ember.Walls;
using Content.Shared.Radiation.Components;

namespace Content.Server.Ember.Materials;

/// <summary>
/// Turns a material's radioactivity into a radiation source on whatever is made of it.
/// </summary>
public static class EmberMaterialRadiation
{
    /// <summary>
    /// Adds or removes the source for <paramref name="radioactivity"/> on Bay's scale.
    /// </summary>
    /// <remarks>
    /// The catch is that the radiation gridcast starts on the source's own tile and subtracts every blocker it
    /// crosses, the source's included. A uranium bulkhead blocks radiation at 10, which swallowed its own 1.2
    /// whole and made a geiger counter next to it read zero. Adding the entity's own resistance back means the
    /// ray leaves the tile at the intensity the material actually calls for, and the shielding still applies to
    /// anything radiating from behind it.
    /// </remarks>
    public static void Apply(IEntityManager entities, EntityUid uid, float radioactivity)
    {
        if (radioactivity <= 0f)
        {
            entities.RemoveComponent<RadiationSourceComponent>(uid);
            return;
        }

        var intensity = radioactivity * EmberWallMaterialStats.RadiationIntensityScale;

        if (entities.TryGetComponent(uid, out RadiationBlockerComponent? blocker) && blocker.Enabled)
            intensity += blocker.RadResistance;

        entities.EnsureComponent<RadiationSourceComponent>(uid).Intensity = intensity;
    }
}
