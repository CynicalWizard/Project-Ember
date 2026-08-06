using System.Collections.Generic;
using System.Linq;
using Content.Client.Clickable;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Verbs;
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
    private static readonly ProtoId<ConstructionGraphPrototype> SeatGraph = "Seat";

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
            chair = serverEnts.SpawnEntity("ChairPilotSeat", map.GridCoords);
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
            chair = serverEnts.SpawnEntity("ChairWood", map.GridCoords);
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
                    var upholstery = rsi.TryGetState($"{name}_padding", out _) ||
                                     rsi.TryGetState($"{name}_padding_over", out _);

                    // An armchair has no frame on the sheet at all: the upholstery is the whole chair. So what
                    // is asked is that something gets drawn, not that a frame in particular does.
                    if (!rsi.TryGetState(name, out _) && !rsi.TryGetState($"{name}_over", out _) && !upholstery)
                        problems.Add($"{proto.ID}: {furniture.Sprite} has no parts called {name}");

                    if (furniture.Padding != null && !upholstery)
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
            chair = entities.SpawnEntity("ComfyChair", map.GridCoords);
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
    ///
    /// This asks the thing that actually decides. Checking that the prototype carries a bound would only prove
    /// the bound is written down, not that a click lands on the chair.
    /// </remarks>
    [Test]
    public async Task ASeatFacingNorthCanStillBeClicked()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var transform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        // Every seat, because the bound lives on the shared base and one of them wandering off it is exactly
        // the kind of thing nobody would notice until a player could not sit down.
        var seats = new List<string>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (!proto.Abstract &&
                    proto.TryGetComponent<EmberProceduralFurnitureComponent>(out var furniture, factory) &&
                    !furniture.DrawsOver)
                {
                    seats.Add(proto.ID);
                }
            }
        });

        Assert.That(seats, Is.Not.Empty, "No furniture to check, so this test would prove nothing.");

        var spawned = new List<(string Prototype, EntityUid Uid)>();

        await server.WaitPost(() =>
        {
            foreach (var id in seats)
            {
                var uid = serverEnts.SpawnEntity(id, map.GridCoords);
                transform.SetLocalRotation(uid, Angle.FromDegrees(180));
                spawned.Add((id, uid));
            }
        });

        await pair.RunTicksSync(30);

        var problems = new List<string>();

        await pair.Client.WaitPost(() =>
        {
            var clickables = clientEnts.System<ClickableSystem>();
            var clientTransform = clientEnts.System<SharedTransformSystem>();
            var eye = pair.Client.ResolveDependency<IEyeManager>().CurrentEye;

            foreach (var (id, uid) in spawned)
            {
                var clientUid = clientEnts.GetEntity(serverEnts.GetNetEntity(uid));
                var sprite = clientEnts.GetComponent<SpriteComponent>(clientUid);
                var clickable = clientEnts.GetComponentOrNull<ClickableComponent>(clientUid);

                // The middle of the tile it is standing on: where a player aiming at the chair clicks.
                var worldPos = clientTransform.GetWorldPosition(clientUid);

                if (!clickables.CheckClick((clientUid, clickable, sprite, null), worldPos, eye, out _, out _, out _))
                    problems.Add($"{id} facing north cannot be clicked");
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await server.WaitPost(() =>
        {
            foreach (var (_, uid) in spawned)
            {
                serverEnts.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// You can spin a chair you are sitting in. You cannot spin a sofa, because it is bolted down.
    /// </summary>
    /// <remarks>
    /// This is the one place Bay's furniture checks whether it is anchored: <c>/obj/structure/bed/chair/rotate</c>
    /// turns ninety degrees and never asks, while <c>/obj/structure/bed/sofa/rotate</c> refuses outright when
    /// anchored. A sofa is built in sections along a wall — turning one section from the seat is nonsense, and
    /// letting it happen is what putting <c>rotateWhileAnchored</c> on the shared base did.
    /// </remarks>
    [Test]
    public async Task AChairTurnsWhereItStandsAndASofaDoesNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var verbs = entities.System<SharedVerbSystem>();
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);

            bool CanTurn(string id)
            {
                var furniture = entities.SpawnEntity(id, map.GridCoords);

                Assert.That(entities.GetComponent<TransformComponent>(furniture).Anchored, Is.True,
                    $"{id} was not bolted down, so this proves nothing about anchored furniture.");

                var found = verbs.GetLocalVerbs(furniture, user, typeof(Verb))
                    .Any(verb => verb.Category == VerbCategory.Rotate);

                entities.DeleteEntity(furniture);
                return found;
            }

            Assert.Multiple(() =>
            {
                Assert.That(CanTurn("Chair"), Is.True, "A chair bolted to the floor will not turn.");
                Assert.That(CanTurn("EmberSofa"), Is.False, "A sofa bolted to the floor turns like a chair.");
                Assert.That(CanTurn("EmberSofaCorner"), Is.False, "A corner sofa turns like a chair.");
                Assert.That(CanTurn("EmberRoundedChair"), Is.True,
                    "A rounded chair is a chair on Bay and should turn like one.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The seats the game already had are the ones this system draws, rather than a second set standing next to
    /// them.
    /// </summary>
    /// <remarks>
    /// Converting them where they stand is what keeps the maps and the recipes working: every map in the repo
    /// says <c>Chair</c>, and a parallel <c>EmberChair</c> would mean every one of them still had the old
    /// picture. Named one by one, because a seat quietly dropping off this list is exactly the failure that
    /// would not show up anywhere else.
    /// </remarks>
    [Test]
    public async Task TheSeatsTheGameAlreadyHadAreDrawnByThisSystem()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        var converted = new[]
        {
            "Chair", "ChairGreyscale", "Stool", "StoolBar", "ChairOfficeLight", "ChairOfficeDark",
            "ComfyChair", "ChairPilotSeat", "ChairWood", "SteelBench", "WoodenBench",
        };

        var problems = new List<string>();

        await pair.Server.WaitPost(() =>
        {
            foreach (var id in converted)
            {
                if (!protoManager.TryIndex<EntityPrototype>(id, out var proto))
                {
                    problems.Add($"{id} no longer exists");
                    continue;
                }

                if (!proto.TryGetComponent<EmberProceduralFurnitureComponent>(out _, factory))
                    problems.Add($"{id} is still drawn as one flat picture");
            }

            // And the ones with art of their own were moved off the wooden chair rather than inheriting its
            // parts, which would have drawn them as wooden chairs.
            foreach (var id in new[] { "ChairRitual", "ChairCursed" })
            {
                if (protoManager.TryIndex<EntityPrototype>(id, out var proto) &&
                    proto.TryGetComponent<EmberProceduralFurnitureComponent>(out _, factory))
                {
                    problems.Add($"{id} has art of its own but inherited a wooden chair's parts");
                }
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Every seat you can build actually builds a seat, and one drawn by this system.
    /// </summary>
    /// <remarks>
    /// A construction recipe names a graph node, the node names an entity, and nothing checks that the entity
    /// is the one the recipe's picture is of. The menu renders procedural furniture by spawning what the recipe
    /// says it builds, so a recipe pointing at a prototype that no longer exists is a blank square in the menu
    /// and a runtime error when it is built.
    /// </remarks>
    [Test]
    public async Task EverySeatRecipeBuildsASeatThisSystemDraws()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();
        var entities = pair.Server.ResolveDependency<IEntityManager>();

        var problems = new List<string>();
        var found = 0;

        await pair.Server.WaitPost(() =>
        {
            var graph = protoManager.Index<ConstructionGraphPrototype>(SeatGraph);

            foreach (var recipe in protoManager.EnumeratePrototypes<ConstructionPrototype>())
            {
                if (recipe.Graph != SeatGraph)
                    continue;

                var node = graph.Nodes.GetValueOrDefault(recipe.TargetNode);

                // A node can pick what it builds at the time, so it is asked rather than read.
                if (node?.Entity.GetId(null, null, new GraphNodeEntityArgs(entities)) is not { } entity)
                {
                    problems.Add($"{recipe.ID} builds node {recipe.TargetNode}, which makes nothing");
                    continue;
                }

                if (!protoManager.TryIndex<EntityPrototype>(entity, out var proto))
                {
                    problems.Add($"{recipe.ID} builds {entity}, which does not exist");
                    continue;
                }

                // The picture in the menu is drawn by spawning what the recipe says it builds, so a recipe
                // whose id is not the entity's has to say which entity that is.
                if (proto.TryGetComponent<EmberProceduralFurnitureComponent>(out _, factory))
                {
                    found++;

                    if (recipe.IconEntity is not { } icon)
                    {
                        if (recipe.ID != entity)
                            problems.Add($"{recipe.ID} builds {entity} and has no iconEntity, so it draws blank");
                    }
                    else if (icon != entity)
                    {
                        problems.Add($"{recipe.ID} builds {entity} but shows a picture of {icon}");
                    }
                }
            }
        });

        Assert.That(found, Is.GreaterThan(10), "Hardly any seat recipes build procedural furniture.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

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
