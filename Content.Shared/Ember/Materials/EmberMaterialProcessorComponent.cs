using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Ember.Materials;

[RegisterComponent]
public sealed partial class EmberMineralMachineComponent : Component
{
    [DataField]
    public Direction? Input = Direction.West;

    [DataField]
    public Direction? Output = Direction.East;

    [DataField]
    public TimeSpan ProcessDelay = TimeSpan.FromSeconds(0.1);

    public TimeSpan NextProcess;
}

[RegisterComponent]
public sealed partial class EmberOreUnloaderComponent : Component
{
    [DataField]
    public int OrePerTick = 10;

    [DataField]
    public int MaxOutputContents = 15;
}

[RegisterComponent]
public sealed partial class EmberMaterialProcessorComponent : Component
{
    [DataField]
    public int SheetsPerTick = 10;

    [DataField]
    public bool Active;

    [DataField]
    public bool ReportAllOres;

    [DataField]
    public Dictionary<ProtoId<EmberMaterialPrototype>, int> StoredOres = new();

    [DataField]
    public Dictionary<ProtoId<EmberMaterialPrototype>, EmberMaterialProcessingMode> OreModes = new();
}

[RegisterComponent]
public sealed partial class EmberOreStackerComponent : Component
{
    [DataField]
    public int StackAmount = 50;

    [DataField]
    public Dictionary<ProtoId<EmberMaterialPrototype>, int> StoredMaterials = new();
}

[RegisterComponent]
public sealed partial class EmberOreProcessingConsoleComponent : Component
{
    [DataField]
    public EntityUid? Unloader;

    [DataField]
    public EntityUid? Processor;

    [DataField]
    public EntityUid? Stacker;
}

[RegisterComponent]
public sealed partial class EmberOreProcessingLinkerComponent : Component
{
    [DataField]
    public EntityUid? Console;

    [DataField]
    public EntityUid? Machine;
}

public enum EmberMaterialProcessingMode : byte
{
    Disabled = 0,
    Smelt = 1,
    Compress = 2,
    Alloy = 3,
}

[Serializable, NetSerializable]
public enum EmberOreMachineKind : byte
{
    Unloader,
    Processor,
    Stacker,
}

[Serializable, NetSerializable]
public enum EmberOreProcessorVisuals : byte
{
    Active,
}

[Serializable, NetSerializable]
public enum EmberOreProcessingConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class EmberOreProcessingConsoleState : BoundUserInterfaceState
{
    public EmberOreMachineConsoleState Unloader { get; }
    public EmberOreMachineConsoleState Processor { get; }
    public EmberOreMachineConsoleState Stacker { get; }

    public EmberOreProcessingConsoleState(
        EmberOreMachineConsoleState unloader,
        EmberOreMachineConsoleState processor,
        EmberOreMachineConsoleState stacker)
    {
        Unloader = unloader;
        Processor = processor;
        Stacker = stacker;
    }
}

[Serializable, NetSerializable]
public sealed class EmberOreMachineConsoleState
{
    public string? ConnectedName { get; }
    public NetEntity? Connected { get; }
    public Direction? Input { get; }
    public Direction? Output { get; }
    public bool ProcessorActive { get; }
    public int StackAmount { get; }
    public Dictionary<string, int> StoredOres { get; }
    public Dictionary<string, int> StoredMaterials { get; }
    public Dictionary<string, EmberMaterialProcessingMode> OreModes { get; }

    public EmberOreMachineConsoleState(
        string? connectedName,
        NetEntity? connected,
        Direction? input,
        Direction? output,
        bool processorActive,
        int stackAmount,
        Dictionary<string, int> storedOres,
        Dictionary<string, int> storedMaterials,
        Dictionary<string, EmberMaterialProcessingMode> oreModes)
    {
        ConnectedName = connectedName;
        Connected = connected;
        Input = input;
        Output = output;
        ProcessorActive = processorActive;
        StackAmount = stackAmount;
        StoredOres = storedOres;
        StoredMaterials = storedMaterials;
        OreModes = oreModes;
    }
}

[Serializable, NetSerializable]
public sealed class EmberOreConsoleRelinkMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class EmberOreConsoleSetDirectionMessage : BoundUserInterfaceMessage
{
    public EmberOreMachineKind Kind { get; }
    public bool Input { get; }
    public Direction? Direction { get; }

    public EmberOreConsoleSetDirectionMessage(EmberOreMachineKind kind, bool input, Direction? direction)
    {
        Kind = kind;
        Input = input;
        Direction = direction;
    }
}

[Serializable, NetSerializable]
public sealed class EmberOreConsoleToggleProcessorMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class EmberOreConsoleSetProcessorModeMessage : BoundUserInterfaceMessage
{
    public string Material { get; }
    public EmberMaterialProcessingMode Mode { get; }

    public EmberOreConsoleSetProcessorModeMessage(string material, EmberMaterialProcessingMode mode)
    {
        Material = material;
        Mode = mode;
    }
}

[Serializable, NetSerializable]
public sealed class EmberOreConsolePresetModesMessage : BoundUserInterfaceMessage
{
    public EmberOreConsolePreset Preset { get; }

    public EmberOreConsolePresetModesMessage(EmberOreConsolePreset preset)
    {
        Preset = preset;
    }
}

[Serializable, NetSerializable]
public sealed class EmberOreConsoleSetStackAmountMessage : BoundUserInterfaceMessage
{
    public int Amount { get; }

    public EmberOreConsoleSetStackAmountMessage(int amount)
    {
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public enum EmberOreConsolePreset : byte
{
    Automatic,
    Alloy,
    Disabled,
}
