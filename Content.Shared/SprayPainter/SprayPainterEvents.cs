using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.SprayPainter;

[Serializable, NetSerializable]
public enum SprayPainterUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SprayPainterSpritePickedMessage : BoundUserInterfaceMessage
{
    public readonly int Index;

    public SprayPainterSpritePickedMessage(int index)
    {
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class SprayPainterColorPickedMessage : BoundUserInterfaceMessage
{
    public readonly string? Key;

    public SprayPainterColorPickedMessage(string? key)
    {
        Key = key;
    }
}

[Serializable, NetSerializable]
public sealed class SprayPainterCustomColorPickedMessage : BoundUserInterfaceMessage
{
    public readonly Color Color;

    public SprayPainterCustomColorPickedMessage(Color color)
    {
        Color = color;
    }
}

[Serializable, NetSerializable]
public sealed class SprayPainterWallModePickedMessage : BoundUserInterfaceMessage
{
    public readonly SprayPainterWallMode Mode;

    public SprayPainterWallModePickedMessage(SprayPainterWallMode mode)
    {
        Mode = mode;
    }
}

[Serializable, NetSerializable]
public sealed class SprayPainterAirlockModePickedMessage : BoundUserInterfaceMessage
{
    public readonly SprayPainterAirlockMode Mode;

    public SprayPainterAirlockModePickedMessage(SprayPainterAirlockMode mode)
    {
        Mode = mode;
    }
}

[Serializable, NetSerializable]
public sealed partial class SprayPainterDoorDoAfterEvent : DoAfterEvent
{
    /// <summary>
    /// Base RSI path to set for the door sprite.
    /// </summary>
    [DataField]
    public string Sprite;

    /// <summary>
    /// Department id to set for the door, if the style has one.
    /// </summary>
    [DataField]
    public string? Department;

    /// <summary>
    /// Spray painter style name selected in the UI.
    /// </summary>
    [DataField]
    public string StyleName;

    [DataField]
    public SprayPainterAirlockMode Mode;

    [DataField]
    public Color? Color;

    public SprayPainterDoorDoAfterEvent(
        string sprite,
        string? department,
        string styleName,
        SprayPainterAirlockMode mode = SprayPainterAirlockMode.ApplyStyle,
        Color? color = null)
    {
        Sprite = sprite;
        Department = department;
        StyleName = styleName;
        Mode = mode;
        Color = color;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class SprayPainterPipeDoAfterEvent : DoAfterEvent
{
    /// <summary>
    /// Color of the pipe to set.
    /// </summary>
    [DataField]
    public Color Color;

    public SprayPainterPipeDoAfterEvent(Color color)
    {
        Color = color;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class SprayPainterWallDoAfterEvent : DoAfterEvent
{
    [DataField]
    public SprayPainterWallMode Mode;

    [DataField]
    public Color? Color;

    public SprayPainterWallDoAfterEvent(SprayPainterWallMode mode, Color? color)
    {
        Mode = mode;
        Color = color;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed class SprayPainterClosetPickedMessage : BoundUserInterfaceMessage
{
    public readonly int Index;

    public SprayPainterClosetPickedMessage(int index)
    {
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed partial class SprayPainterClosetDoAfterEvent : DoAfterEvent
{
    /// <summary>The appearance to give the container.</summary>
    [DataField]
    public string Style;

    /// <summary>A colour of the user's own, which overrides the one the appearance carries.</summary>
    [DataField]
    public Color? Color;

    public SprayPainterClosetDoAfterEvent(string style, Color? color)
    {
        Style = style;
        Color = color;
    }

    public override DoAfterEvent Clone() => this;
}
