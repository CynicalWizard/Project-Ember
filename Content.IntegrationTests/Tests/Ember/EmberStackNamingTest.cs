using System.Collections.Generic;
using System.Linq;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Stacks;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// The construction guide names a step's material from the stack prototype, and a stack with no name shows the
/// player "add 1 of" followed by nothing at all. It fails quietly: the recipe still works, it just cannot say
/// what it wants.
/// </summary>
[TestFixture]
public sealed class EmberStackNamingTest
{
    [Test]
    public async Task EveryMaterialAskedForByARecipeCanBeNamed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var loc = pair.Server.ResolveDependency<ILocalizationManager>();

        var problems = new List<string>();
        var seen = new HashSet<string>();
        var checked_ = 0;

        foreach (var recipe in protoManager.EnumeratePrototypes<ConstructionPrototype>())
        {
            if (!protoManager.TryIndex(recipe.Graph, out ConstructionGraphPrototype? graph))
                continue;

            foreach (var node in graph.Nodes.Values)
            {
                foreach (var edge in node.Edges)
                {
                    foreach (var step in edge.Steps.OfType<MaterialConstructionGraphStep>())
                    {
                        if (!seen.Add(step.MaterialPrototypeId) ||
                            !protoManager.TryIndex(step.MaterialPrototypeId, out StackPrototype? stack))
                        {
                            continue;
                        }

                        // Ember's own stacks are the ones this fixture is about; the vanilla set has its own
                        // gaps that are not ours to fix in passing.
                        if (!IsEmber(stack, protoManager))
                            continue;

                        checked_++;

                        if (string.IsNullOrWhiteSpace(stack.Name))
                            problems.Add($"{stack.ID} has no name, so recipes asking for it show a blank");
                        else if (!loc.TryGetString(stack.Name, out var text) || string.IsNullOrWhiteSpace(text))
                            problems.Add($"{stack.ID} is named '{stack.Name}', which has no string");
                    }
                }
            }
        }

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    private static bool IsEmber(StackPrototype stack, IPrototypeManager protoManager)
    {
        if (stack.ID.StartsWith("Ember"))
            return true;

        return !string.IsNullOrEmpty(stack.Spawn.Id) &&
               protoManager.TryIndex(stack.Spawn, out EntityPrototype? entity) &&
               entity.ID.StartsWith("Ember");
    }
}
