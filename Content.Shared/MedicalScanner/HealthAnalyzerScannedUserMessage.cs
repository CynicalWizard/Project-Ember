using Content.Shared.Ember.Medical.Targeting; // Shitmed Change
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
///     On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;
    public Dictionary<EmberTargetBodyPart, EmberTargetIntegrity>? Body; // Ember: body-part targeting
    public NetEntity? Part; // Shitmed Change
    public HealthAnalyzerScannedUserMessage(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, Dictionary<EmberTargetBodyPart, EmberTargetIntegrity>? body, NetEntity? part = null) // Ember: body-part targeting
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Body = body; // Shitmed Change
        Part = part; // Shitmed Change
        Unrevivable = unrevivable;
    }
}

// Shitmed Change Start
[Serializable, NetSerializable]
public sealed class HealthAnalyzerPartMessage(NetEntity? owner, EmberTargetBodyPart? bodyPart) : BoundUserInterfaceMessage
{
    public readonly NetEntity? Owner = owner;
    public readonly EmberTargetBodyPart? BodyPart = bodyPart;

}
// Shitmed Change End