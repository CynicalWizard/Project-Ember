using Content.Shared.Preferences;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._EE.Contractors.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PassportComponent : Component
{
    [ViewVariables]
    public HumanoidCharacterProfile OwnerProfile;
}
