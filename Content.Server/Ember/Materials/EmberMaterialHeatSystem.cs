using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server.Ember.Materials;

/// <summary>
/// Burns things standing in a fire hotter than the material they are made of can take.
/// </summary>
/// <remarks>
/// Bay's <c>fire_act</c>: a point of damage for every hundred kelvin the fire runs above the melting point,
/// never less than one, and a wall counts its reinforcement towards the total. That is what makes a plasteel
/// wall worth building and a wooden table a bad place to keep a welder. Tiles are deliberately left out --
/// a tile has nowhere to keep a state, so it cannot take damage in the first place.
/// </remarks>
public sealed class EmberMaterialHeatSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";

    public override void Initialize()
    {
        base.Initialize();

        // One subscription per component that can name a material, since that is the only thing they share.
        // Both doors go through the adjacent event as well: a shut airlock seals its tile just like a wall.
        SubscribeLocalEvent<EmberProceduralWallComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberProceduralStructureComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberProceduralTableComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberProceduralMaterialDoorComponent, TileFireEvent>(OnTileFire);

        SubscribeLocalEvent<EmberProceduralWallComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
        SubscribeLocalEvent<EmberProceduralStructureComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
        SubscribeLocalEvent<EmberProceduralMaterialDoorComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
    }

    private void OnTileFire<T>(Entity<T> ent, ref TileFireEvent args) where T : IComponent
    {
        Burn(ent, args.Temperature);
    }

    /// <summary>
    /// A fire next door. Only the things that seal their own tile hear this, because they are the ones a fire
    /// can never start on; a table standing in the flames is burnt by <see cref="TileFireEvent"/> instead and
    /// must not be burnt twice.
    /// </summary>
    private void OnAdjacentTileFire<T>(Entity<T> ent, ref AdjacentTileFireEvent args) where T : IComponent
    {
        if (!HasComp<AirtightComponent>(ent))
            return;

        Burn(ent, args.Temperature);
    }

    private void Burn(EntityUid uid, float temperature)
    {
        if (!HasComp<DamageableComponent>(uid))
            return;

        if (MeltingPoint(uid) is not { } melting)
            return;

        var damage = EmberMaterialHeat.Damage(temperature, melting);

        if (damage <= 0f)
            return;

        _damageable.TryChangeDamage(
            uid,
            new DamageSpecifier(_prototype.Index(Heat), FixedPoint2.New(damage)),
            ignoreResistances: false);
    }

    /// <summary>
    /// The temperature this thing starts suffering at, or null if nothing about it can melt.
    /// </summary>
    /// <remarks>
    /// Bay adds a wall's reinforcement to its melting point rather than taking the higher of the two, so a
    /// reinforced wall stands in a fire that would have taken down either material on its own. Everything else
    /// it measures by the one material it calls its own, which for a table is the plating rather than the frame
    /// underneath — a wooden table burns like wood however the frame is made. Taking the lowest of the parts
    /// says the same thing without having to ask what kind of thing this is.
    /// </remarks>
    private float? MeltingPoint(EntityUid uid)
    {
        var wall = HasComp<EmberProceduralWallComponent>(uid);
        float? total = null;

        foreach (var id in EmberMaterialLookup.Materials(EntityManager, _prototype, uid))
        {
            if (!_prototype.TryIndex(id, out EmberMaterialPrototype? material))
                continue;

            if (material.Unmeltable)
                return null;

            total = total is not { } current
                ? material.MeltingPoint
                : wall
                    ? current + material.MeltingPoint
                    : MathF.Min(current, material.MeltingPoint);
        }

        return total;
    }
}
