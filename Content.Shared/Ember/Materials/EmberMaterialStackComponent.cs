using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Materials;

[RegisterComponent]
public sealed partial class EmberMaterialStackComponent : Component
{
    [DataField(required: true)]
    public ProtoId<EmberMaterialPrototype> Material;

    [DataField]
    public bool Tint = true;

    [DataField]
    public bool RenameEntity = true;

    /// <summary>
    /// A name that follows how many are in the stack, handed the count as <c>$count</c>. Bay keeps a separate
    /// singular and plural name on every material; one Fluent id does the same job and, unlike a pair of English
    /// strings, gets languages whose plurals are not a binary right.
    /// </summary>
    [DataField]
    public LocId? CountedName;

    /// <summary>The description to match, since one rod and an armful of them do not read the same way.</summary>
    [DataField]
    public LocId? CountedDescription;
}
