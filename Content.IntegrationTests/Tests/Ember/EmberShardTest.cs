using System.Collections.Generic;
using System.Linq;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Walls;
using Content.Shared.Tools.Components;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Debris is built at runtime out of a material, so nothing in the prototypes says what a given shard will look
/// like or refine into. These check that every material which claims to shatter can actually produce something.
/// </summary>
[TestFixture]
public sealed class EmberShardTest
{
    [Test]
    public async Task EveryShardTypeHasItsSpritesOnTheSheet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var cache = pair.Client.ResolveDependency<IResourceCache>();
        var sheet = new ResPath("/Textures/Ember/Objects/Materials/shards.rsi");

        var problems = new List<string>();

        await pair.Client.WaitPost(() =>
        {
            if (!cache.TryGetResource<RSIResource>(sheet, out var resource))
            {
                problems.Add($"{sheet} does not load");
                return;
            }

            foreach (EmberShardType type in Enum.GetValues<EmberShardType>())
            {
                if (EmberShardTypes.GetIconBase(type) is not { } iconBase)
                    continue;

                foreach (var size in EmberShardTypes.Sizes)
                {
                    if (!resource.RSI.TryGetState(iconBase + size, out _))
                        problems.Add($"{type} has no '{iconBase}{size}' state");
                }
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The debris name is assembled from Fluent, and a material whose name has no entry would show a raw id.
    /// </summary>
    [Test]
    public async Task ShatteringMaterialsProduceANamedShard()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var transform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        var problems = new List<string>();

        await server.WaitPost(() =>
        {
            foreach (var material in protoManager.EnumeratePrototypes<EmberMaterialPrototype>())
            {
                if (material.ShardType == EmberShardType.None)
                    continue;

                // Spawned exactly the way the destruction behaviour does it, so this covers that path too.
                var shard = EmberSpawnMaterialDebrisBehavior.SpawnShard(
                    entManager,
                    material.ID,
                    transform.ToMapCoordinates(map.GridCoords));

                var name = entManager.GetComponent<MetaDataComponent>(shard).EntityName;

                if (string.IsNullOrWhiteSpace(name) || name.Contains("ember-shard") || name == "shard")
                    problems.Add($"{material.ID} produces debris named \"{name}\"");

                // Splinters cannot be welded back, but anything else that has a sheet form should refine.
                if (material.ShardCanRepair && material.StackEntity != null &&
                    !entManager.HasComponent<ToolRefinableComponent>(shard))
                {
                    problems.Add($"{material.ID} debris cannot be welded back into a sheet");
                }

                entManager.DeleteEntity(shard);
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Every procedural wall should leave something behind when it comes down, which means its material needs
    /// either a shard type or a sheet to fall back on.
    /// </summary>
    [Test]
    public async Task EveryProceduralWallLeavesDebris()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var wallName = componentFactory.GetComponentName<EmberProceduralWallComponent>();

        var problems = new List<string>();

        foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.Components.TryGetComponent(wallName, out var raw))
                continue;

            var wall = (EmberProceduralWallComponent) raw;

            if (!protoManager.TryIndex(wall.Material, out EmberWallMaterialPrototype? wallMaterial) ||
                wallMaterial.PhysicalMaterial is not { } physicalId ||
                !protoManager.TryIndex(physicalId, out EmberMaterialPrototype? material))
            {
                continue;
            }

            if (material.ShardType == EmberShardType.None && material.StackEntity == null)
                problems.Add($"{proto.ID} is made of {material.ID}, which leaves nothing when destroyed");
        }

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }
}
