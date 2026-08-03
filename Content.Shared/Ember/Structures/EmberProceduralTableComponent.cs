using Content.Shared.Ember.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Structures;

/// <summary>
/// A Bay table: a frame, whatever it is plated with, whatever that plating is reinforced with, and felt on top if
/// it is a gambling table. Each of those is a separate set of corner sprites, so one entity covers every table in
/// the game rather than one prototype per material.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class EmberProceduralTableComponent : Component
{
    public (EntityUid?, Vector2i)? LastPosition;

    /// <summary>The depth it draws at standing up, remembered so lying down can hand it back.</summary>
    public int? UprightDrawDepth;

    public int UpdateGeneration;

    /// <summary>What the table is plated with. Nothing means a bare frame, which joins onto no other table.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EmberMaterialPrototype>? Material;

    /// <summary>What the plating is reinforced with. Bay will not flip a reinforced table.</summary>
    [DataField, AutoNetworkedField]
    public ProtoId<EmberMaterialPrototype>? Reinforcement;

    /// <summary>Felt over the plating, which is all a gambling table is.</summary>
    [DataField, AutoNetworkedField]
    public bool Carpeted;

    /// <summary>On its side, as cover, rather than standing on its legs.</summary>
    [DataField, AutoNetworkedField]
    public bool Flipped;

    /// <summary>
    /// Which edge of its own tile a flipped table lies along.
    /// </summary>
    /// <remarks>
    /// Carried as a direction of its own rather than as the entity's rotation. Both the lip it blocks with and
    /// the sprite it draws are worked out from this one networked value, so the two cannot end up a quarter or a
    /// half turn apart from each other, which is what an unrotated client and a rotated server produced.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public Direction FlipFacing = Direction.South;

    /// <summary>
    /// The shape it stood in before being tipped over, remembered so that standing it back up restores exactly
    /// what the prototype gave it rather than a second copy of those numbers kept in step by hand. Only a table
    /// that was already lying down when the map loaded falls back on <see cref="UprightBounds"/>.
    /// </summary>
    [ViewVariables]
    public IPhysShape? UprightShape;

    /// <summary>What it collided with standing up, remembered for the same reason as the shape.</summary>
    [ViewVariables]
    public int? UprightLayer;

    /// <summary>What a table stands in when nothing remembers what it stood in before.</summary>
    [DataField]
    public Box2 UprightBounds = new(-0.45f, -0.45f, 0.45f, 0.45f);

    /// <summary>A lip along the edge it faces, so you can shelter behind it and shoot over it.</summary>
    [DataField]
    public Box2 FlippedBounds = new(-0.5f, -0.5f, 0.5f, -0.28125f);

    [DataField]
    public ResPath Sprite = new("/Textures/Ember/Structures/Tables/tables_offbay.rsi");
}
