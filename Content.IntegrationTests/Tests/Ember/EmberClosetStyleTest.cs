using System.Collections.Generic;
using System.Linq;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Storage;
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

    private static string Sheet(EmberClosetShape shape)
    {
        return shape == EmberClosetShape.LargeCrate ? "large_crate" : shape.ToString().ToLowerInvariant();
    }

    private static RSI Rsi(IResourceCache cache, string path)
    {
        return cache.GetResource<RSIResource>(new ResPath(path)).RSI;
    }
}
