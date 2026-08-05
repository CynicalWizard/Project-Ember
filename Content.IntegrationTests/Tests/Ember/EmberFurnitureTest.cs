using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Clickable;
using EmberDrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Content.Shared.Ember.Furniture;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

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
                // Under the sitter: the frame and its upholstery, and nothing else.
                Assert.That(States(seat), Is.EquivalentTo(new[] { "capchair", "capchair_padding" }));

                // Above: the back, the upholstery on it, and the gold — Bay puts its trim above the sitter
                // too, which is the whole point of a badge on the back of a chair. No arms, because nobody
                // is sitting there yet.
                Assert.That(States(over),
                    Is.EquivalentTo(new[] { "capchair_over", "capchair_padding_over", "capchair_special" }));

                // Gold stays gold: unlike a shuttle seat's harness, this is not part of the frame.
                Assert.That(Colour(over, "capchair_special"), Is.EqualTo(Color.White),
                    "The captain's gold was painted with the chair.");

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

        // Bay draws almost nothing in the base state when a chair faces north and puts the whole back into
        // _over, so a companion that does not turn with the chair leaves an empty tile. It rides on the chair,
        // so it should be facing wherever the chair is facing without being told.
        await server.WaitPost(() =>
        {
            var xform = serverEnts.GetComponent<TransformComponent>(chair);
            serverEnts.System<SharedTransformSystem>().SetLocalRotation(chair, Angle.FromDegrees(180), xform);
        });

        await pair.RunTicksSync(10);

        await server.WaitPost(() =>
        {
            var transform = serverEnts.System<SharedTransformSystem>();

            Assert.That(transform.GetWorldRotation(overlay).Theta,
                Is.EqualTo(transform.GetWorldRotation(chair).Theta).Within(0.001),
                "The companion is facing a different way from the chair it belongs to, so a chair turned "
                + "north would be drawn as an empty tile.");
        });

        await server.WaitPost(() => serverEnts.DeleteEntity(chair));
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The shuttle seat, which was reported broken and was.
    /// </summary>
    /// <remarks>
    /// Three things about it are unlike every other chair, and the first pass got all three wrong. Its trim is
    /// the harness rather than decoration, so it takes the frame's colour instead of keeping its own. The
    /// harness is hanging open, so it goes the moment anyone is strapped in. And <c>post_buckle_mob</c> swaps
    /// the whole seat to <c>shuttle_chair-b</c>, which is the one with arms — the harness closed around its
    /// occupant. Drawing the open harness under the sitter, untinted, on the unoccupied seat is what made it
    /// look like two chairs on one tile.
    /// </remarks>
    [Test]
    public async Task AShuttleSeatClosesItsHarnessAroundWhoeverSitsInIt()
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
            chair = serverEnts.SpawnEntity("EmberShuttleChair", map.GridCoords);
            overlay = serverEnts.GetComponent<EmberProceduralFurnitureComponent>(chair).Overlay!.Value;
        });

        await pair.RunTicksSync(10);

        await pair.Client.WaitPost(() =>
        {
            var seat = clientEnts.GetComponent<SpriteComponent>(
                clientEnts.GetEntity(serverEnts.GetNetEntity(chair)));
            var over = clientEnts.GetComponent<SpriteComponent>(
                clientEnts.GetEntity(serverEnts.GetNetEntity(overlay)));

            Assert.Multiple(() =>
            {
                Assert.That(States(seat), Is.EquivalentTo(new[] { "shuttle_chair", "shuttle_chair_padding" }));

                // The harness hangs open above the seat, painted with the seat rather than left white.
                Assert.That(States(over), Does.Contain("shuttle_chair_special"));
                Assert.That(Colour(over, "shuttle_chair_special"), Is.Not.EqualTo(Color.White),
                    "The harness is part of the seat and should be painted with it.");
            });
        });

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
            var seat = clientEnts.GetComponent<SpriteComponent>(
                clientEnts.GetEntity(serverEnts.GetNetEntity(chair)));
            var over = clientEnts.GetComponent<SpriteComponent>(
                clientEnts.GetEntity(serverEnts.GetNetEntity(overlay)));

            Assert.Multiple(() =>
            {
                Assert.That(States(seat), Is.EquivalentTo(new[] { "shuttle_chair-b", "shuttle_chair-b_padding" }),
                    "A seat with someone in it is still drawn as the empty one.");

                Assert.That(States(over), Does.Contain("shuttle_chair-b_armrest"),
                    "The harness never closed around its occupant.");

                Assert.That(States(over), Does.Not.Contain("shuttle_chair_special"),
                    "The open harness is still hanging there with someone strapped in behind it.");
            });
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

    /// <summary>
    /// Every part a piece of furniture is supposed to be built from has to be in the sheet under the name the
    /// component will look for.
    /// </summary>
    /// <remarks>
    /// A part the sheet does not have is silently skipped, which is what lets one system serve a stool and a
    /// captain's chair — and also what let the corner sofa ship as a bare frame. Its states were cut out under
    /// Bay's own names, <c>sofa_over_corner</c> and <c>sofa_padding_corner</c>, while the component builds its
    /// part names as base plus suffix and was asking for <c>sofa_corner_over</c>. Nothing anywhere complained;
    /// the sofa just lost its upholstery and its back and looked like a wooden bench from every side.
    ///
    /// So: whatever a prototype claims to have, the sheet must actually have. Upholstery it names has to exist
    /// somewhere, and so does the set of parts it swaps to when someone sits down.
    /// </remarks>
    [Test]
    public async Task EveryPieceOfFurnitureFindsItsPartsInTheSheet()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var cache = pair.Client.ResolveDependency<IResourceCache>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        var problems = new List<string>();

        await pair.Client.WaitPost(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract ||
                    !proto.TryGetComponent<EmberProceduralFurnitureComponent>(out var furniture, factory))
                {
                    continue;
                }

                // The companion is handed its parts by the piece it belongs to and carries a placeholder here.
                if (furniture.DrawsOver)
                    continue;

                var rsi = cache.GetResource<RSIResource>(furniture.Sprite).RSI;
                var names = new List<string> { furniture.BaseState };

                if (furniture.OccupiedState is { } occupied)
                    names.Add(occupied);

                foreach (var name in names)
                {
                    // Something has to be drawn under the sitter or above them, or there is no chair at all.
                    if (!rsi.TryGetState(name, out _) && !rsi.TryGetState($"{name}_over", out _))
                        problems.Add($"{proto.ID}: {furniture.Sprite} has nothing called {name}");

                    if (furniture.Padding == null)
                        continue;

                    if (!rsi.TryGetState($"{name}_padding", out _) &&
                        !rsi.TryGetState($"{name}_padding_over", out _))
                    {
                        problems.Add($"{proto.ID}: upholstered in {furniture.Padding}, but {furniture.Sprite} "
                            + $"has no {name}_padding to draw it on");
                    }
                }

                if (furniture.Special && !rsi.TryGetState($"{furniture.BaseState}_special", out _))
                    problems.Add($"{proto.ID}: has trim, but {furniture.Sprite} has no {furniture.BaseState}_special");
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A chair a mapper already turned north still has to face north once the round starts.
    /// </summary>
    /// <remarks>
    /// The companion is spawned facing south and then parented to the chair, and parenting keeps world rotation
    /// — so onto a chair that is already turned it lands with a local rotation cancelling the chair's, and stays
    /// exactly that far behind it forever after. Bay draws nothing under the sitter at north and the whole chair
    /// above, so such a chair is an empty tile in the direction it is most often mapped facing.
    /// </remarks>
    [Test]
    public async Task FurnitureMappedFacingNorthIsStillFacingNorthWhenTheRoundStarts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var maps = server.System<SharedMapSystem>();
        var transform = server.System<SharedTransformSystem>();

        // Uninitialized, so the chair can be turned before map init the way a mapper turns it in the editor.
        var map = await pair.CreateTestMap(initialized: false);

        EntityUid chair = default;

        await server.WaitPost(() =>
        {
            chair = entities.SpawnEntity("EmberComfyChair", map.GridCoords);
            transform.SetLocalRotation(chair, Angle.FromDegrees(180));

            Assert.That(entities.GetComponent<EmberProceduralFurnitureComponent>(chair).Overlay, Is.Null,
                "The companion appeared before map init, so this proves nothing about mapped furniture.");
        });

        await server.WaitPost(() => maps.InitializeMap(map.MapId));
        await server.WaitRunTicks(5);

        await server.WaitPost(() =>
        {
            var overlay = entities.GetComponent<EmberProceduralFurnitureComponent>(chair).Overlay;

            Assert.That(overlay, Is.Not.Null, "Map init never gave the chair its other half.");

            Assert.That(transform.GetWorldRotation(overlay!.Value).Theta,
                Is.EqualTo(transform.GetWorldRotation(chair).Theta).Within(0.001),
                "A chair mapped facing north got a companion facing south, so the tile is drawn empty.");
        });

        await server.WaitPost(() => entities.DeleteEntity(chair));
        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A seat drawn entirely above the sitter still has to be clickable.
    /// </summary>
    /// <remarks>
    /// Click detection runs over the pixels of the entity's own sprite, and every Bay seat facing north has
    /// none — the whole chair is in the half the companion draws, and the companion is not a click target at
    /// all. Without an explicit bound the chair is there, visible, and cannot be sat in.
    /// </remarks>
    [Test]
    public async Task ASeatFacingNorthCanStillBeClicked()
    {
        // The client's, because Clickable is a client component and the server drops it on the floor.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var protoManager = pair.Client.ResolveDependency<IPrototypeManager>();
        var factory = pair.Client.ResolveDependency<IComponentFactory>();

        var problems = new List<string>();

        await pair.Client.WaitPost(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract ||
                    !proto.TryGetComponent<EmberProceduralFurnitureComponent>(out var furniture, factory) ||
                    furniture.DrawsOver)
                {
                    continue;
                }

                if (!proto.TryGetComponent<ClickableComponent>(out var clickable, factory) ||
                    clickable.Bounds is not { } bounds ||
                    bounds.North.Size == Vector2.Zero)
                {
                    problems.Add(proto.ID);
                }
            }
        });

        Assert.That(problems, Is.Empty,
            "These have nothing to click when they face north:\n" + string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    private static Color Colour(SpriteComponent sprite, string state)
    {
        return sprite.AllLayers.First(layer => layer.Rsi != null && layer.RsiState.Name == state).Color;
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
