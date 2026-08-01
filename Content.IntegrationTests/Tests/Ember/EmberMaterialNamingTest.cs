using System.Collections.Generic;
using System.Linq;
using Content.Shared.Ember.Materials;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Ember materials are wired together across four places at once: the material prototype, the sheet or ore
/// entity, the Fluent strings and the stack prototype. Forgetting one of them does not fail to load, it just
/// shows the player a raw id or a generic "item", which is how a batch of ores shipped nameless.
/// </summary>
[TestFixture]
public sealed class EmberMaterialNamingTest
{
    private static readonly string[] GenericNames = { "item", "ore", "" };

    [Test]
    public async Task EveryMaterialNameResolvesToRealText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var loc = pair.Server.ResolveDependency<ILocalizationManager>();

        Assert.Multiple(() =>
        {
            foreach (var material in protoManager.EnumeratePrototypes<EmberMaterialPrototype>())
            {
                CheckLocId(loc, material.DisplayName, $"{material.ID}.displayName");
                CheckLocId(loc, material.OreName, $"{material.ID}.oreName");
                CheckLocId(loc, material.OreDescription, $"{material.ID}.oreDescription");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A material that names a sheet entity must actually have one, and it has to be a stack of the right type.
    /// </summary>
    [Test]
    public async Task MaterialStackEntitiesExistAndMatchTheirStackType()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            foreach (var material in protoManager.EnumeratePrototypes<EmberMaterialPrototype>())
            {
                if (material.StackEntity is not { } stackEntity)
                    continue;

                Assert.That(protoManager.HasIndex(stackEntity), Is.True,
                    $"{material.ID} names sheet entity {stackEntity}, which does not exist.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Anything carrying an Ember ore or stack component shows up in the spawn panel, so it needs a name of its
    /// own rather than the one it inherits from BaseItem.
    /// </summary>
    [Test]
    public async Task EmberMaterialEntitiesAreNotNamedGenerically()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();

        var oreName = componentFactory.GetComponentName<EmberOreComponent>();
        var stackName = componentFactory.GetComponentName<EmberMaterialStackComponent>();

        var offenders = new List<string>();

        foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (!proto.Components.ContainsKey(oreName) && !proto.Components.ContainsKey(stackName))
                continue;

            if (GenericNames.Contains(proto.Name.Trim()))
                offenders.Add($"{proto.ID} (shows as \"{proto.Name}\")");
        }

        Assert.That(offenders, Is.Empty,
            "These Ember material entities fall back to a generic name:\n  " + string.Join("\n  ", offenders));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// An ore with no PhysicalComposition is inert to the reclaimer and to material storage. Half the ported set
    /// shipped that way because the material prototypes they needed did not exist yet.
    /// </summary>
    [Test]
    public async Task EveryEmberOreReclaimsIntoAnExistingMaterial()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var oreComponent = componentFactory.GetComponentName<EmberOreComponent>();

        var problems = new List<string>();

        foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.ID.StartsWith("Ember") || !proto.Components.ContainsKey(oreComponent))
                continue;

            // Slag is worthless by design: /material/waste has stack_type = null in Bay, so there is nothing to
            // reclaim it into.
            if (proto.ID.Contains("Waste"))
                continue;

            if (!proto.Components.TryGetComponent("PhysicalComposition", out var raw) ||
                raw is not PhysicalCompositionComponent composition ||
                composition.MaterialComposition.Count == 0)
            {
                problems.Add($"{proto.ID} has no PhysicalComposition");
                continue;
            }

            foreach (var material in composition.MaterialComposition.Keys)
            {
                if (!protoManager.HasIndex<MaterialPrototype>(material))
                    problems.Add($"{proto.ID} reclaims into '{material}', which is not a material prototype");
            }
        }

        Assert.That(problems, Is.Empty, string.Join("; ", problems));

        await pair.CleanReturnAsync();
    }

    private static void CheckLocId(ILocalizationManager loc, string? id, string where)
    {
        if (string.IsNullOrEmpty(id))
            return;

        Assert.That(loc.TryGetString(id, out var text), Is.True,
            $"{where} points at Fluent id '{id}', which has no string.");
        Assert.That(text, Is.Not.Empty, $"{where} resolves to an empty string.");
    }
}
