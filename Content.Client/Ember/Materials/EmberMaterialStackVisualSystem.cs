using Content.Client.Items.Systems;
using Content.Shared.Ember.Materials;
using Content.Shared.Hands;

using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Ember.Materials;

/// <summary>
/// Colours a stack of material by what it is made of, both where it lies and in the hand carrying it.
/// </summary>
/// <remarks>
/// Bay does this with one line — <c>color = material.icon_colour</c> in <c>on_update_icon</c> — which takes the
/// held icon along with the object, since its in-hand states are drawn plain and only ever tinted. Ours are the
/// same art, divided through by steel so that a steel sheet still looks exactly as it always has and every other
/// metal moves off it.
/// </remarks>
public sealed class EmberMaterialStackVisualSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberMaterialStackComponent, ComponentStartup>(OnStartup);

        // ItemSystem is what puts the layers there in the first place, so there is nothing to colour until it has.
        SubscribeLocalEvent<EmberMaterialStackComponent, GetInhandVisualsEvent>(
            OnGetInhandVisuals,
            after: new[] { typeof(ItemSystem) });
    }

    private void OnStartup(EntityUid uid, EmberMaterialStackComponent component, ComponentStartup args)
    {
        if (!component.Tint ||
            !TryComp<SpriteComponent>(uid, out var sprite) ||
            !TryGetColor(component, out var color))
        {
            return;
        }

        sprite.LayerSetColor(0, color);
    }

    private void OnGetInhandVisuals(
        EntityUid uid,
        EmberMaterialStackComponent component,
        GetInhandVisualsEvent args)
    {
        if (!component.Tint || !TryGetColor(component, out var color))
            return;

        foreach (var (_, layer) in args.Layers)
        {
            // Anything that arrived already coloured asked for that colour on purpose. A material stack never
            // does: it has only the layer built from its held prefix, which is made fresh for each request.
            layer.Color ??= color;
        }
    }

    private bool TryGetColor(EmberMaterialStackComponent component, out Color color)
    {
        color = default;

        if (!_prototype.TryIndex(component.Material, out EmberMaterialPrototype? material))
            return false;

        color = material.Color;
        return true;
    }
}
