using System.Collections.Generic;
using System.Linq;
using EmberDrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Content.Shared.Ember.Furniture;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Furniture is drawn by two entities at once, and the interesting part is the seam between them.
/// </summary>
/// <remarks>
/// A sprite has one draw depth for all of its layers, so the back of a chair cannot be above a mob while its
/// seat is below one. The seat and its upholstery are drawn by the chair; the back, and the arms that close
/// around whoever is sitting there, by a companion riding at OverMobs. Nothing about that is visible from the
/// prototypes, which is exactly why it wants a test.
/// </remarks>
[TestFixture]
public sealed class EmberFurnitureTest
{
    [Test]
    public async Task AChairIsDrawnInTwoHalvesAcrossTwoEntities()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var furniture = server.System<Content.Server.Ember.Furniture.EmberProceduralFurnitureSystem>();
        var map = await pair.CreateTestMap();

        EntityUid chair = default;
        EntityUid overlay = default;

        await server.WaitPost(() =>
        {
            // The captain's chair: a frame, upholstery, gold trim that is not tinted, a back and two arms.
            chair = serverEnts.SpawnEntity("EmberCaptainChair", map.GridCoords);
            var comp = serverEnts.GetComponent<EmberProceduralFurnitureComponent>(chair);

            Assert.That(comp.Overlay, Is.Not.Null, "A chair was spawned without the half that draws over.");
            overlay = comp.Overlay!.Value;

            Assert.That(serverEnts.GetComponent<TransformComponent>(overlay).ParentUid, Is.EqualTo(chair),
                "The companion is not riding on the chair, so it will be left behind the moment it moves.");
        });

        await pair.RunTicksSync(10);

        await pair.Client.WaitPost(() =>
        {
            var clientChair = clientEnts.GetEntity(serverEnts.GetNetEntity(chair));
            var clientOverlay = clientEnts.GetEntity(serverEnts.GetNetEntity(overlay));

            var seat = clientEnts.GetComponent<SpriteComponent>(clientChair);
            var over = clientEnts.GetComponent<SpriteComponent>(clientOverlay);

            Assert.Multiple(() =>
            {
                // Under the sitter: frame, upholstery, and the gold that belongs to the design.
                Assert.That(States(seat), Is.EquivalentTo(new[] { "capchair", "capchair_padding", "capchair_special" }));

                // Above: the back and the upholstery on it. No arms, because nobody is sitting there yet.
                Assert.That(States(over), Is.EquivalentTo(new[] { "capchair_over", "capchair_padding_over" }));

                Assert.That((int) over.DrawDepth, Is.EqualTo((int) EmberDrawDepth.OverMobs),
                    "The half that is supposed to cover the sitter is not drawn above mobs.");

                Assert.That((int) seat.DrawDepth, Is.LessThan((int) EmberDrawDepth.Mobs),
                    "The seat is drawn above mobs, so a sitter would vanish into the chair.");
            });
        });

        // The arms close around an occupant, so they appear when there is one.
        await server.WaitPost(() =>
        {
            var comp = serverEnts.GetComponent<EmberProceduralFurnitureComponent>(chair);
            comp.Occupied = true;
            serverEnts.Dirty(chair, comp);
            furniture.Sync((chair, comp));
        });

        await pair.RunTicksSync(10);

        await pair.Client.WaitPost(() =>
        {
            var over = clientEnts.GetComponent<SpriteComponent>(
                clientEnts.GetEntity(serverEnts.GetNetEntity(overlay)));

            Assert.That(States(over), Does.Contain("capchair_armrest"),
                "Nobody's arms are on the armrests, because the chair never drew them.");
        });

        await server.WaitPost(() => serverEnts.DeleteEntity(chair));
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A part the sheet does not have is simply not drawn, which is what lets one system serve a stool of two
    /// states and a captain's chair of seven. A bare frame is a perfectly good chair.
    /// </summary>
    [Test]
    public async Task FurnitureOnlyDrawsThePartsItsSheetHas()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        EntityUid chair = default;

        await server.WaitPost(() =>
        {
            // A wooden chair has no upholstery on Bay and no _padding state to draw it with.
            chair = serverEnts.SpawnEntity("EmberWoodenChair", map.GridCoords);
        });

        await pair.RunTicksSync(10);

        var net = default(Robust.Shared.GameObjects.NetEntity);
        await server.WaitPost(() =>
        {
            Assert.That(serverEnts.EntityExists(chair), Is.True, "The chair was gone by the second tick.");
            net = serverEnts.GetNetEntity(chair);
        });

        await pair.Client.WaitPost(() =>
        {
            Assert.That(clientEnts.TryGetEntity(net, out var clientChair), Is.True,
                $"The client never received {net}.");

            var seat = clientEnts.GetComponent<SpriteComponent>(clientChair!.Value);

            Assert.That(States(seat), Is.EquivalentTo(new[] { "wooden_chair" }));
        });

        await server.WaitPost(() => serverEnts.DeleteEntity(chair));
        await pair.CleanReturnAsync();
    }

    private static List<string> States(SpriteComponent sprite)
    {
        return sprite.AllLayers
            .Select(layer => layer.Rsi != null ? layer.RsiState.Name : null)
            .Where(name => name != null)
            .Select(name => name!)
            .ToList();
    }
}
