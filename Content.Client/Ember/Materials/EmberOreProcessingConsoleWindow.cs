using System.Linq;
using System.Numerics;
using Content.Shared.Ember.Materials;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Ember.Materials;

public sealed class EmberOreProcessingConsoleWindow : DefaultWindow
{
    private static readonly Direction?[] Directions =
    {
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
        null,
    };

    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private HashSet<string>? _alloyIngredients;

    private static readonly Color DisabledPanelTint = new(0.6f, 0.6f, 0.6f);

    private readonly MachinePanel _unloader;
    private readonly MachinePanel _processor;
    private readonly MachinePanel _stacker;
    private bool _updating;

    public event Action? OnRelink;
    public event Action<EmberOreMachineKind, Direction?>? OnInputChanged;
    public event Action<EmberOreMachineKind, Direction?>? OnOutputChanged;
    public event Action? OnToggleProcessor;
    public event Action<EmberOreConsolePreset>? OnPreset;
    public event Action<string, EmberMaterialProcessingMode>? OnOreModeChanged;
    public event Action<int>? OnStackAmountChanged;
    public event Action<string>? OnReleaseStack;

    public EmberOreProcessingConsoleWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("ember-ore-processing-window-title");
        MinSize = new Vector2(520, 560);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };
        Contents.AddChild(root);

        var relink = new Button { Text = Loc.GetString("ember-ore-processing-window-reconnect") };
        relink.OnPressed += _ => OnRelink?.Invoke();
        root.AddChild(relink);

        _unloader = BuildMachinePanel(root, EmberOreMachineKind.Unloader, "ember-ore-processing-window-unloader");
        _processor = BuildMachinePanel(root, EmberOreMachineKind.Processor, "ember-ore-processing-window-processor");
        _stacker = BuildMachinePanel(root, EmberOreMachineKind.Stacker, "ember-ore-processing-window-stacker");

        _processor.ToggleProcessor = new Button();
        _processor.ToggleProcessor.OnPressed += _ => OnToggleProcessor?.Invoke();
        _processor.Body.AddChild(_processor.ToggleProcessor);

        var presetBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
        };
        _processor.Body.AddChild(presetBox);

        AddPresetButton(presetBox, "ember-ore-processing-window-preset-auto", EmberOreConsolePreset.Automatic);
        AddPresetButton(presetBox, "ember-ore-processing-window-preset-alloy", EmberOreConsolePreset.Alloy);
        AddPresetButton(presetBox, "ember-ore-processing-window-preset-disable", EmberOreConsolePreset.Disabled);

        _processor.OreList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
        };
        _processor.Body.AddChild(_processor.OreList);

        var amountBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        _stacker.Body.AddChild(amountBox);
        amountBox.AddChild(new Label { Text = Loc.GetString("ember-ore-processing-window-stack-amount") });
        _stacker.StackAmount = new OptionButton();
        foreach (var amount in new[] { 1, 5, 10, 20, 30, 50, 60 })
            _stacker.StackAmount.AddItem(amount.ToString(), amount);
        _stacker.StackAmount.OnItemSelected += args =>
        {
            if (_updating)
                return;

            _stacker.StackAmount.SelectId(args.Id);
            OnStackAmountChanged?.Invoke(args.Id);
        };
        amountBox.AddChild(_stacker.StackAmount);

        _stacker.StoredStacks = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
        };
        _stacker.Body.AddChild(_stacker.StoredStacks);
    }

    public void UpdateState(EmberOreProcessingConsoleState state)
    {
        _updating = true;
        UpdateMachine(_unloader, state.Unloader);
        UpdateMachine(_processor, state.Processor);
        UpdateMachine(_stacker, state.Stacker);

        _processor.ToggleProcessor!.Text = Loc.GetString(state.Processor.ProcessorActive
            ? "ember-ore-processing-window-stop-processor"
            : "ember-ore-processing-window-start-processor");
        RebuildOres(state.Processor);

        _stacker.StackAmount!.SelectId(state.Stacker.StackAmount);
        RebuildStacks(state.Stacker);
        _updating = false;
    }

    private MachinePanel BuildMachinePanel(Control parent, EmberOreMachineKind kind, string title)
    {
        var panel = new PanelContainer();
        parent.AddChild(panel);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            Margin = new Thickness(6),
        };
        panel.AddChild(body);

        body.AddChild(new Label { Text = Loc.GetString(title) });

        var status = new Label();
        body.AddChild(status);

        var directionBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };
        body.AddChild(directionBox);

        directionBox.AddChild(new Label { Text = Loc.GetString("ember-ore-processing-window-input") });
        var input = BuildDirectionButton();
        input.OnItemSelected += args =>
        {
            if (_updating)
                return;

            input.SelectId(args.Id);
            OnInputChanged?.Invoke(kind, Directions[args.Id]);
        };
        directionBox.AddChild(input);

        directionBox.AddChild(new Label { Text = Loc.GetString("ember-ore-processing-window-output") });
        var output = BuildDirectionButton();
        output.OnItemSelected += args =>
        {
            if (_updating)
                return;

            output.SelectId(args.Id);
            OnOutputChanged?.Invoke(kind, Directions[args.Id]);
        };
        directionBox.AddChild(output);

        return new MachinePanel(kind, body, status, input, output);
    }

    private void UpdateMachine(MachinePanel panel, EmberOreMachineConsoleState state)
    {
        var linked = state.ConnectedName != null;

        panel.Status.Text = linked
            ? Loc.GetString("ember-ore-processing-window-linked-machine", ("machine", state.ConnectedName!))
            : Loc.GetString("ember-ore-processing-window-no-linked-machine");

        panel.Input.SelectId(DirectionToId(state.Input));
        panel.Output.SelectId(DirectionToId(state.Output));

        // Nothing in a panel does anything without a machine behind it, so make that visible rather than
        // letting the player click dead controls.
        panel.Input.Disabled = !linked;
        panel.Output.Disabled = !linked;
        panel.Body.Modulate = linked ? Color.White : DisabledPanelTint;

        if (panel.ToggleProcessor != null)
            panel.ToggleProcessor.Disabled = !linked;

        if (panel.StackAmount != null)
            panel.StackAmount.Disabled = !linked;
    }

    private void RebuildOres(EmberOreMachineConsoleState state)
    {
        var list = _processor.OreList!;
        var keys = state.StoredOres.Keys.OrderBy(key => key).ToArray();

        if (_processor.OreRows.Count != keys.Length ||
            !_processor.OreRows.Keys.OrderBy(key => key).SequenceEqual(keys))
        {
            list.RemoveAllChildren();
            _processor.OreRows.Clear();

            if (keys.Length == 0)
            {
                list.AddChild(new Label { Text = Loc.GetString("ember-ore-processing-window-no-stored-ore") });
                return;
            }

            foreach (var material in keys)
                AddOreRow(list, material);
        }

        foreach (var material in keys)
        {
            var row = _processor.OreRows[material];
            row.Label.Text = Loc.GetString("ember-ore-processing-window-material-entry",
                ("material", MaterialName(material)),
                ("amount", state.StoredOres[material]));
            // TrySelectId, not SelectId: a mode the material cannot use was never added to the dropdown.
            row.Mode.TrySelectId((int) state.OreModes.GetValueOrDefault(material, EmberMaterialProcessingMode.Disabled));
        }
    }

    private void AddOreRow(BoxContainer list, string material)
    {
        var supported = SupportedModes(material);

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };

        var label = new Label { HorizontalExpand = true };
        row.AddChild(label);

        var modes = new OptionButton();
        AddMode(modes, "ember-ore-processing-mode-disabled", EmberMaterialProcessingMode.Disabled);

        if ((supported & EmberMaterialProcessingModes.Smelt) != 0)
            AddMode(modes, "ember-ore-processing-mode-smelt", EmberMaterialProcessingMode.Smelt);

        if ((supported & EmberMaterialProcessingModes.Compress) != 0)
            AddMode(modes, "ember-ore-processing-mode-compress", EmberMaterialProcessingMode.Compress);

        if ((supported & EmberMaterialProcessingModes.Alloy) != 0)
            AddMode(modes, "ember-ore-processing-mode-alloy", EmberMaterialProcessingMode.Alloy);

        // Nothing but "off" to pick from means this is a dead-end material; say so rather than showing a
        // dropdown that does nothing.
        modes.Disabled = supported == EmberMaterialProcessingModes.None;
        if (modes.Disabled)
            modes.ToolTip = Loc.GetString("ember-ore-processing-window-no-modes");

        modes.OnItemSelected += args =>
        {
            modes.SelectId(args.Id);
            OnOreModeChanged?.Invoke(material, (EmberMaterialProcessingMode) args.Id);
        };
        row.AddChild(modes);

        list.AddChild(row);
        _processor.OreRows[material] = new MaterialRow(label, modes);
    }

    private void RebuildStacks(EmberOreMachineConsoleState state)
    {
        var list = _stacker.StoredStacks!;
        var keys = state.StoredMaterials.Keys.OrderBy(MaterialName).ToArray();

        // Only tear the list down when the set of materials actually changed. Rebuilding it every state update
        // made the buttons flicker and drop the cursor mid-click.
        if (_stacker.StackRows.Count != keys.Length ||
            !_stacker.StackRows.Keys.OrderBy(MaterialName).SequenceEqual(keys))
        {
            list.RemoveAllChildren();
            _stacker.StackRows.Clear();

            if (keys.Length == 0)
            {
                list.AddChild(new Label { Text = Loc.GetString("ember-ore-processing-window-no-stored-sheets") });
                _stacker.ReleaseAll = null;
                return;
            }

            foreach (var material in keys)
                AddStackRow(list, material);

            _stacker.ReleaseAll = new Button { Text = Loc.GetString("ember-ore-processing-window-release-all") };
            _stacker.ReleaseAll.OnPressed += _ =>
            {
                foreach (var material in _stacker.StackRows.Keys.ToArray())
                    OnReleaseStack?.Invoke(material);
            };
            list.AddChild(_stacker.ReleaseAll);
        }

        foreach (var material in keys)
        {
            var amount = state.StoredMaterials[material];
            var row = _stacker.StackRows[material];

            // Showing the threshold makes it obvious why a pile is sitting there instead of popping out.
            row.Label.Text = Loc.GetString("ember-ore-processing-window-stack-entry",
                ("material", MaterialName(material)),
                ("amount", amount),
                ("threshold", state.StackAmount));
            row.Release.Disabled = amount <= 0;
        }
    }

    private void AddStackRow(BoxContainer list, string material)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = true,
        };

        var label = new Label { HorizontalExpand = true };
        row.AddChild(label);

        var release = new Button { Text = Loc.GetString("ember-ore-processing-window-release") };
        release.OnPressed += _ => OnReleaseStack?.Invoke(material);
        row.AddChild(release);

        list.AddChild(row);
        _stacker.StackRows[material] = new StackRow(label, release);
    }

    /// <summary>
    /// The console state carries prototype ids; players should see the material's own name.
    /// </summary>
    private string MaterialName(string material)
    {
        return _prototype.TryIndex(material, out EmberMaterialPrototype? proto) && proto.DisplayName != string.Empty
            ? Loc.GetString(proto.DisplayName)
            : material;
    }

    private EmberMaterialProcessingModes SupportedModes(string material)
    {
        if (!_prototype.TryIndex(material, out EmberMaterialPrototype? proto))
            return EmberMaterialProcessingModes.None;

        _alloyIngredients ??= EmberMaterialProcessing.GetAlloyIngredients(
            _prototype.EnumeratePrototypes<EmberMaterialPrototype>());

        return EmberMaterialProcessing.SupportedModes(proto, _alloyIngredients);
    }

    private static OptionButton BuildDirectionButton()
    {
        var button = new OptionButton();
        for (var i = 0; i < Directions.Length; i++)
            button.AddItem(DirectionName(Directions[i]), i);

        return button;
    }

    private void AddPresetButton(BoxContainer parent, string text, EmberOreConsolePreset preset)
    {
        var button = new Button { Text = Loc.GetString(text) };
        button.OnPressed += _ => OnPreset?.Invoke(preset);
        parent.AddChild(button);
    }

    private static void AddMode(OptionButton button, string name, EmberMaterialProcessingMode mode)
    {
        button.AddItem(Loc.GetString(name), (int) mode);
    }

    private static int DirectionToId(Direction? direction)
    {
        for (var i = 0; i < Directions.Length; i++)
        {
            if (Directions[i] == direction)
                return i;
        }

        return Directions.Length - 1;
    }

    private static string DirectionName(Direction? direction)
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

    private sealed class MachinePanel(
        EmberOreMachineKind kind,
        BoxContainer body,
        Label status,
        OptionButton input,
        OptionButton output)
    {
        public EmberOreMachineKind Kind { get; } = kind;
        public BoxContainer Body { get; } = body;
        public Label Status { get; } = status;
        public OptionButton Input { get; } = input;
        public OptionButton Output { get; } = output;
        public Button? ToggleProcessor;
        public OptionButton? StackAmount;
        public BoxContainer? OreList;
        public BoxContainer? StoredStacks;
        public Button? ReleaseAll;
        public readonly Dictionary<string, MaterialRow> OreRows = new();
        public readonly Dictionary<string, StackRow> StackRows = new();
    }

    private sealed record MaterialRow(Label Label, OptionButton Mode);

    private sealed record StackRow(Label Label, Button Release);
}
