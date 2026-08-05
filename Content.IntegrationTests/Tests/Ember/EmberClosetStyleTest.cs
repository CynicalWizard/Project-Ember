using System.Collections.Generic;
using System.Linq;
using Content.Shared.Ember.Materials;
using Content.Client.Ember.Storage;
using Content.Client.Storage.Visualizers;
using Content.Shared.Ember.Storage;
using Content.Shared.Labels;
using Content.Shared.Storage;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// The closet styles are read out of Bay's DM by a generator, so the names in them are only ever as good as the
/// parse. A marking whose name does not exist in the sheet is drawn as nothing at all and says nothing about
/// itself, which is exactly the kind of mistake that reaches a live round.
/// </summary>
[TestFixture]
public sealed class EmberClosetStyleTest
{
    [Test]
    public async Task EveryMarkingExistsInItsSheet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var cache = pair.Client.ResolveDependency<IResourceCache>();

        var problems = new List<string>();

        await pair.Client.WaitPost(() =>
        {
            foreach (var style in protoManager.EnumeratePrototypes<EmberClosetStylePrototype>())
            {
                if (style.Abstract)
                    continue;

                var shape = Sheet(style.Shape);
                var markings = Sheet(style.Markings);

                var bases = Rsi(cache, $"/Textures/Ember/Structures/Storage/bases/{shape}.rsi");
                var decals = Rsi(cache, $"/Textures/Ember/Structures/Storage/decals/{markings}.rsi");

                // Every shape draws these, so a sheet without one is a container with a hole in it.
                foreach (var state in new[] { "base", "open", "interior", "welded", "blank" })
                {
                    if (!bases.TryGetState(state, out _))
                        problems.Add($"{style.ID}: {shape} has no {state}");
                }

                foreach (var decal in style.AllDecals())
                {
                    if (!decals.TryGetState($"{decal.State}_closed", out _) &&
                        !decals.TryGetState($"{decal.State}_open", out _) &&
                        !decals.TryGetState(decal.State, out _))
                    {
                        problems.Add($"{style.ID}: {markings} has no marking called {decal.State}");
                    }
                }
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Bay's containers are steel unless they say otherwise, and the material is what decides whether a crate
    /// survives the room it is standing in. A procedural container that names none would not melt at all.
    /// </summary>
    [Test]
    public async Task EveryProceduralContainerIsMadeOfSomething()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        var closet = factory.GetComponentName<EmberProceduralClosetComponent>();
        var composition = factory.GetComponentName<EmberMaterialCompositionComponent>();

        var problems = protoManager.EnumeratePrototypes<EntityPrototype>()
            .Where(proto => !proto.Abstract && proto.Components.ContainsKey(closet))
            .Where(proto => !proto.Components.ContainsKey(composition))
            .Select(proto => proto.ID)
            .ToList();

        Assert.That(problems, Is.Empty,
            "These containers are procedural but made of nothing:\n" + string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// What a container actually looks like once the client has it, which is the only question that matters
    /// and the one nothing was asking. Two passes shipped with the procedural layers drawn underneath the
    /// vanilla sprite they replace, and a third with the vanilla visualiser setting states on a sheet that
    /// never had them: all three were invisible to prototype-level checks.
    /// </summary>
    [Test]
    public async Task ConvertedContainersDrawOnlyTheirOwnLayers()
    {
        // Connected, because nothing is replicated to a client that is not, and the whole point here is to
        // look at what the client ended up drawing.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var map = await pair.CreateTestMap();

        // A handful rather than all hundred and thirty: every one of them has to cross the wire and be drawn
        // before it can be looked at, and a hundred and thirty at once outruns what the client is sent in the
        // ticks this waits. One of each shape is what the layering actually depends on.
        var wanted = new[]
        {
            "LockerSecurity",
            "ClosetEmergency",
            "WardrobeGreen",
            "CrateMedical",
            "CratePlastic",
            "CrateThermal",
            "ClosetWallEmergency",

            // Named because they were reported drawing both sprites at once: variants that lay out art of
            // their own, and one that reaches its crate through a second parent.
            "CrateEngineeringParticleAccelerator",
            "CrateArmorySMG",
            "CrateMaterialPlasma",
            "CrateSecgear",
            "LockerBooze",
            "WardrobeMedicalDoctor",
        };

        var closet = factory.GetComponentName<EmberProceduralClosetComponent>();
        foreach (var id in wanted)
        {
            Assert.That(protoManager.Index<EntityPrototype>(id).Components.ContainsKey(closet), Is.True,
                $"{id} is supposed to be procedural and is not, so this test would prove nothing.");
        }

        var spawned = new List<(string Prototype, EntityUid Uid)>();

        await server.WaitPost(() =>
        {
            foreach (var id in wanted)
            {
                spawned.Add((id, serverEnts.SpawnEntity(id, map.GridCoords)));
            }
        });

        await pair.RunTicksSync(30);

        var problems = new List<string>();

        await pair.Client.WaitPost(() =>
        {
            foreach (var (id, uid) in spawned)
            {
                var clientUid = clientEnts.GetEntity(serverEnts.GetNetEntity(uid));

                if (!clientEnts.TryGetComponent(clientUid, out SpriteComponent? sprite))
                {
                    problems.Add($"{id} has no sprite on the client at all");
                    continue;
                }

                if (!sprite.LayerMapTryGet(EmberClosetLayer.Base, out var index))
                {
                    problems.Add($"{id} never got its procedural base layer");
                    continue;
                }

                // The vanilla visualisers draw the same container out of one flat sheet. Either they are gone
                // or there are two containers on the entity, one of them on top.
                if (clientEnts.HasComponent<EntityStorageVisualsComponent>(clientUid))
                    problems.Add($"{id} still has the vanilla storage visualiser");

                if (sprite.LayerMapTryGet(StorageVisualLayers.Base, out _))
                    problems.Add($"{id} still has the vanilla base layer");

                // A paper label belongs above the container; nothing else does. Anything else left over is a
                // second container drawn on the same entity, which is what "it looks like both" means.
                var label = sprite.LayerMapTryGet(PaperLabelVisuals.Layer, out var labelIndex);
                if (label && labelIndex < index)
                    problems.Add($"{id} draws its label under itself");

                var own = 3 + protoManager.Index(
                    clientEnts.GetComponent<EmberProceduralClosetComponent>(clientUid).Style).AllDecals().Count() + 3;

                if (sprite.AllLayers.Count() != own + (label ? 1 : 0))
                {
                    problems.Add(
                        $"{id} has {sprite.AllLayers.Count()} layers where {own + (label ? 1 : 0)} are its own");
                }
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

    private static string Sheet(EmberClosetShape shape)
    {
        return shape == EmberClosetShape.LargeCrate ? "large_crate" : shape.ToString().ToLowerInvariant();
    }

    private static RSI Rsi(IResourceCache cache, string path)
    {
        return cache.GetResource<RSIResource>(new ResPath(path)).RSI;
    }
}
