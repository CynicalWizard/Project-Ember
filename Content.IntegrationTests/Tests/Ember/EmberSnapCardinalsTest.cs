#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Nothing procedural may ask to be snapped to the cardinal directions, because everything procedural is drawn
/// from four-directional art and the engine forbids the combination.
/// </summary>
/// <remarks>
/// Snapping picks one of the four compass angles to draw a sprite at, which only makes sense when there is one
/// picture to turn; a sheet that already has a picture per direction would be turned twice.
/// <see cref="Robust.Client.GameObjects.SpriteSystem.CalculateLocalBounds"/> asserts on it — and an assert is a
/// dead client, not a warning, so it takes the whole test pool with it and reports itself as several hundred
/// unrelated failures a millisecond apart.
///
/// It has happened twice: once on an airlock collar layer, and once on the hazard shutter, whose prototype asked
/// for snapping to keep it upright against a mapper's rotation. Upright is what no-rot is for; the airlocks were
/// already doing it that way. Since neither time was caught by anything that named the culprit, this walks every
/// entity our own systems dress and asks the engine for its bounds, which is where the rule lives.
///
/// Only a build with asserts in it can answer, which is the one CI uses.
/// </remarks>
[TestFixture]
public sealed class EmberSnapCardinalsTest
{
    [Test]
    public async Task NothingProceduralSnapsFourDirectionalArtToTheCardinals()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();
        var map = await pair.CreateTestMap();

        var dressed = new[]
        {
            factory.GetComponentName<EmberProceduralWallComponent>(),
            factory.GetComponentName<EmberProceduralStructureComponent>(),
            factory.GetComponentName<EmberProceduralTableComponent>(),
            factory.GetComponentName<EmberProceduralAirlockComponent>(),
            factory.GetComponentName<EmberProceduralFirelockComponent>(),
            factory.GetComponentName<EmberProceduralMaterialDoorComponent>(),
        };

        var ids = prototypes.EnumeratePrototypes<EntityPrototype>()
            .Where(entity => !entity.Abstract && dressed.Any(name => entity.Components.ContainsKey(name)))
            .Select(entity => entity.ID)
            .OrderBy(id => id)
            .ToList();

        Assert.That(ids, Is.Not.Empty, "Nothing procedural was found at all.");

        var spawned = new List<(string Id, NetEntity Net)>();

        await server.WaitPost(() =>
        {
            var maps = serverEnts.System<SharedMapSystem>();
            var tile = new Tile(server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);

            // One tile each, since several of these refuse to share one and would delete each other.
            for (var i = 0; i < ids.Count; i++)
            {
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new EntityCoordinates(map.Grid, i * 2, 0), tile);
            }

            for (var i = 0; i < ids.Count; i++)
            {
                var uid = serverEnts.SpawnEntity(ids[i], new EntityCoordinates(map.Grid, i * 2, 0));
                spawned.Add((ids[i], serverEnts.GetNetEntity(uid)));
            }
        });

        await pair.RunTicksSync(15);

        var problems = new List<string>();

        await pair.Client.WaitPost(() =>
        {
            var sprites = clientEnts.System<SpriteSystem>();

            foreach (var (id, net) in spawned)
            {
                if (!clientEnts.TryGetEntity(net, out var uid) ||
                    !clientEnts.TryGetComponent(uid, out SpriteComponent? sprite))
                {
                    continue;
                }

                // Asking the engine rather than restating its rule: whether a layer snaps depends on the
                // sprite, on whether it renders its layers granularly, and on that layer's own strategy, and a
                // second copy of that in here would be one more thing to keep in step.
                try
                {
                    sprites.GetLocalBounds((uid.Value, sprite));
                }
                catch (DebugAssertException)
                {
                    var offenders = sprite.AllLayers
                        .Where(layer => layer.ActualRsi is { } rsi
                                        && rsi.TryGetState(layer.RsiState, out var state)
                                        && state.RsiDirections != RsiDirectionType.Dir1)
                        .Select(layer => $"'{layer.RsiState}' of {layer.ActualRsi?.Path}");

                    problems.Add($"{id} snaps to cardinals while drawing {string.Join(", ", offenders)}");
                }
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems.Distinct()));

        await pair.CleanReturnAsync();
    }
}
