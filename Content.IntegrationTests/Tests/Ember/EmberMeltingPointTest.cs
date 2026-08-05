using Content.Server.Atmos;
using Content.Server.Ember.Materials;
using Content.Shared.CCVar;
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
    /// Bay's damage curve is calibrated to Bay's fires, which are deliberately cool -- its turf fire releases
    /// 50 kJ per burnt mole of oxygen and tops out at 1023 K. Ours releases 284 kJ per mole of hydrogen and a
    /// tritium fire runs an order of magnitude hotter than anything Bay sees, which turned the same formula
    /// into hundreds of points a second. The cap is where the two are pinned together; these are the numbers
    /// it produces for the things that were reported dissolving.
    /// </summary>
    [Test]
    public async Task AFireCannotCountAsHotterThanTheCap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var config = server.ResolveDependency<IConfigurationManager>();
        var map = await pair.CreateTestMap();

        var cap = config.GetCVar(CCVars.EmberFireMaterialTemperatureCap);

        await server.WaitPost(() =>
        {
            // A low wall of osmium-carbide plasteel: 12000 K, well past anything the cap admits.
            var low = entManager.SpawnEntity("WallFrameOCP", map.GridCoords);
            var damage = entManager.GetComponent<DamageableComponent>(low);

            var inferno = new TileFireEvent(1_000_000f, 2500f);
            for (var i = 0; i < 2000; i++)
            {
                entManager.EventBus.RaiseLocalEvent(low, ref inferno);
            }

            Assert.That((float) damage.TotalDamage, Is.Zero,
                $"A fire capped at {cap} K melted a low wall that gives way at 12000.");

            entManager.DeleteEntity(low);

            // Borosilicate melts at 4273, so the capped fire is worth seven points against a floor of seven.
            var window = entManager.SpawnEntity("ReinforcedBorosilicateWindow", map.GridCoords);
            var pane = entManager.GetComponent<DamageableComponent>(window);

            for (var i = 0; i < 2000; i++)
            {
                entManager.EventBus.RaiseLocalEvent(window, ref inferno);
            }

            Assert.That((float) pane.TotalDamage, Is.Zero,
                "A borosilicate pane shattered in a fire it is supposed to stand in.");

            entManager.DeleteEntity(window);
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
