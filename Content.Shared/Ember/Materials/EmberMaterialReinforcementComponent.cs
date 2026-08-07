using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Materials;

/// <summary>
/// A lattice of something else set into a thing, stiffening it without being what the thing is made of.
/// </summary>
/// <remarks>
/// Bay counts a window's reinforcement as a quarter of its own melting point rather than as a material in its
/// own right: a reinforced pane is still glass and still shatters, it just wants a hotter fire to do it. A wall
/// keeps its reinforcement on the wall component instead, because there it counts for the whole of its melting
/// point — which is the difference between a plasteel-backed bulkhead and a pane with rods in it.
/// </remarks>
[RegisterComponent]
public sealed partial class EmberMaterialReinforcementComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EmberMaterialPrototype> Material;
}
