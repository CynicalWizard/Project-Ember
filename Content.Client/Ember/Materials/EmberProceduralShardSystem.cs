using Content.Shared.Ember.Materials;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Ember.Materials;

/// <summary>
/// Draws debris in the shape its material breaks into and tints it to match.
/// </summary>
public sealed class EmberProceduralShardSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralShardComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberProceduralShardComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnStartup(Entity<EmberProceduralShardComponent> ent, ref ComponentStartup args)
    {
        Apply(ent);
    }

    private void OnHandleState(Entity<EmberProceduralShardComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Apply(ent);
    }

    private void Apply(Entity<EmberProceduralShardComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) ||
            !_prototype.TryIndex(ent.Comp.Material, out EmberMaterialPrototype? material) ||
            EmberShardTypes.GetIconBase(material.ShardType) is not { } iconBase)
        {
            return;
        }

        var size = EmberShardTypes.Sizes[Math.Clamp(ent.Comp.Size, 0, EmberShardTypes.Sizes.Length - 1)];

        sprite.LayerSetSprite(0, new SpriteSpecifier.Rsi(ent.Comp.Sprite, iconBase + size));
        sprite.Color = material.Color.WithAlpha(GetAlpha(material));
    }

    /// <summary>
    /// Bay's <c>1 - (1 - opacity)²</c>, so glass at 0.3 opacity comes out around half visible rather than
    /// almost invisible.
    /// </summary>
    private static float GetAlpha(EmberMaterialPrototype material)
    {
        var inverse = 1f - material.Opacity;

        return 1f - inverse * inverse;
    }
}
