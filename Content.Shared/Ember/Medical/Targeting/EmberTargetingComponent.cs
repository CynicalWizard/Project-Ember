using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.Ember.Medical.Targeting;

/// <summary>
/// Controls entity limb targeting for actions.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmberTargetingComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EmberTargetBodyPart Target = EmberTargetBodyPart.Torso;

    /// <summary>
    /// What odds does the entity have of targeting each body part?
    /// </summary>
    [DataField]
    public Dictionary<EmberTargetBodyPart, float> TargetOdds = new()
    {
        { EmberTargetBodyPart.Head, 0.1f },
        { EmberTargetBodyPart.Torso, 0.3f },
        { EmberTargetBodyPart.Groin, 0.1f },
        { EmberTargetBodyPart.LeftArm, 0.1f },
        { EmberTargetBodyPart.LeftHand, 0.05f },
        { EmberTargetBodyPart.RightArm, 0.1f },
        { EmberTargetBodyPart.RightHand, 0.05f },
        { EmberTargetBodyPart.LeftLeg, 0.1f },
        { EmberTargetBodyPart.LeftFoot, 0.05f },
        { EmberTargetBodyPart.RightLeg, 0.1f },
        { EmberTargetBodyPart.RightFoot, 0.05f }
    };

    /// <summary>
    /// What is the current integrity of each body part?
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<EmberTargetBodyPart, EmberTargetIntegrity> BodyStatus = new()
    {
        { EmberTargetBodyPart.Head, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.Torso, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.Groin, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.LeftArm, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.LeftHand, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.RightArm, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.RightHand, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.LeftLeg, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.LeftFoot, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.RightLeg, EmberTargetIntegrity.Healthy },
        { EmberTargetBodyPart.RightFoot, EmberTargetIntegrity.Healthy }
    };

    /// <summary>
    /// What noise does the entity play when swapping targets?
    /// </summary>
    [DataField]
    public string SwapSound = "/Audio/Effects/toggleoncombat.ogg";
}
