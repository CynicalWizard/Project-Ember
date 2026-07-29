using System.Linq;
using System.Numerics;
using Content.Shared.Ember.Materials;
using Robust.Client.UserInterface;
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

    public EmberOreProcessingConsoleWindow()
    {
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
        panel.Status.Text = state.ConnectedName == null
            ? Loc.GetString("ember-ore-processing-window-no-linked-machine")
            : Loc.GetString("ember-ore-processing-window-linked-machine", ("machine", state.ConnectedName));

        panel.Input.SelectId(DirectionToId(state.Input));
        panel.Output.SelectId(DirectionToId(state.Output));
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
                ("material", material),
                ("amount", state.StoredOres[material]));
            row.Mode.TrySelectId((int) state.OreModes.GetValueOrDefault(material, EmberMaterialProcessingMode.Disabled));
        }
    }

    private void AddOreRow(BoxContainer list, string material)
    {
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
        AddMode(modes, "ember-ore-processing-mode-smelt", EmberMaterialProcessingMode.Smelt);
        AddMode(modes, "ember-ore-processing-mode-compress", EmberMaterialProcessingMode.Compress);
        AddMode(modes, "ember-ore-processing-mode-alloy", EmberMaterialProcessingMode.Alloy);
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
        list.RemoveAllChildren();

        if (state.StoredMaterials.Count == 0)
        {
            list.AddChild(new Label { Text = Loc.GetString("ember-ore-processing-window-no-stored-sheets") });
            return;
        }

        foreach (var (material, amount) in state.StoredMaterials.OrderBy(pair => pair.Key))
        {
            list.AddChild(new Label
            {
                Text = Loc.GetString("ember-ore-processing-window-material-entry",
                    ("material", material),
                    ("amount", amount)),
            });
        }
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
        public readonly Dictionary<string, MaterialRow> OreRows = new();
    }

    private sealed record MaterialRow(Label Label, OptionButton Mode);
}
