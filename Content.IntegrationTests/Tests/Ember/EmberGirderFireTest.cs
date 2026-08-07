using Content.Server.Atmos;
using Content.Shared.Damage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Fire damage has to survive the armour standing between it and the thing burning.
/// </summary>
/// <remarks>
/// Bay charges its whole toll once every two seconds; ours is called thirty times as often, and handing over a
/// thirtieth of the toll each time was silently equivalent to handing over nothing, because every flat armour
/// reduction in the game is bigger than a thirtieth of Bay's damage. Nothing built of structural metal was
/// taking any fire damage at all. These burn the real prototypes rather than reasoning about the formula.
/// </remarks>
[TestFixture]
public sealed class EmberGirderFireTest
{
    /// <summary>
    /// A girder never said what it was made of — one prototype serves every wall material — so a fire had
    /// nothing to compare itself against. It is steel, the way Bay's is: fixed, uncoloured, and the sheet you
    /// get back when it comes apart.
    /// </summary>
    [Test]
    public async Task AGirderBurnsAtSteelsMeltingPoint()
    {
        await using var pair = await PoolManager.GetServerClient();
        await AssertBurns(pair, "Girder");
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A steel wall ignores anything under Bay's hardness floor, sixteen points for steel, so it wants a fire
    /// well past its melting point before it feels anything: 1800 K to start melting and another 1600 K worth
    /// of damage to clear the floor. That is why it takes a tritium fire to bring one down and why an ordinary
    /// one leaves the hull alone.
    /// </summary>
    [Test]
    public async Task AWallBurnsThroughItsArmour()
    {
        await using var pair = await PoolManager.GetServerClient();
        await AssertBurns(pair, "WallSolid", 8000f);
        await pair.CleanReturnAsync();
    }

    private static async Task AssertBurns(Pair.TestPair pair, string prototype, float temperature = 8000f)
    {
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var uid = entManager.SpawnEntity(prototype, map.GridCoords);
            var damage = entManager.GetComponent<DamageableComponent>(uid);

            // Steel melts at 1800 K, so a fire below that wears it down not at all.
            var warm = new TileFireEvent(1000f, 2500f);
            entManager.EventBus.RaiseLocalEvent(uid, ref warm);

            Assert.That((float) damage.TotalDamage, Is.Zero,
                $"A fire cooler than steel damaged {prototype}.");

            // Eight thousand kelvin rather than three: the damage curve bends above two thousand, so a fire
            // has to be a real one before it gets past steel's armour at all.
            //
            // A tick is Bay's whole blow one time in thirty rather than a thirtieth of it every time, so this
            // waits for the first one to land. Two thousand chances at one in thirty leaves no room for luck,
            // and stopping at the first keeps the thing alive to be measured.
            var hot = new TileFireEvent(temperature, 2500f);
            for (var i = 0; i < 2000 && damage.TotalDamage <= 0; i++)
            {
                entManager.EventBus.RaiseLocalEvent(uid, ref hot);
            }

            Assert.That((float) damage.TotalDamage, Is.GreaterThan(0f),
                $"A fire hotter than steel left {prototype} untouched.");

            entManager.DeleteEntity(uid);
        });
    }
}
