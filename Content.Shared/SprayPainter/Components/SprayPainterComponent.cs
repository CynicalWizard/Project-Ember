using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.SprayPainter.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SprayPainterComponent : Component
{
    [DataField]
    public SoundSpecifier SpraySound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    [DataField]
    public TimeSpan AirlockSprayTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan PipeSprayTime = TimeSpan.FromSeconds(1);

    /// <summary>
    /// DoAfterId for airlock spraying.
    /// Pipes do not track doafters so you can spray multiple at once.
    /// </summary>
    [DataField]
    public DoAfterId? AirlockDoAfter;

    /// <summary>
    /// Pipe color chosen to spray with.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? PickedColor;

    [DataField, AutoNetworkedField]
    public bool PickedCustomColor;

    [DataField, AutoNetworkedField]
    public Color CustomColor = Color.White;

    /// <summary>
    /// Pipe colors that can be selected.
    /// </summary>
    [DataField]
    public Dictionary<string, Color> ColorPalette = new();

    /// <summary>
    /// Airlock style index selected.
    /// After prototype reload this might not be the same style but it will never be out of bounds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Index;

    [DataField, AutoNetworkedField]
    public SprayPainterWallMode WallMode = SprayPainterWallMode.PaintWall;

    [DataField, AutoNetworkedField]
    public SprayPainterAirlockMode AirlockMode = SprayPainterAirlockMode.ApplyStyle;

    /// <summary>
    /// Which closet or crate appearance is picked, as an index into the cached list of them.
    /// </summary>
    /// <remarks>
    /// A container is crafted plain and painted into whatever it is meant to be, which is why there is one
    /// recipe per shape rather than one per department. Same bookkeeping as the airlock index above: after a
    /// prototype reload the index may point at a different style, but never out of bounds.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public int ClosetIndex;
}
