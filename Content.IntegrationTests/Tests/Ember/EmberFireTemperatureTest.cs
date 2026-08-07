using System;
using System.Collections.Generic;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Ember.Materials;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// How hot our fires actually get, and what the material damage curve makes of that.
/// </summary>
/// <remarks>
/// Every balance argument about fire has been fought with guesses about the temperature, so this measures it
/// instead. Bay's numbers to compare against: its turf fire releases 50 kJ per burnt mole of oxygen and tops
/// out at 1023 K, and the damage curve — a point per hundred kelvin over the melting point — was written for
/// that. Ours releases 284 kJ per mole of hydrogen.
///
/// The results are printed as well as asserted, because the number a burn chamber has to be built for is the
/// point of running this at all. Two things they show. A tritium flame settles at much the same temperature
/// whatever its size -- both sides of the reaction scale together, so five moles burn as hot as two canisters
/// -- which means there is no gentle setting for a burn chamber to be built around, only a lining to choose.
/// And that temperature is some seventy times Bay's hottest fire, which is why the curve needed a knee.
///
/// This burns the mixture in isolation, so it measures the chemistry and not a tile: nothing here conducts
/// into the floor, spreads to a neighbour or radiates away, and a real hotspot will therefore run cooler than
/// these figures. Read them as the ceiling the chemistry allows rather than as what a thermometer in the room
/// would say.
/// </remarks>
[TestFixture]
public sealed class EmberFireTemperatureTest
{
    /// <summary>A tile of air, which is what a hotspot burns at once.</summary>
    private const float TileVolume = 2500f;

    /// <summary>The room in the report: three by three.</summary>
    private const float RoomVolume = TileVolume * 9f;

    // Straight off the canister prototypes.
    private const float TritiumCanisterMoles = 1871.71051f;
    private const float OxygenCanisterMoles = 1871.71051f;
    private const float LiquidOxygenCanisterMoles = 18710.71051f;

    [Test]
    public async Task ReportWhatOurFiresReach()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var atmos = server.System<AtmosphereSystem>();
        var config = server.ResolveDependency<IConfigurationManager>();
        var knee = config.GetCVar(CCVars.EmberFireTemperatureKnee);

        var cases = new (string Name, float Volume, float Tritium, float Oxygen)[]
        {
            // What the report describes: a tritium canister mixed with a liquid oxygen one, in a three by
            // three cell. Liquid oxygen is ten times the moles of the ordinary canister beside it.
            ("tritium + LOX, 3x3", RoomVolume, TritiumCanisterMoles, LiquidOxygenCanisterMoles),

            // The same gas in one tile, because a hotspot burns the tile it is on rather than the room.
            ("tritium + LOX, one tile", TileVolume, TritiumCanisterMoles, LiquidOxygenCanisterMoles),

            // Ordinary oxygen for comparison, so the difference the liquid makes is visible.
            ("tritium + oxygen, 3x3", RoomVolume, TritiumCanisterMoles, OxygenCanisterMoles),

            // A burn chamber running normally: a modest, oxygen-rich trickle rather than a bomb.
            ("burn chamber, lean mix", TileVolume, 5f, 50f),
            ("burn chamber, rich mix", TileVolume, 50f, 500f),
        };

        var report = new List<string>();
        float roomPeak = 0f;

        await server.WaitPost(() =>
        {
            foreach (var (name, volume, tritium, oxygen) in cases)
            {
                // Lit rather than left cold: the reaction needs a spark, and 373 K is the temperature a
                // hotspot has to reach before a fire exists at all.
                var mixture = new GasMixture(volume)
                {
                    Temperature = Atmospherics.FireMinimumTemperatureToExist + 1f,
                };
                mixture.SetMoles(Gas.Tritium, tritium);
                mixture.SetMoles(Gas.Oxygen, oxygen);

                // Run it until it stops reacting; the first pass rarely burns everything.
                var peak = mixture.Temperature;
                for (var i = 0; i < 100 && mixture.GetMoles(Gas.Tritium) > 0f; i++)
                {
                    atmos.React(mixture, null);
                    peak = MathF.Max(peak, mixture.Temperature);
                }

                if (name == cases[0].Name)
                    roomPeak = peak;

                // What the damage curve makes of it, before and after the knee.
                var effective = EmberMaterialHeat.Effective(peak, knee);
                report.Add(
                    $"{name,-28} {peak,12:N0} K -> counts as {effective,10:N0} K | " +
                    $"steel {EmberMaterialHeat.Damage(effective, 1800f),6:N0}, " +
                    $"plasteel {EmberMaterialHeat.Damage(effective, 6000f),6:N0}, " +
                    $"OCP {EmberMaterialHeat.Damage(effective, 12000f),6:N0} per 2s");
            }
        });

        await pair.CleanReturnAsync();

        var text = $"knee = {knee:N0} K\n" + string.Join("\n", report);
        TestContext.Progress.WriteLine(text);
        System.IO.File.WriteAllText("fire_temperature_report.txt", text);

        // Not a fixed number — the reaction is not ours to pin — but the shape of the answer is the whole
        // point. If a pair of canisters ever stops being far hotter than Bay's hottest fire, the curve should
        // be revisited rather than the test quietly loosened.
        Assert.That(roomPeak, Is.GreaterThan(1023f),
            "A pair of canisters no longer beats Bay's hottest fire, so the knee may be solving nothing.");
    }
}
