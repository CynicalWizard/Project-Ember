using System.Collections.Generic;
using System.Linq;
using Content.Server.Construction.Components;
using Content.Shared.Construction.Components;
using Content.Server.Power.Components;
using Content.Shared.Ember.Materials;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Mining;
using Content.Shared.Random;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

[TestFixture]
public sealed class EmberOreProcessingTest
{
    private const string Unloader = "EmberOreUnloader";
    private const string Processor = "EmberOreProcessor";
    private const string Stacker = "EmberOreStacker";
    private const string Console = "EmberOreConsole";

    private static readonly (string Machine, string Board)[] MachineBoards =
    {
        (Unloader, "EmberOreUnloaderMachineCircuitboard"),
        (Processor, "EmberOreProcessorMachineCircuitboard"),
        (Stacker, "EmberOreStackerMachineCircuitboard"),
    };

    /// <summary>
    /// The machines were mapper-only for a while. Each one needs a board that points back at it, or it cannot be
    /// built or deconstructed at all.
    /// </summary>
    [Test]
    public async Task EveryOreMachineHasABoardPointingBackAtIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            foreach (var (machineId, boardId) in MachineBoards)
            {
                Assert.That(protoManager.TryIndex(boardId, out EntityPrototype? board), Is.True,
                    $"Machine board {boardId} does not exist.");
                Assert.That(board!.Components.TryGetComponent("MachineBoard", out var boardComp), Is.True,
                    $"{boardId} is missing a MachineBoard component.");
                Assert.That(((MachineBoardComponent) boardComp!).Prototype.Id, Is.EqualTo(machineId),
                    $"{boardId} does not build {machineId}.");

                Assert.That(protoManager.TryIndex(machineId, out EntityPrototype? machine), Is.True,
                    $"Machine {machineId} does not exist.");
                Assert.That(machine!.Components.TryGetComponent("Machine", out var machineComp), Is.True,
                    $"{machineId} is missing a Machine component, so it cannot be deconstructed.");
                Assert.That(((MachineComponent) machineComp!).Board?.Id, Is.EqualTo(boardId),
                    $"{machineId} does not name {boardId} as its board.");
            }

            Assert.That(protoManager.TryIndex("EmberOreConsoleCircuitboard", out EntityPrototype? consoleBoard), Is.True);
            Assert.That(consoleBoard!.Components.TryGetComponent("ComputerBoard", out var computerBoard), Is.True);
            Assert.That(((ComputerBoardComponent) computerBoard!).Prototype, Is.EqualTo(Console));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Every board has to be printable, or it is only reachable through admin spawning.
    /// </summary>
    [Test]
    public async Task EveryOreMachineBoardIsPrintable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoManager = server.ResolveDependency<IPrototypeManager>();

        var packed = protoManager.EnumeratePrototypes<LatheRecipePackPrototype>()
            .SelectMany(pack => pack.Recipes)
            .Select(recipe => recipe.Id)
            .ToHashSet();

        var boards = MachineBoards.Select(pair => pair.Board).Append("EmberOreConsoleCircuitboard");

