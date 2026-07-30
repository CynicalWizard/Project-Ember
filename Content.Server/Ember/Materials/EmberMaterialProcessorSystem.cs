using Content.Server.Stack;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.Ember.Materials;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Ember.Materials;

public sealed class EmberMaterialProcessorSystem : EntitySystem
{
    private static readonly Direction[] CardinalDirections =
    {
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    };

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly HashSet<EntityUid> _entities = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<EmberMaterialProcessorComponent, ComponentStartup>(OnProcessorStartup);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, ActivateInWorldEvent>(OnConsoleActivate);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, GetVerbsEvent<InteractionVerb>>(OnConsoleGetVerbs);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeConsoleOpen);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, InteractUsingEvent>(OnConsoleInteractUsing);
        SubscribeLocalEvent<EmberMineralMachineComponent, InteractUsingEvent>(OnMachineInteractUsing);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, EmberOreConsoleRelinkMessage>(OnConsoleRelink);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, EmberOreConsoleSetDirectionMessage>(OnConsoleSetDirection);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, EmberOreConsoleToggleProcessorMessage>(OnConsoleToggleProcessor);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, EmberOreConsoleSetProcessorModeMessage>(OnConsoleSetProcessorMode);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, EmberOreConsolePresetModesMessage>(OnConsolePresetModes);
        SubscribeLocalEvent<EmberOreProcessingConsoleComponent, EmberOreConsoleSetStackAmountMessage>(OnConsoleSetStackAmount);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        _stackTypeCache.Clear();
    }

    private void OnProcessorStartup(EntityUid uid, EmberMaterialProcessorComponent component, ComponentStartup args)
    {
        UpdateProcessorAppearance(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var unloaderQuery = EntityQueryEnumerator<EmberMineralMachineComponent, EmberOreUnloaderComponent>();
        while (unloaderQuery.MoveNext(out var uid, out var machine, out var unloader))
        {
            if (machine.NextProcess > now)
                continue;

            machine.NextProcess = now + machine.ProcessDelay;
            ProcessUnloader(uid, machine, unloader);
        }

        var processorQuery = EntityQueryEnumerator<EmberMineralMachineComponent, EmberMaterialProcessorComponent>();
        while (processorQuery.MoveNext(out var uid, out var machine, out var processor))
        {
            if (machine.NextProcess > now)
                continue;

            machine.NextProcess = now + machine.ProcessDelay;
            ProcessProcessor(uid, machine, processor);
        }

        var stackerQuery = EntityQueryEnumerator<EmberMineralMachineComponent, EmberOreStackerComponent>();
        while (stackerQuery.MoveNext(out var uid, out var machine, out var stacker))
        {
            if (machine.NextProcess > now)
                continue;

            machine.NextProcess = now + machine.ProcessDelay;
            ProcessStacker(uid, machine, stacker);
        }
    }

    private void ProcessUnloader(EntityUid uid, EmberMineralMachineComponent machine, EmberOreUnloaderComponent unloader)
    {
        if (!_power.IsPowered(uid) ||
            machine.Input == null ||
            machine.Output == null ||
            TryGetMachineTile(uid) is not {} input ||
            TryGetAdjacent(uid, machine.Output.Value) is not {} output ||
            CountTileContents(output) >= unloader.MaxOutputContents)
        {
            return;
        }

        var remaining = unloader.OrePerTick;
        foreach (var entity in GetTileEntities(input))
        {
            if (remaining <= 0)
                return;

            if (TryComp<StorageComponent>(entity, out var storage))
            {
                UnloadStorage(entity, storage, output, ref remaining);
                continue;
            }

            TryMoveOre(entity, output, ref remaining);
        }
    }

    private void ProcessProcessor(EntityUid uid, EmberMineralMachineComponent machine, EmberMaterialProcessorComponent processor)
    {
        if (!processor.Active || !_power.IsPowered(uid))
            return;

        if (machine.Input != null && TryGetMachineTile(uid) is {} input)
        {
            foreach (var entity in GetMachineInputEntities(uid, input))
            {
                ConsumeOre(uid, entity, processor);
            }
        }

        if (machine.Output == null || TryGetAdjacent(uid, machine.Output.Value) is not {} output)
            return;

        var stored = new Dictionary<string, int>();
        var modes = new Dictionary<string, EmberMaterialProcessingMode>();

        foreach (var (material, amount) in processor.StoredOres)
        {
            stored[material.Id] = amount;
        }

        foreach (var (material, mode) in processor.OreModes)
        {
            modes[material.Id] = mode;
        }

        var result = EmberMaterialProcessing.ProcessTick(
            _prototype.EnumeratePrototypes<EmberMaterialPrototype>(),
            stored,
            modes,
            processor.SheetsPerTick);

        foreach (var (material, amount) in stored)
        {
            var id = new ProtoId<EmberMaterialPrototype>(material);
            processor.StoredOres[id] = amount;
        }

        foreach (var material in new List<ProtoId<EmberMaterialPrototype>>(processor.StoredOres.Keys))
        {
            if (processor.StoredOres[material] <= 0)
                processor.StoredOres.Remove(material);
        }

        foreach (var produced in result.Outputs)
        {
            if (produced.ProducedWaste)
            {
                _stack.SpawnMultiple("EmberWasteOre", produced.Sheets, output);
                continue;
            }

            if (produced.Material == null ||
                !_prototype.TryIndex<EmberMaterialPrototype>(produced.Material, out var material) ||
                material.StackEntity == null)
            {
                continue;
            }

            _stack.SpawnMultiple(material.StackEntity.Value, produced.Sheets, output);
        }

        UpdateConsoleUisForTarget(uid);
    }

    private void ProcessStacker(EntityUid uid, EmberMineralMachineComponent machine, EmberOreStackerComponent stacker)
    {
        if (!_power.IsPowered(uid))
            return;

        if (machine.Input != null && TryGetMachineTile(uid) is {} input)
        {
            foreach (var entity in GetMachineInputEntities(uid, input))
            {
                if (!CanHandleLooseItem(uid, entity))
                    continue;

                if (!TryComp<StackComponent>(entity, out var stack) ||
                    !TryGetMaterialByStackType(stack.StackTypeId, out var material))
                {
                    if (machine.Output != null && TryGetAdjacent(uid, machine.Output.Value) is {} fallbackOutput)
                        _transform.SetCoordinates(entity, fallbackOutput);

                    continue;
                }

                stacker.StoredMaterials.TryAdd(material.ID, 0);
                stacker.StoredMaterials[material.ID] += stack.Count;
                QueueDel(entity);
            }
        }

        if (machine.Output == null || TryGetAdjacent(uid, machine.Output.Value) is not {} output)
            return;

        ReleaseStacks(stacker, output, stacker.StackAmount);
        UpdateConsoleUisForTarget(uid);
    }

    private void UnloadStorage(EntityUid storageUid, StorageComponent storage, EntityCoordinates output, ref int remaining)
    {
        foreach (var stored in new List<EntityUid>(storage.Container.ContainedEntities))
        {
            if (remaining <= 0)
                return;

            if (!HasComp<EmberOreComponent>(stored))
                continue;

            if (TryComp<StackComponent>(stored, out var stack) && stack.Count > remaining)
            {
                _stack.Split(stored, remaining, output, stack);
                remaining = 0;
                return;
            }

            var moved = TryComp<StackComponent>(stored, out stack)
                ? Math.Min(stack.Count, remaining)
                : 1;

            storage.StoredItems.Remove(stored);
            _container.Remove(stored, storage.Container, force: true, destination: output);
            remaining -= moved;
        }

        Dirty(storageUid, storage);
    }

    private void TryMoveOre(EntityUid uid, EntityCoordinates output, ref int remaining)
    {
        if (!CanHandleLooseItem(null, uid) || !HasComp<EmberOreComponent>(uid))
            return;

        if (TryComp<StackComponent>(uid, out var stack) && stack.Count > remaining)
        {
            _stack.Split(uid, remaining, output, stack);
            remaining = 0;
            return;
        }

        var moved = TryComp<StackComponent>(uid, out stack)
            ? Math.Min(stack.Count, remaining)
            : 1;

        _transform.SetCoordinates(uid, output);
        remaining -= moved;
    }

    private void ConsumeOre(EntityUid machine, EntityUid uid, EmberMaterialProcessorComponent processor)
    {
        if (!CanHandleLooseItem(machine, uid) ||
            !TryComp<EmberOreComponent>(uid, out var ore) ||
            !_prototype.TryIndex(ore.Material, out EmberMaterialPrototype? material))
        {
            return;
        }

        var count = TryComp<StackComponent>(uid, out var stack)
            ? stack.Count
            : 1;

        processor.StoredOres.TryAdd(ore.Material, 0);
        processor.StoredOres[ore.Material] += material.UnitsPerSheet * count;
        processor.OreModes.TryAdd(ore.Material, EmberMaterialProcessingMode.Disabled);
        QueueDel(uid);
    }

    private IEnumerable<EntityUid> GetMachineInputEntities(EntityUid machine, EntityCoordinates coords)
    {
        _entities.Clear();

        foreach (var entity in GetTileEntities(coords))
        {
            if (CanHandleLooseItem(machine, entity))
                _entities.Add(entity);
        }

        foreach (var entity in _lookup.GetEntitiesInRange(machine, 0.65f, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (CanHandleLooseItem(machine, entity))
                _entities.Add(entity);
        }

        return new List<EntityUid>(_entities);
    }

    private readonly Dictionary<string, EmberMaterialPrototype> _stackTypeCache = new();

    private void BuildStackTypeCache()
    {
        _stackTypeCache.Clear();
        foreach (var prototype in _prototype.EnumeratePrototypes<EmberMaterialPrototype>())
        {
            var key = prototype.StackType ?? prototype.ID;
            _stackTypeCache[key] = prototype;
        }
    }

    private bool TryGetMaterialByStackType(string stackType, out EmberMaterialPrototype material)
    {
        if (_stackTypeCache.Count == 0)
            BuildStackTypeCache();

        return _stackTypeCache.TryGetValue(stackType, out material!);
    }

    private void ReleaseStacks(EmberOreStackerComponent stacker, EntityCoordinates output, int amount)
    {
        foreach (var (materialId, stored) in new List<KeyValuePair<ProtoId<EmberMaterialPrototype>, int>>(stacker.StoredMaterials))
        {
            if (stored < amount || !_prototype.TryIndex(materialId, out EmberMaterialPrototype? material))
                continue;

            if (material.StackEntity == null)
            {
                continue;
            }

            _stack.SpawnMultiple(material.StackEntity.Value, amount, output);
            stacker.StoredMaterials[materialId] -= amount;

            if (stacker.StoredMaterials[materialId] <= 0)
                stacker.StoredMaterials.Remove(materialId);
        }
    }

    private IEnumerable<EntityUid> GetTileEntities(EntityCoordinates coords)
    {
        if (!coords.TryGetTileRef(out var tile, EntityManager) || tile == null)
            return Array.Empty<EntityUid>();

        _entities.Clear();
        _lookup.GetLocalEntitiesIntersecting(tile.Value.GridUid, tile.Value.GridIndices, _entities, 0f, LookupFlags.Dynamic | LookupFlags.Sundries);
        return new List<EntityUid>(_entities);
    }

    private int CountTileContents(EntityCoordinates coords)
    {
        var count = 0;
        foreach (var entity in GetTileEntities(coords))
        {
            if (HasComp<TransformComponent>(entity))
                count++;
        }

        return count;
    }

    private EntityCoordinates? TryGetAdjacent(EntityUid uid, Direction direction)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not {} gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return null;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates).Offset(direction);
        return _map.GridTileToLocal(gridUid, grid, tile);
    }

    private EntityCoordinates? TryGetMachineTile(EntityUid uid)
    {
        var xform = Transform(uid);
        return xform.GridUid == null ? null : xform.Coordinates;
    }

    private bool CanHandleLooseItem(EntityUid? machine, EntityUid entity)
    {
        if (machine != null && entity == machine.Value)
            return false;

        return HasComp<ItemComponent>(entity);
    }

    private void OnConsoleMapInit(EntityUid uid, EmberOreProcessingConsoleComponent component, MapInitEvent args)
    {
        FindConsoleTargets(uid, component);
    }

    private void OnConsoleActivate(EntityUid uid, EmberOreProcessingConsoleComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _ui.TryOpenUi(uid, EmberOreProcessingConsoleUiKey.Key, args.User);
    }

    private void OnBeforeConsoleOpen(EntityUid uid, EmberOreProcessingConsoleComponent component, BeforeActivatableUIOpenEvent args)
    {
        FindConsoleTargets(uid, component);
        UpdateConsoleUi(uid, component);
    }

    private void OnConsoleGetVerbs(EntityUid uid, EmberOreProcessingConsoleComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        FindConsoleTargets(uid, component);

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("ember-ore-processing-verb-relink"),
            Act = () =>
            {
                FindConsoleTargets(uid, component, true);
                UpdateConsoleUi(uid, component);
            },
        });

        AddMachineVerbs(args, component.Unloader);
        AddMachineVerbs(args, component.Processor);
        AddMachineVerbs(args, component.Stacker);
    }

    private void AddMachineVerbs(GetVerbsEvent<InteractionVerb> args, EntityUid? target)
    {
        if (target == null || !Exists(target.Value))
            return;

        if (TryComp<EmberMineralMachineComponent>(target.Value, out var machine))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("ember-ore-processing-verb-cycle-input",
                    ("direction", DirectionName(machine.Input))),
                Act = () => CycleInput(target.Value, machine),
            });
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("ember-ore-processing-verb-cycle-output",
                    ("direction", DirectionName(machine.Output))),
                Act = () => CycleOutput(target.Value, machine),
            });
        }

        if (TryComp<EmberMaterialProcessorComponent>(target.Value, out var processor))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString(processor.Active
                    ? "ember-ore-processing-verb-stop-processor"
                    : "ember-ore-processing-verb-start-processor"),
                Act = () =>
                {
            SetProcessorActive(target.Value, processor, !processor.Active);
                },
            });
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("ember-ore-processing-verb-auto"),
                Act = () => SetAutomaticModes(target.Value, processor),
            });
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("ember-ore-processing-verb-alloy"),
                Act = () => SetAlloyModes(target.Value, processor),
            });
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("ember-ore-processing-verb-disable"),
                Act = () => DisableModes(target.Value, processor),
            });
        }

        if (TryComp<EmberOreStackerComponent>(target.Value, out var stacker))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("ember-ore-processing-verb-cycle-stack-amount",
                    ("amount", stacker.StackAmount)),
                Act = () =>
                {
                    stacker.StackAmount = NextStackAmount(stacker.StackAmount);
                    UpdateConsoleUisForTarget(target.Value);
                },
            });
        }
    }

    private void FindConsoleTargets(EntityUid uid, EmberOreProcessingConsoleComponent component, bool force = false)
    {
        if (force)
        {
            component.Unloader = null;
            component.Processor = null;
            component.Stacker = null;
        }
        else
        {
            ClearInvalidTargets(component);
        }

        foreach (var entity in _lookup.GetEntitiesInRange(uid, 1.25f, LookupFlags.Static | LookupFlags.Dynamic))
        {
            if (entity == uid || !HasComp<EmberMineralMachineComponent>(entity))
                continue;

            AssignMachine(component, entity, false);
        }
    }

    private void ClearInvalidTargets(EmberOreProcessingConsoleComponent component)
    {
        if (component.Unloader != null && (!Exists(component.Unloader.Value) || !HasComp<EmberOreUnloaderComponent>(component.Unloader.Value)))
            component.Unloader = null;

        if (component.Processor != null && (!Exists(component.Processor.Value) || !HasComp<EmberMaterialProcessorComponent>(component.Processor.Value)))
            component.Processor = null;

        if (component.Stacker != null && (!Exists(component.Stacker.Value) || !HasComp<EmberOreStackerComponent>(component.Stacker.Value)))
            component.Stacker = null;
    }

    private void AssignMachine(EmberOreProcessingConsoleComponent component, EntityUid machine, bool replace)
    {
        if (HasComp<EmberOreUnloaderComponent>(machine))
            component.Unloader = replace ? machine : component.Unloader ?? machine;
        else if (HasComp<EmberMaterialProcessorComponent>(machine))
            component.Processor = replace ? machine : component.Processor ?? machine;
        else if (HasComp<EmberOreStackerComponent>(machine))
            component.Stacker = replace ? machine : component.Stacker ?? machine;
    }

    private EntityUid? GetConsoleTarget(EmberOreProcessingConsoleComponent component, EmberOreMachineKind kind)
    {
        ClearInvalidTargets(component);

        return kind switch
        {
            EmberOreMachineKind.Unloader => component.Unloader,
            EmberOreMachineKind.Processor => component.Processor,
            EmberOreMachineKind.Stacker => component.Stacker,
            _ => null,
        };
    }

    private void OnConsoleInteractUsing(EntityUid uid, EmberOreProcessingConsoleComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, "Multitool"))
            return;

        var linker = EnsureComp<EmberOreProcessingLinkerComponent>(args.Used);
        linker.Console = uid;
        TryFinishMultitoolLink(args.Used, linker, args.User);
        args.Handled = true;
    }

    private void OnMachineInteractUsing(EntityUid uid, EmberMineralMachineComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !_tag.HasTag(args.Used, "Multitool"))
            return;

        var linker = EnsureComp<EmberOreProcessingLinkerComponent>(args.Used);
        linker.Machine = uid;
        TryFinishMultitoolLink(args.Used, linker, args.User);
        args.Handled = true;
    }

    private void TryFinishMultitoolLink(EntityUid tool, EmberOreProcessingLinkerComponent linker, EntityUid user)
    {
        if (linker.Console is not {} console || linker.Machine is not {} machine)
        {
            _popup.PopupEntity(Loc.GetString("ember-ore-processing-link-buffered"), tool, user, PopupType.Medium);
            return;
        }

        if (!Exists(console) ||
            !Exists(machine) ||
            !TryComp<EmberOreProcessingConsoleComponent>(console, out var consoleComp) ||
            !HasComp<EmberMineralMachineComponent>(machine))
        {
            linker.Console = null;
            linker.Machine = null;
            _popup.PopupEntity(Loc.GetString("ember-ore-processing-link-invalid"), tool, user, PopupType.SmallCaution);
            return;
        }

        AssignMachine(consoleComp, machine, true);
        UpdateConsoleUi(console, consoleComp);
        _popup.PopupEntity(Loc.GetString("ember-ore-processing-link-complete", ("machine", Name(machine))),
            console,
            user,
            PopupType.Medium);
    }

    private void OnConsoleRelink(EntityUid uid, EmberOreProcessingConsoleComponent component, EmberOreConsoleRelinkMessage args)
    {
        FindConsoleTargets(uid, component, true);
        UpdateConsoleUi(uid, component);
    }

    private void OnConsoleSetDirection(EntityUid uid, EmberOreProcessingConsoleComponent component, EmberOreConsoleSetDirectionMessage args)
    {
        if (GetConsoleTarget(component, args.Kind) is not {} target ||
            !TryComp<EmberMineralMachineComponent>(target, out var machine))
            return;

        if (args.Input)
            machine.Input = args.Direction;
        else
            machine.Output = args.Direction;

        UpdateConsoleUi(uid, component);
    }

    private void OnConsoleToggleProcessor(EntityUid uid, EmberOreProcessingConsoleComponent component, EmberOreConsoleToggleProcessorMessage args)
    {
        if (GetConsoleTarget(component, EmberOreMachineKind.Processor) is not {} target ||
            !TryComp<EmberMaterialProcessorComponent>(target, out var processor))
            return;

        SetProcessorActive(target, processor, !processor.Active);
        UpdateConsoleUi(uid, component);
    }

    private void SetProcessorActive(EntityUid uid, EmberMaterialProcessorComponent processor, bool active)
    {
        processor.Active = active;
        UpdateProcessorAppearance(uid, processor);
        UpdateConsoleUisForTarget(uid);
    }

    private void UpdateProcessorAppearance(EntityUid uid, EmberMaterialProcessorComponent processor)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, EmberOreProcessorVisuals.Active, processor.Active, appearance);
    }

    private void OnConsoleSetProcessorMode(EntityUid uid, EmberOreProcessingConsoleComponent component, EmberOreConsoleSetProcessorModeMessage args)
    {
        if (GetConsoleTarget(component, EmberOreMachineKind.Processor) is not {} target ||
            !TryComp<EmberMaterialProcessorComponent>(target, out var processor) ||
            !_prototype.HasIndex<EmberMaterialPrototype>(args.Material))
            return;

        var id = new ProtoId<EmberMaterialPrototype>(args.Material);
        if (args.Mode == EmberMaterialProcessingMode.Disabled)
            processor.OreModes.Remove(id);
        else
            processor.OreModes[id] = args.Mode;

        UpdateConsoleUi(uid, component);
    }

    private void OnConsolePresetModes(EntityUid uid, EmberOreProcessingConsoleComponent component, EmberOreConsolePresetModesMessage args)
    {
        if (GetConsoleTarget(component, EmberOreMachineKind.Processor) is not {} target ||
            !TryComp<EmberMaterialProcessorComponent>(target, out var processor))
            return;

        switch (args.Preset)
        {
            case EmberOreConsolePreset.Automatic:
                SetAutomaticModes(target, processor);
                break;
            case EmberOreConsolePreset.Alloy:
                SetAlloyModes(target, processor);
                break;
            case EmberOreConsolePreset.Disabled:
                DisableModes(target, processor);
                break;
        }

        UpdateConsoleUi(uid, component);
    }

    private void OnConsoleSetStackAmount(EntityUid uid, EmberOreProcessingConsoleComponent component, EmberOreConsoleSetStackAmountMessage args)
    {
        if (GetConsoleTarget(component, EmberOreMachineKind.Stacker) is not {} target ||
            !TryComp<EmberOreStackerComponent>(target, out var stacker))
            return;

        stacker.StackAmount = Math.Clamp(args.Amount, 1, 60);
        UpdateConsoleUi(uid, component);
    }

    private void UpdateConsoleUi(EntityUid uid, EmberOreProcessingConsoleComponent component)
    {
        ClearInvalidTargets(component);
        _ui.SetUiState(uid, EmberOreProcessingConsoleUiKey.Key, new EmberOreProcessingConsoleState(
            BuildMachineState(component.Unloader),
            BuildMachineState(component.Processor),
            BuildMachineState(component.Stacker)));
    }

    private EmberOreMachineConsoleState BuildMachineState(EntityUid? target)
    {
        Direction? input = null;
        Direction? output = null;
        var processorActive = false;
        var stackAmount = 50;
        var storedOres = new Dictionary<string, int>();
        var storedMaterials = new Dictionary<string, int>();
        var oreModes = new Dictionary<string, EmberMaterialProcessingMode>();

        if (target != null)
        {
            if (TryComp<EmberMineralMachineComponent>(target.Value, out var machine))
            {
                input = machine.Input;
                output = machine.Output;
            }

            if (TryComp<EmberMaterialProcessorComponent>(target.Value, out var processor))
            {
                processorActive = processor.Active;

                foreach (var (material, amount) in processor.StoredOres)
                    storedOres[material.Id] = amount;

                foreach (var (material, mode) in processor.OreModes)
                    oreModes[material.Id] = mode;
            }

            if (TryComp<EmberOreStackerComponent>(target.Value, out var stacker))
            {
                stackAmount = stacker.StackAmount;

                foreach (var (material, amount) in stacker.StoredMaterials)
                    storedMaterials[material.Id] = amount;
            }
        }

        return new EmberOreMachineConsoleState(
            target == null ? null : Name(target.Value),
            target == null ? null : GetNetEntity(target.Value),
            input,
            output,
            processorActive,
            stackAmount,
            storedOres,
            storedMaterials,
            oreModes);
    }

    private void UpdateConsoleUisForTarget(EntityUid target)
    {
        var query = EntityQueryEnumerator<EmberOreProcessingConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Unloader == target || console.Processor == target || console.Stacker == target)
                UpdateConsoleUi(uid, console);
        }
    }

    private void SetAutomaticModes(EntityUid uid, EmberMaterialProcessorComponent processor)
    {
        processor.OreModes.Clear();
        foreach (var material in _prototype.EnumeratePrototypes<EmberMaterialPrototype>())
        {
            if (material.OreSmeltsTo != null)
                processor.OreModes[new ProtoId<EmberMaterialPrototype>(material.ID)] = EmberMaterialProcessingMode.Smelt;
            else if (material.OreCompressesTo != null)
                processor.OreModes[new ProtoId<EmberMaterialPrototype>(material.ID)] = EmberMaterialProcessingMode.Compress;
        }

        UpdateConsoleUisForTarget(uid);
    }

    private void SetAlloyModes(EntityUid uid, EmberMaterialProcessorComponent processor)
    {
        processor.OreModes.Clear();
        var alloyInputs = new HashSet<string>();
        foreach (var material in _prototype.EnumeratePrototypes<EmberMaterialPrototype>())
        {
            foreach (var input in material.AlloyMaterials.Keys)
            {
                alloyInputs.Add(input);
            }
        }

        foreach (var input in alloyInputs)
        {
            processor.OreModes[new ProtoId<EmberMaterialPrototype>(input)] = EmberMaterialProcessingMode.Alloy;
        }

        UpdateConsoleUisForTarget(uid);
    }

    private void DisableModes(EntityUid uid, EmberMaterialProcessorComponent processor)
    {
        processor.OreModes.Clear();
        UpdateConsoleUisForTarget(uid);
    }

    private void CycleInput(EntityUid uid, EmberMineralMachineComponent machine)
    {
        machine.Input = NextDirection(machine.Input);
        UpdateConsoleUisForTarget(uid);
    }

    private void CycleOutput(EntityUid uid, EmberMineralMachineComponent machine)
    {
        machine.Output = NextDirection(machine.Output);
        UpdateConsoleUisForTarget(uid);
    }

    private static Direction? NextDirection(Direction? current)
    {
        if (current == null)
            return CardinalDirections[0];

        for (var i = 0; i < CardinalDirections.Length; i++)
        {
            if (CardinalDirections[i] == current)
                return i == CardinalDirections.Length - 1 ? null : CardinalDirections[i + 1];
        }

        return null;
    }

    private string DirectionName(Direction? direction)
    {
        return Loc.GetString(direction switch
        {
            Direction.North => "ember-ore-processing-direction-north",
            Direction.East => "ember-ore-processing-direction-east",
            Direction.South => "ember-ore-processing-direction-south",
            Direction.West => "ember-ore-processing-direction-west",
            _ => "ember-ore-processing-direction-none",
        });
    }

    private static int NextStackAmount(int current)
    {
        return current switch
        {
            1 => 5,
            5 => 10,
            10 => 20,
            20 => 30,
            30 => 50,
            50 => 60,
            _ => 1,
        };
    }
}
