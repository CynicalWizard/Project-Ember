using Content.Server.Atmos;
using Content.Server.Ember.Materials;
using Content.Shared.CCVar;
using Content.Shared.Ember.Materials;
using Content.Shared.Damage;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Bay has no single answer for what a thing melts at: <c>get_material_melting_point</c> is overridden wherever
/// something is made of more than one substance, and the overrides disagree on purpose. These pin the numbers
/// those overrides produce, because every one of them was silently wrong here at some point — a reinforced pane
/// melted at plain glass's hundred degrees, and a glass airlock at its window rather than its shell.
/// </summary>
[TestFixture]
public sealed class EmberMeltingPointTest
{
    private static readonly (string Prototype, float Expected, string Because)[] Cases =
    {
        // Steel is 1800 K, plasteel 6000, glass 373 — Bay's T0C + 100, which is why windows go first.
        ("WallSolid", 1800f, "a steel wall is steel"),
        ("Window", 373f, "glass melts at a hundred degrees on Bay and is meant to"),
        ("Girder", 1800f, "a girder is steel whatever gets built on it"),
        ("Firelock", 6000f, "Bay's firedoor answers plasteel"),

        // /turf/simulated/wall: melting_point + reinf_material.melting_point, added outright.
        ("WallReinforced", 7800f, "a wall counts its reinforcement in full"),

        // /obj/structure/window: . + 0.25 * reinf_material.melting_point. A reinforced pane is still glass.
        ("ReinforcedWindow", 823f, "a lattice is worth a quarter of itself"),

        // /obj/machinery/door/airlock: round((. + window_material.melting_point) / 2), and DM's round floors,
        // so the half of 2173 goes down rather than up.
        ("Airlock", 1800f, "a solid airlock is a steel shell"),
        ("AirlockGlass", 1086f, "a glass airlock averages its shell and its pane"),
    };

    /// <summary>
    /// The curve has a knee, not a ceiling.
    /// </summary>
    /// <remarks>
    /// The first attempt at this clamped the temperature, which made every material above the clamp immune to
    /// fire outright — a wall that shrugs off a million kelvin is no more defensible than one that dissolves in
    /// a candle. Bending the curve instead means damage keeps climbing for as long as the fire does, just far
    /// more slowly than a straight line would.
    ///
    /// Whether a given wall then feels it is a separate question with a separate answer: Bay's hardness floor
    /// ignores any hit under it, and osmium-carbide's is twenty six, so at the current knee it takes a fire
    /// beyond anything the station can produce before an OCP bulkhead registers one. That is the floor doing
    /// its job, not the knee acting as a ceiling, and these check the two apart.
    /// </remarks>
    [Test]
    public void TheCurveHasAKneeAndNotACeiling()
    {
        const float knee = 2000f;

        // Steel, because it starts feeling a fire early enough to watch the curve climb.
        var last = 0f;
        foreach (var temperature in new[] { 3_000f, 10_000f, 100_000f, 1_000_000f, 100_000_000f })
        {
            var damage = EmberMaterialHeat.Damage(EmberMaterialHeat.Effective(temperature, knee), 1_800f);

            Assert.That(damage, Is.GreaterThan(last),
                $"{temperature} K did no more to steel than the fire below it.");

            last = damage;
        }

        // And osmium-carbide, which a station fire never troubles, still gives way to something absurd. That
        // is the difference between a knee and a ceiling.
        Assert.That(EmberMaterialHeat.Damage(EmberMaterialHeat.Effective(67_816f, knee), 12_000f), Is.Zero,
            "A tritium flame got through osmium-carbide, so a burn chamber cannot be lined with it.");

        Assert.That(EmberMaterialHeat.Damage(EmberMaterialHeat.Effective(100_000_000f, knee), 12_000f),
            Is.GreaterThan(0f),
            "A hundred million kelvin left osmium-carbide untouched, so the knee is a ceiling after all.");

        // And below the knee it is Bay's arithmetic untouched, because at Bay's temperatures Bay was right.
        Assert.That(EmberMaterialHeat.Effective(1_500f, knee), Is.EqualTo(1_500f));
        Assert.That(EmberMaterialHeat.Damage(EmberMaterialHeat.Effective(1_900f, knee), 1_800f), Is.EqualTo(1f));
    }

    /// <summary>
    /// A steel bulkhead in the worst fire the station can make: it has to go, and it has to take long enough
    /// that someone could have done something about it.
    /// </summary>
    [Test]
    public async Task ASteelWallBurnsInATritiumFireButNotInstantly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var wall = entManager.SpawnEntity("WallSolid", map.GridCoords);
            var damage = entManager.GetComponent<DamageableComponent>(wall);

            // What EmberFireTemperatureTest measures a tritium flame settling at.
            var fire = new TileFireEvent(67_816f, 2500f);
            var hits = 0;
            for (var i = 0; i < 20_000 && damage.TotalDamage <= 0; i++)
            {
                entManager.EventBus.RaiseLocalEvent(wall, ref fire);
                hits++;
            }

            Assert.That((float) damage.TotalDamage, Is.GreaterThan(0f),
                "A tritium fire did nothing at all to a steel wall.");

            // A blow lands one tick in thirty, so this is the first of many rather than the end of the wall.
            Assert.That((float) damage.TotalDamage, Is.LessThan(100f),
                $"One tick of a tritium fire took {damage.TotalDamage} off a steel wall after {hits} ticks.");

            entManager.DeleteEntity(wall);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MeltingPointsFollowBaysFormulas()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var heat = server.System<EmberMaterialHeatSystem>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (prototype, expected, because) in Cases)
                {
                    var uid = entManager.SpawnEntity(prototype, map.GridCoords);

                    Assert.That(heat.MeltingPoint(uid), Is.EqualTo(expected).Within(0.01f),
                        $"{prototype}: {because}.");

                    entManager.DeleteEntity(uid);
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
