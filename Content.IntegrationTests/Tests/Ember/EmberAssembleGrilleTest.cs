using System.Collections.Generic;
using Content.Shared.Ember.Materials;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Bay lets you put rods straight against a low wall to get a grille of the same metal, without going near a
/// build menu. That relies on three separate pieces of data lining up: the material knows which stack its rods
/// are, which grille they become, and the grille has to actually be made of that material.
/// </summary>
[TestFixture]
public sealed class EmberAssembleGrilleTest
{
    [Test]
    public async Task RodsAndGrillesAgreeOnTheirMaterial()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();

        var stackName = componentFactory.GetComponentName<EmberMaterialStackComponent>();
        var structureName = componentFactory.GetComponentName<EmberProceduralStructureComponent>();

        var problems = new List<string>();
        var found = 0;

        foreach (var material in protoManager.EnumeratePrototypes<EmberMaterialPrototype>())
        {
            if (material.RodStack is not { } rodStack)
            {
                if (material.GrilleEntity != null)
                    problems.Add($"{material.ID} names a grille but no rod stack to build it from");

                continue;
            }

            found++;

            if (!protoManager.TryIndex(rodStack, out StackPrototype? stack))
            {
                problems.Add($"{material.ID} names rod stack {rodStack}, which does not exist");
                continue;
            }

            // The system tells rods from sheets by the stack type, so the rods must carry the material too.
            if (protoManager.TryIndex(stack.Spawn, out EntityPrototype? rods))
            {
                if (!rods.Components.TryGetComponent(stackName, out var rawStack))
                    problems.Add($"{stack.Spawn} is not tied to a material, so it cannot be recognised as rods");
                else if (((EmberMaterialStackComponent) rawStack).Material != material.ID)
                    problems.Add($"{stack.Spawn} claims a different material than {material.ID}");
            }

            if (material.GrilleEntity is not { } grilleId)
            {
                problems.Add($"{material.ID} has rods but nothing for them to be assembled into");
                continue;
            }

            if (!protoManager.TryIndex(grilleId, out EntityPrototype? grille))
            {
                problems.Add($"{material.ID} names grille {grilleId}, which does not exist");
                continue;
            }

            if (!grille.Components.TryGetComponent(structureName, out var rawStructure))
            {
                problems.Add($"{grilleId} is not a procedural structure, so it will not take the metal's colour");
                continue;
            }

            var structure = (EmberProceduralStructureComponent) rawStructure;

            if (structure.Role != EmberProceduralStructureRole.Grille)
                problems.Add($"{grilleId} is a {structure.Role} rather than a grille");

            if (!protoManager.TryIndex(structure.Material, out EmberWallMaterialPrototype? wallMaterial) ||
                wallMaterial.PhysicalMaterial != material.ID)
            {
                problems.Add($"{grilleId} is built out of {structure.Material} but assembled from {material.ID} rods");
            }
        }

        Assert.That(found, Is.GreaterThan(0), "No material can be drawn into rods at all.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }
}
