using Content.Shared.Buckle.Components;
using Content.Shared.Ember.Furniture;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Ember.Furniture;

/// <summary>
/// Keeps each piece of furniture's companion — the parts that belong above whoever is sitting in it — alive,
/// in step and in the same place.
/// </summary>
/// <remarks>
/// A sprite has one draw depth for all of its layers, so the back of a chair cannot be above a mob while its
/// seat is below one. The second entity exists for that reason alone: no collision, no interaction, nothing
/// but a sprite riding at <see cref="Content.Shared.DrawDepth.DrawDepth.OverMobs"/>.
///
/// Vanilla solves the same problem by flipping the sitter behind the whole chair when it faces north, in a
/// piece of BuckleSystem whose own comment calls it cursed. That only works from one side; this works from
/// four, which is what the art was drawn for.
/// </remarks>
public sealed class EmberProceduralFurnitureSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralFurnitureComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<EmberProceduralFurnitureComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EmberProceduralFurnitureComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<EmberProceduralFurnitureComponent, UnstrappedEvent>(OnUnstrapped);
    }

    private void OnMapInit(Entity<EmberProceduralFurnitureComponent> ent, ref MapInitEvent args)
    {
        // The companion is furniture too, and would otherwise want a companion of its own.
        if (ent.Comp.DrawsOver)
            return;

        ent.Comp.Overlay = Spawn(ent.Comp.OverlayPrototype, Transform(ent).Coordinates);
        _transform.SetParent(ent.Comp.Overlay.Value, ent);

        Sync(ent);
    }

    private void OnShutdown(Entity<EmberProceduralFurnitureComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Overlay is { } overlay && !TerminatingOrDeleted(overlay))
            QueueDel(overlay);

        ent.Comp.Overlay = null;
    }

    private void OnStrapped(Entity<EmberProceduralFurnitureComponent> ent, ref StrappedEvent args)
    {
        SetOccupied(ent, true);
    }

    private void OnUnstrapped(Entity<EmberProceduralFurnitureComponent> ent, ref UnstrappedEvent args)
    {
        // Fired before the entity leaves the list, so anyone else still sitting there keeps the arms closed.
        SetOccupied(ent, TryComp<StrapComponent>(ent, out var strap) && strap.BuckledEntities.Count > 1);
    }

    private void SetOccupied(Entity<EmberProceduralFurnitureComponent> ent, bool occupied)
    {
        if (ent.Comp.Occupied == occupied)
            return;

        ent.Comp.Occupied = occupied;
        Dirty(ent);
        Sync(ent);
    }

    /// <summary>
    /// Copies what the companion needs to draw itself. It is a separate entity, so nothing reaches the chair
    /// from it — repainting or reupholstering has to be pushed across.
    /// </summary>
    public void Sync(Entity<EmberProceduralFurnitureComponent> ent)
    {
        if (ent.Comp.Overlay is not { } overlay ||
            !TryComp<EmberProceduralFurnitureComponent>(overlay, out var over))
        {
            return;
        }

        over.BaseState = ent.Comp.BaseState;
        over.Sprite = ent.Comp.Sprite;
        over.Material = ent.Comp.Material;
        over.Padding = ent.Comp.Padding;
        over.Special = ent.Comp.Special;
        over.SpecialTinted = ent.Comp.SpecialTinted;
        over.SpecialWhenOccupied = ent.Comp.SpecialWhenOccupied;
        over.OccupiedState = ent.Comp.OccupiedState;
        over.Occupied = ent.Comp.Occupied;
        Dirty(overlay, over);
    }
}
