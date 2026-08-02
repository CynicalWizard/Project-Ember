using System.Collections.Generic;
using System.Linq;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Ember.Skills;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Construction difficulty is derived, not authored: it is read off whichever Ember material the recipe's stacks
/// happen to point at. That makes it easy to break silently — a stack that loses its EmberMaterialStack, or a
/// material with no constructionDifficulty, just quietly drops the recipe back to "anyone can build this".
/// </summary>
[TestFixture]
public sealed class EmberConstructionSkillTest
{
    [Test]
    public async Task EveryRecipeHasADifficultyOnTheSkillScale()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        var problems = new List<string>();

        // Reading components off a prototype resolves through IoC, which only has a context on the sim thread.
        await pair.Server.WaitPost(() =>
        {
            foreach (var (proto, edge) in EnumerateRecipes(protoManager))
            {
                var difficulty = EmberConstructionSkill.GetDifficulty(edge, protoManager, factory);

                if (difficulty < EmberConstructionSkill.MinDifficulty ||
                    difficulty > EmberConstructionSkill.MaxDifficulty)
                {
                    problems.Add($"{proto.ID} has difficulty {difficulty}");
                    continue;
                }

                // The failure roll takes the difficulty as a skill level, so it has to survive that conversion.
                var required = EmberConstructionSkill.GetRequiredLevel(difficulty);
                if (required < SkillLevels.Min || required > SkillLevels.Max)
                    problems.Add($"{proto.ID} requires {required}, which is off the skill scale");
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// If nothing in the catalogue ever exceeds the base difficulty then the skill gate is inert: every recipe is
    /// safe for everyone and the construction skill does nothing but change build speed.
    /// </summary>
    [Test]
    public async Task HardMaterialsActuallyRaiseTheDifficulty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        var byDifficulty = new Dictionary<int, int>();

        await pair.Server.WaitPost(() =>
        {
            foreach (var (_, edge) in EnumerateRecipes(protoManager))
            {
                var difficulty = EmberConstructionSkill.GetDifficulty(edge, protoManager, factory);
                byDifficulty[difficulty] = byDifficulty.GetValueOrDefault(difficulty) + 1;
            }
        });

        var spread = string.Join(", ", byDifficulty.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}: {pair.Value}"));
        TestContext.Out.WriteLine($"Recipes by construction difficulty: {spread}");

        Assert.That(byDifficulty.Keys.Any(difficulty => difficulty > 1), Is.True,
            $"No construction recipe is harder than the base level, so the skill never gates anything. Spread was {spread}.");

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<(ConstructionPrototype Proto, ConstructionGraphEdge Edge)> EnumerateRecipes(
        IPrototypeManager protoManager)
    {
        foreach (var proto in protoManager.EnumeratePrototypes<ConstructionPrototype>())
        {
            if (!protoManager.TryIndex(proto.Graph, out ConstructionGraphPrototype? graph))
                continue;

            if (graph.Edge(proto.StartNode, proto.TargetNode) is { } edge)
                yield return (proto, edge);
        }
    }
}
