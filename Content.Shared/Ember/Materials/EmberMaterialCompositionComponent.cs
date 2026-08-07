using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Materials;

/// <summary>
/// What a thing is made of, when that is fixed and does not show.
/// </summary>
/// <remarks>
/// Most things that name a material do so because the material decides how they look: a wall is coloured by it,
/// a stack is tinted by it, a window is glass and looks it. A girder is none of those — on Bay it is steel
/// whatever wall ends up being built on it, it is never recoloured, and it hands back a steel sheet when it comes
/// apart. It still needs to be made of something, because a fire asks what a thing melts at and gets no answer
/// from a prototype that never mentions a material.
/// </remarks>
[RegisterComponent]
public sealed partial class EmberMaterialCompositionComponent : Component
{
    /// <summary>The materials, most important first, exactly as the rest of the lookup reports them.</summary>
    [DataField(required: true)]
    public List<ProtoId<EmberMaterialPrototype>> Materials = new();
}
