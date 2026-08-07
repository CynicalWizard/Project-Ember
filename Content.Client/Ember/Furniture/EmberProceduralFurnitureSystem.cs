using System.Linq;
using Content.Shared.Ember.Furniture;
using Content.Shared.Ember.Materials;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Ember.Furniture;

/// <summary>
/// Draws a piece of furniture out of its parts, each tinted by the material it is made of.
/// </summary>
/// <remarks>
/// Bay's layering, kept exactly. Under the sitter: the frame, and the upholstery over it. Above the sitter:
/// the back of the chair, the upholstery on that back, and — only while someone is actually sitting there —
/// the arms that close around them. The parts above are drawn by the companion entity, because one sprite
/// cannot straddle a mob; which half this is drawing is the component's <c>DrawsOver</c>.
///
/// A part the sheet does not have is simply not drawn, which is what makes one system serve a stool with two
/// states and a captain's chair with seven.
/// </remarks>
public sealed class EmberProceduralFurnitureSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralFurnitureComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberProceduralFurnitureComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnStartup(Entity<EmberProceduralFurnitureComponent> ent, ref ComponentStartup args)
    {
        Rebuild(ent);
    }

    private void OnStateHandled(Entity<EmberProceduralFurnitureComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Rebuild(ent);
    }

    private void Rebuild(Entity<EmberProceduralFurnitureComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        // This owns the whole sprite: the prototypes it replaces draw the same furniture as one flat picture,
        // and leaving that in means the chair is drawn twice.
        while (sprite.AllLayers.Any())
        {
            sprite.RemoveLayer(0);
        }

        var rsi = _cache.GetResource<RSIResource>(ent.Comp.Sprite).RSI;
        var frame = Colour(ent.Comp.Material);
        var padding = ent.Comp.Padding is { } id ? Colour(id) : (Color?) null;

        // A shuttle seat is drawn from a different set of parts once someone is strapped into it.
        var name = ent.Comp.Occupied && ent.Comp.OccupiedState is { } occupied
            ? occupied
            : ent.Comp.BaseState;

        if (ent.Comp.DrawsOver)
        {
            Add(sprite, rsi, ent.Comp, name, "_over", frame);
            Add(sprite, rsi, ent.Comp, name, "_padding_over", padding);

            // The arms close around an occupant and are drawn only when there is one to close around.
            if (ent.Comp.Occupied)
            {
                Add(sprite, rsi, ent.Comp, name, "_armrest", frame);
                Add(sprite, rsi, ent.Comp, name, "_padding_armrest", padding);
            }

            // Bay puts the trim above the sitter too, alongside the back of the chair.
            if (ent.Comp.Special && (ent.Comp.SpecialWhenOccupied || !ent.Comp.Occupied))
                Add(sprite, rsi, ent.Comp, name, "_special", ent.Comp.SpecialTinted ? frame : Color.White);

            return;
        }

        Add(sprite, rsi, ent.Comp, name, string.Empty, frame);
        Add(sprite, rsi, ent.Comp, name, "_padding", padding);
    }

    /// <summary>Adds one part, if the sheet has it and there is a colour to draw it in.</summary>
    private static void Add(
        SpriteComponent sprite,
        RSI rsi,
        EmberProceduralFurnitureComponent furniture,
        string name,
        string suffix,
        Color? color)
    {
        if (color is not { } tint)
            return;

        var state = name + suffix;
        if (!rsi.TryGetState(state, out _))
            return;

        var index = sprite.AddLayer(new SpriteSpecifier.Rsi(furniture.Sprite, state));
        sprite.LayerSetColor(index, tint);
    }

    private Color Colour(ProtoId<EmberMaterialPrototype> id)
    {
        return _prototype.TryIndex(id, out EmberMaterialPrototype? material) ? material.Color : Color.White;
    }
}
