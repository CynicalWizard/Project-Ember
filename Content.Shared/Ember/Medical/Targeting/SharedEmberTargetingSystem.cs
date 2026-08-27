namespace Content.Shared.Ember.Medical.Targeting;
public abstract class SharedEmberTargetingSystem : EntitySystem
{
    /// <summary>
    /// Returns all Valid target body parts as an array.
    /// </summary>
    public static EmberTargetBodyPart[] GetValidParts()
    {
        var parts = new[]
        {
            EmberTargetBodyPart.Head,
            EmberTargetBodyPart.Torso,
            //EmberTargetBodyPart.Groin,
            EmberTargetBodyPart.LeftArm,
            EmberTargetBodyPart.LeftHand,
            EmberTargetBodyPart.LeftLeg,
            EmberTargetBodyPart.LeftFoot,
            EmberTargetBodyPart.RightArm,
            EmberTargetBodyPart.RightHand,
            EmberTargetBodyPart.RightLeg,
            EmberTargetBodyPart.RightFoot,
        };

        return parts;
    }
}
