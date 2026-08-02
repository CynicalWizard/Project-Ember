using System.Collections.Generic;
using System.Linq;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Skills;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Rods are a material stack in Bay: any metal can be drawn into them, one sheet giving two. Each metal needs a
/// stack prototype, an entity and a recipe that all agree with each other, which is exactly the kind of thing
/// that rots when a new metal is added and one of the three is forgotten.
/// </summary>
[TestFixture]
public sealed class EmberRodTest
{
    [Test]
    public async Task EveryRodRecipeProducesTheStackItNames()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var entManager = pair.Server.ResolveDependency<IEntityManager>();

        var problems = new List<string>();
        var found = 0;

        foreach (var recipe in protoManager.EnumeratePrototypes<ConstructionPrototype>())
        {
            if (!recipe.ID.StartsWith("EmberRod"))
                continue;

            found++;

            if (!protoManager.TryIndex(recipe.Graph, out ConstructionGraphPrototype? graph) ||
                graph.Edge(recipe.StartNode, recipe.TargetNode) is not { } edge)
            {
                problems.Add($"{recipe.ID} has no edge from {recipe.StartNode} to {recipe.TargetNode}");
                continue;
            }

            var entityId = graph.Nodes[recipe.TargetNode].Entity
                .GetId(null, null, new GraphNodeEntityArgs(entManager));

            if (entityId != null)
            {
                if (!protoManager.TryIndex(entityId, out EntityPrototype? entity))
                {
                    problems.Add($"{recipe.ID} builds {entityId}, which does not exist");
                    continue;
                }

                if (!entity.Components.TryGetComponent("Stack", out var rawStack))
                    problems.Add($"{recipe.ID} builds {entityId}, which is not a stack");
                else if (!protoManager.HasIndex(((StackComponent) rawStack).StackTypeId))
                    problems.Add($"{entityId} is a stack of a type that does not exist");

                if (!entity.Components.ContainsKey(
                        componentFactory.GetComponentName<EmberMaterialStackComponent>()))
                {
                    problems.Add($"{entityId} is not tied to a material, so it will not take its colour");
                }
            }

            // Bay draws two rods from one sheet, and the sheet has to be a material we actually have.
            var sheets = edge.Steps.OfType<MaterialConstructionGraphStep>().ToList();
            if (sheets.Count != 1)
                problems.Add($"{recipe.ID} consumes {sheets.Count} materials rather than one sheet");
            else if (!protoManager.HasIndex<StackPrototype>(sheets[0].MaterialPrototypeId))
                problems.Add($"{recipe.ID} consumes '{sheets[0].MaterialPrototypeId}', which is not a stack");
        }

        Assert.That(found, Is.GreaterThan(0), "No rod recipes were found at all.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Drawing rods is skill-gated through the same path as everything else, from the material rather than from
    /// a number written into the recipe. A gold rod should therefore be harder to draw than an iron one.
    /// </summary>
    [Test]
    public async Task RodDifficultyComesFromTheMetal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        var difficulties = new Dictionary<string, int>();

        await pair.Server.WaitPost(() =>
        {
            foreach (var recipe in protoManager.EnumeratePrototypes<ConstructionPrototype>())
            {
                if (!recipe.ID.StartsWith("EmberRod") ||
                    !protoManager.TryIndex(recipe.Graph, out ConstructionGraphPrototype? graph) ||
                    graph.Edge(recipe.StartNode, recipe.TargetNode) is not { } edge)
                {
                    continue;
                }

                difficulties[recipe.ID] = EmberConstructionSkill.GetDifficulty(edge, protoManager, factory);
            }
        });

        Assert.That(difficulties, Is.Not.Empty);
        Assert.That(difficulties.Values.Distinct().Count(), Is.GreaterThan(1),
            "Every rod is the same difficulty, so the material is not reaching the skill check. " +
            string.Join(", ", difficulties.Select(pair => $"{pair.Key}={pair.Value}")));

        await pair.CleanReturnAsync();
    }
}
