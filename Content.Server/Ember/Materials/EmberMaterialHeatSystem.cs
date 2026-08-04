using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Ember.Materials;

/// <summary>
/// Burns things standing in a fire hotter than the material they are made of can take, and sets alight the
/// ones that catch rather than melt.
/// </summary>
/// <remarks>
/// Bay's <c>fire_act</c>: a point of damage for every hundred kelvin the fire runs above the melting point,
/// never less than one, and a wall counts its reinforcement towards the total. That is what makes a plasteel
/// wall worth building and a wooden table a bad place to keep a welder. Tiles are deliberately left out --
/// a tile has nowhere to keep a state, so it cannot take damage in the first place.
/// </remarks>
public sealed class EmberMaterialHeatSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// How often Bay runs a fire, in seconds: its air subsystem takes the default twenty deciseconds and
    /// never overrides it.
    /// </summary>
    /// <remarks>
    /// Ours runs at the atmos tick rate, fifteen times a second by default, so the same numbers land thirty
    /// times as often. Left unscaled, a tritium fire put four hundred points into a steel wall in under half
    /// a second and took out everything within twenty tiles before anyone could react to it. What we do with
    /// this is decide how often a tick counts, not how much it is worth — see <see cref="Burn"/>.
    /// </remarks>
    private const float BayFireInterval = 2f;

    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";

    public override void Initialize()
    {
        base.Initialize();

        // One subscription per component that can name a material, since that is the only thing they share.
        SubscribeLocalEvent<EmberProceduralWallComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberProceduralStructureComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberMaterialTintComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberProceduralTableComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberProceduralAirlockComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberProceduralMaterialDoorComponent, TileFireEvent>(OnTileFire);
        SubscribeLocalEvent<EmberMaterialCompositionComponent, TileFireEvent>(OnTileFire);

        // Anything that seals its own tile can never have a fire on it, so it hears about the one next door.
        SubscribeLocalEvent<EmberProceduralWallComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
        SubscribeLocalEvent<EmberProceduralStructureComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
        SubscribeLocalEvent<EmberMaterialTintComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
        SubscribeLocalEvent<EmberProceduralAirlockComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
        SubscribeLocalEvent<EmberProceduralMaterialDoorComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
        SubscribeLocalEvent<EmberMaterialCompositionComponent, AdjacentTileFireEvent>(OnAdjacentTileFire);
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
        if (Ignites(uid, temperature))
            _flammable.AdjustFireStacks(uid, 1f, ignite: true);

        if (!HasComp<DamageableComponent>(uid) || MeltingPoint(uid) is not { } melting)
            return;

        var damage = EmberMaterialHeat.Damage(temperature, melting);

        if (damage <= 0f)
            return;

        // Bay charges its whole toll once every two seconds, and we are called thirty times in those two
        // seconds. Handing over a thirtieth of it each time looks equivalent and is not: armour is a flat
        // subtraction taken per blow, every flat reduction in the game is larger than a thirtieth of Bay's
        // damage, and so sliced fire damage was being rounded away to nothing on everything that has any.
        // Landing the whole blow a thirtieth of the time keeps Bay's rate and lets armour work as written.
        if (!_random.Prob(1f / (_atmosphere.AtmosTickRate * BayFireInterval)))
            return;

        _damageable.TryChangeDamage(
            uid,
            new DamageSpecifier(_prototype.Index(Heat), FixedPoint2.New(damage)),
            ignoreResistances: false);
    }

    /// <summary>
    /// Whether a fire this hot sets the thing alight rather than merely wearing it down.
    /// </summary>
    /// <remarks>
    /// Only some materials have an ignition point at all — wood, cloth, carpet, cardboard, vox resin — and for
    /// those it sits below the melting point, so they catch before they soften. Anything already burning is
    /// left alone rather than being fed a fresh stack every tick.
    /// </remarks>
    private bool Ignites(EntityUid uid, float temperature)
    {
        if (!TryComp<FlammableComponent>(uid, out var flammable) || flammable.OnFire)
            return false;

        foreach (var id in EmberMaterialLookup.Materials(EntityManager, _prototype, uid))
        {
            if (_prototype.TryIndex(id, out EmberMaterialPrototype? material) &&
                material.IgnitionPoint is { } ignition &&
                temperature >= ignition)
            {
                return true;
            }
        }

        return false;
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
