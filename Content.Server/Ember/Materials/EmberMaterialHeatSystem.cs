using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
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
    [Dependency] private readonly IConfigurationManager _config = default!;
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

    /// <summary>What Bay's <c>/obj/structure/window/get_material_melting_point</c> gives a reinforcement.</summary>
    private const float ReinforcementShare = 0.25f;

    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";

    /// <summary>
    /// See <see cref="CCVars.EmberFireTemperatureKnee"/>: where Bay's damage curve stops being taken literally.
    /// </summary>
    private float _temperatureKnee;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, CCVars.EmberFireTemperatureKnee, value => _temperatureKnee = value, true);

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

        var damage = EmberMaterialHeat.Damage(EmberMaterialHeat.Effective(temperature, _temperatureKnee), melting);

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
    ///
    /// This one reads the real temperature rather than the capped one: catching fire is a threshold and not a
    /// curve, so a fire hotter than anything Bay ever saw still lights wood, it just does not eat steel.
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
    /// Bay does not have one answer to this: <c>get_material_melting_point</c> is overridden wherever a thing
    /// is made of more than one substance, and the overrides disagree on purpose. A wall adds its
    /// reinforcement, so a plasteel-backed bulkhead stands in a fire neither material would survive alone. A
    /// glass airlock averages its shell and its pane, so the window is a weakness without being the whole
    /// story. Everything else is measured by the one material it calls its own, which for a table is the
    /// plating and not the frame underneath — a wooden table burns like wood however it is framed — and taking
    /// the lowest of the parts says that without having to ask what kind of thing this is.
    /// </remarks>
    public float? MeltingPoint(EntityUid uid)
    {
        var rule = Rule(uid);
        float? total = null;
        var parts = 0;

        foreach (var id in EmberMaterialLookup.Materials(EntityManager, _prototype, uid))
        {
            if (!_prototype.TryIndex(id, out EmberMaterialPrototype? material))
                continue;

            if (material.Unmeltable)
                return null;

            parts++;
            total = total is not { } current
                ? material.MeltingPoint
                : rule == MeltingRule.Lowest
                    ? MathF.Min(current, material.MeltingPoint)
                    : current + material.MeltingPoint;
        }

        if (total is not { } point)
            return null;

        // DM's single-argument round floors, which is not what .NET's Round does with a half.
        if (rule == MeltingRule.Mean)
            point = MathF.Floor(point / parts);

        // A lattice set into a pane is worth a quarter of itself, and no more: the pane is still glass.
        if (TryComp<EmberMaterialReinforcementComponent>(uid, out var reinforcement) &&
            _prototype.TryIndex(reinforcement.Material, out EmberMaterialPrototype? lattice) &&
            !lattice.Unmeltable)
        {
            point += ReinforcementShare * lattice.MeltingPoint;
        }

        return point;
    }

    /// <summary>Which of Bay's overrides applies to this thing.</summary>
    private MeltingRule Rule(EntityUid uid)
    {
        if (HasComp<EmberProceduralWallComponent>(uid))
            return MeltingRule.Sum;

        if (HasComp<EmberProceduralAirlockComponent>(uid))
            return MeltingRule.Mean;

        return MeltingRule.Lowest;
    }

    /// <summary>How the parts of a thing add up to the temperature it gives way at.</summary>
    private enum MeltingRule : byte
    {
        /// <summary>The weakest part decides, which is what Bay means by asking for one material.</summary>
        Lowest,

        /// <summary>A wall: material plus reinforcement.</summary>
        Sum,

        /// <summary>A glass airlock: the shell and the pane, halved.</summary>
        Mean,
    }
}