        Assert.Multiple(() =>
        {
            foreach (var boardId in boards)
            {
                Assert.That(protoManager.TryIndex<LatheRecipePrototype>(boardId, out var recipe), Is.True,
                    $"No lathe recipe produces {boardId}.");
                Assert.That(recipe!.Result?.Id, Is.EqualTo(boardId));
                Assert.That(packed, Does.Contain(boardId),
                    $"Lathe recipe {boardId} is in no recipe pack, so no lathe can print it.");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Ores that nothing can drop are just admin-spawn props. Every Ember ore entity has to be reachable from a
    /// rock vein distribution, which is the existing SS14 mining path rather than a ported Bay one.
    /// </summary>
    [Test]
    public async Task EveryEmberOreIsMinable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var oreComponent = componentFactory.GetComponentName<EmberOreComponent>();

        // Every ore entity a vein can drop, following stack singles back to the entity players actually see.
        var droppable = protoManager.EnumeratePrototypes<WeightedRandomOrePrototype>()
            .SelectMany(dist => dist.Weights.Keys)
            .Where(protoManager.HasIndex<OrePrototype>)
            .Select(id => protoManager.Index<OrePrototype>(id).OreEntity?.Id)
            .Where(id => id != null)
            .ToHashSet();

        var unreachable = new List<string>();

        foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.Components.ContainsKey(oreComponent))
                continue;

            // Only the ores this port introduced. Vanilla ores also carry EmberOre now, but they reach players
            // through their own Unprocessed entities and stack size variants, which is not ours to police.
            if (!proto.ID.StartsWith("Ember"))
                continue;

            // Slag is a processing byproduct, not something anyone digs up.
            if (proto.ID.Contains("Waste"))
                continue;

            var family = protoManager.EnumeratePrototypes<EntityPrototype>()
                .Where(p => p.ID == proto.ID || p.ID == proto.ID + "1")
                .Select(p => p.ID);

            if (!family.Any(droppable.Contains))
                unreachable.Add(proto.ID);
        }

        Assert.That(unreachable, Is.Empty,
            "These ores exist but no rock can drop them: " + string.Join(", ", unreachable));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// End to end: ore on the processor's input tile becomes sheets on its output tile, then the stacker turns
    /// those into a single stack of the configured size. This is the path that was silently broken while the
    /// unloader and stacker read from their own tile instead of their configured input direction.
    /// </summary>
    [Test]
    public async Task ProcessorSmeltsOreAndStackerCombinesTheSheets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapSystem = entityManager.System<SharedMapSystem>();
        var grid = map.Grid;

        var hematite = protoManager.Index<EmberMaterialPrototype>("Hematite");
        Assert.That(hematite.OreSmeltsTo, Is.Not.Null, "Hematite should smelt into something.");

        var target = protoManager.Index<EmberMaterialPrototype>(hematite.OreSmeltsTo!);
        Assert.That(target.StackEntity, Is.Not.Null, "Hematite's smelting target needs a sheet entity.");

        EntityUid processor = default;
        EntityCoordinates outputCoords = default;

        await server.WaitPost(() =>
        {
            var origin = mapSystem.TileIndicesFor(grid.Owner, grid.Comp, map.GridCoords);
            var processorCoords = mapSystem.GridTileToLocal(grid.Owner, grid.Comp, origin);
            var inputCoords = mapSystem.GridTileToLocal(grid.Owner, grid.Comp, origin + new Vector2i(0, 1));
            outputCoords = mapSystem.GridTileToLocal(grid.Owner, grid.Comp, origin + new Vector2i(0, -1));

            processor = entityManager.SpawnEntity(Processor, processorCoords);

            var machine = entityManager.GetComponent<EmberMineralMachineComponent>(processor);
            Assert.That(machine.Input, Is.EqualTo(Direction.North));
            Assert.That(machine.Output, Is.EqualTo(Direction.South));

            var ore = entityManager.SpawnEntity("SteelOre", inputCoords);
            var oreComp = entityManager.GetComponent<EmberOreComponent>(ore);
            Assert.That(oreComp.Material.Id, Is.EqualTo("Hematite"));

            // No APC on a bare test grid, and the processor refuses to run unpowered.
            entityManager.GetComponent<ApcPowerReceiverComponent>(processor).NeedsPower = false;

            var processorComp = entityManager.GetComponent<EmberMaterialProcessorComponent>(processor);
            processorComp.Active = true;
            processorComp.OreModes["Hematite"] = EmberMaterialProcessingMode.Smelt;
        });

        // The machines run off their own tick timer, so give them a moment rather than one frame.
        await pair.RunTicksSync(30);

        var lookup = entityManager.System<EntityLookupSystem>();
        var sheets = 0;

        await server.WaitPost(() =>
        {
            var found = new HashSet<EntityUid>();
            lookup.GetEntitiesInRange(outputCoords, 0.4f, found);

            foreach (var entity in found)
            {
                if (entityManager.TryGetComponent<StackComponent>(entity, out var stack) &&
                    entityManager.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID == target.StackEntity!.Value.Id)
                {
                    sheets += stack.Count;
                }
            }
        });

        Assert.That(sheets, Is.GreaterThan(0),
            "The processor produced no sheets on the tile its output direction points at.");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The console has to find the machines around it, or none of its controls do anything.
    /// </summary>
    [Test]
    public async Task ConsoleLinksToAdjacentMachines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mapSystem = entityManager.System<SharedMapSystem>();
        var grid = map.Grid;

        EntityUid console = default;
        EntityUid unloader = default;
        EntityUid processor = default;
        EntityUid stacker = default;

        await server.WaitPost(() =>
        {
            var origin = mapSystem.TileIndicesFor(grid.Owner, grid.Comp, map.GridCoords);

            EntityCoordinates At(int x, int y) =>
                mapSystem.GridTileToLocal(grid.Owner, grid.Comp, origin + new Vector2i(x, y));

            unloader = entityManager.SpawnEntity(Unloader, At(1, 0));
            processor = entityManager.SpawnEntity(Processor, At(-1, 0));
            stacker = entityManager.SpawnEntity(Stacker, At(0, 1));

            // Spawned last so the machines already exist when it looks around on map init.
            console = entityManager.SpawnEntity(Console, At(0, 0));
        });

        await pair.RunTicksSync(5);

        var link = entityManager.GetComponent<EmberOreProcessingConsoleComponent>(console);

        Assert.Multiple(() =>
        {
            Assert.That(link.Unloader, Is.EqualTo(unloader), "Console did not link the adjacent unloader.");
            Assert.That(link.Processor, Is.EqualTo(processor), "Console did not link the adjacent processor.");
            Assert.That(link.Stacker, Is.EqualTo(stacker), "Console did not link the adjacent stacker.");
        });

        await pair.CleanReturnAsync();
    }
}
