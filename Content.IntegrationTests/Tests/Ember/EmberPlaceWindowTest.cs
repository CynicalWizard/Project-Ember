using System.Collections.Generic;
using System.Linq;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Ember.Structures;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Bay lets you hold glass against a low wall or a grille and get a window without opening a build menu. Rather
/// than keeping a second list of what each kind of glass becomes, the system reads the ordinary window recipe, so
/// what the recipe says has to be enough to build from on its own.
/// </summary>
[TestFixture]
public sealed class EmberPlaceWindowTest
{
    private const string WindowGraph = "Window";

    [Test]
    public async Task EveryGlassRecipeNamesAWindowThatCanBePlacedByHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var entManager = pair.Server.ResolveDependency<IEntityManager>();

        var tagName = componentFactory.GetComponentName<TagComponent>();

        var problems = new List<string>();
        var found = 0;

        Assert.That(protoManager.TryIndex(WindowGraph, out ConstructionGraphPrototype? graph), Is.True);
        Assert.That(graph!.Start, Is.Not.Null);
        Assert.That(graph.Nodes.TryGetValue(graph.Start!, out var start), Is.True);

        foreach (var edge in start!.Edges)
        {
            // The same rule the system uses: one material and nothing else. A shuttle window also wants
            // plasteel and is not something you can hold up against a frame.
            if (edge.Steps.Count != 1 || edge.Steps[0] is not MaterialConstructionGraphStep material)
                continue;

            if (!protoManager.HasIndex<StackPrototype>(material.MaterialPrototypeId))
            {
                problems.Add($"{edge.Target} is built from {material.MaterialPrototypeId}, which is not a stack");
                continue;
            }

            found++;

            if (material.Amount <= 0)
                problems.Add($"{edge.Target} asks for {material.Amount} sheets, so it would cost nothing to place");

            if (!graph.Nodes.TryGetValue(edge.Target, out var node))
            {
                problems.Add($"{edge.Target} is not a node in the {WindowGraph} graph");
                continue;
            }

            if (node.Entity.GetId(null, null, new GraphNodeEntityArgs(entManager)) is not { } windowId)
            {
                problems.Add($"{edge.Target} builds no entity, so placing it by hand would consume the glass for nothing");
                continue;
            }

            if (!protoManager.TryIndex(windowId, out EntityPrototype? window))
            {
                problems.Add($"{edge.Target} builds {windowId}, which does not exist");
                continue;
            }

            // Placement is refused when the tile already holds something tagged Window, so a window that is not
            // tagged could be stacked on top of itself forever.
            if (!window.Components.TryGetComponent(tagName, out var rawTag) ||
                !((TagComponent) rawTag).Tags.Contains<ProtoId<TagPrototype>>("Window"))
            {
                problems.Add($"{windowId} is not tagged Window, so nothing stops a second one going up on the same tile");
            }
        }

        Assert.That(found, Is.GreaterThan(0), "No glass builds a window on its own.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Reinforced glass builds both a plain and a tinted window, and by hand you get the cheaper one. That only
    /// stays true while the two recipes cost different amounts.
    /// </summary>
    [Test]
    public async Task GlassWithTwoRecipesHasOneCheapestWindow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.That(protoManager.TryIndex(WindowGraph, out ConstructionGraphPrototype? graph), Is.True);
        Assert.That(graph!.Nodes.TryGetValue(graph.Start!, out var start), Is.True);

        var cheapest = new Dictionary<string, (string Node, int Amount, int Count)>();

        foreach (var edge in start!.Edges)
        {
            if (edge.Steps.Count != 1 || edge.Steps[0] is not MaterialConstructionGraphStep material)
                continue;

            if (!cheapest.TryGetValue(material.MaterialPrototypeId, out var best) || material.Amount < best.Amount)
                cheapest[material.MaterialPrototypeId] = (edge.Target, material.Amount, 1);
            else if (material.Amount == best.Amount)
                cheapest[material.MaterialPrototypeId] = (best.Node, best.Amount, best.Count + 1);
        }

        var ties = cheapest
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => $"{pair.Key} builds {pair.Value.Count} different windows for {pair.Value.Amount} sheets")
            .ToList();

        Assert.That(ties, Is.Empty, string.Join("\n", ties));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The low wall and the grille are the only two things you can glaze, and the system finds them by role
    /// rather than by prototype, so both roles have to be represented.
    /// </summary>
    [Test]
    public async Task LowWallsAndGrillesExist()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();

        var structureName = componentFactory.GetComponentName<EmberProceduralStructureComponent>();
        var roles = new HashSet<EmberProceduralStructureRole>();

        foreach (var entity in protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (entity.Abstract || !entity.Components.TryGetComponent(structureName, out var raw))
                continue;

            roles.Add(((EmberProceduralStructureComponent) raw).Role);
        }

        Assert.That(roles, Does.Contain(EmberProceduralStructureRole.WallFrame));
        Assert.That(roles, Does.Contain(EmberProceduralStructureRole.Grille));
        Assert.That(roles, Does.Contain(EmberProceduralStructureRole.Window));

        await pair.CleanReturnAsync();
    }
}
