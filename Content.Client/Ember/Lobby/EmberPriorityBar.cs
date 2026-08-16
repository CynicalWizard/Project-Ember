using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Ember.Lobby;

/// <summary>
/// An ordered preference - never, low, medium, high - as a row of cells with the choice named
/// beside them.
/// </summary>
/// <remarks>
/// Ember: the four priority buttons carried their words and a 90-pixel floor apiece, which came to
/// 360 pixels on every one of forty-four rows and left the posts' own names with what remained.
/// The words are what cost the room and the least of what they said: priority is an ordinal scale,
/// and a scale is read faster as a filled bar than as four labels of which one is highlighted.
///
/// Same shape as <see cref="Skills.SkillLevelBar"/> on purpose. Both are "how much of this", they
/// sit in the same section, and a player who has learnt to read one has learnt to read the other.
/// </remarks>
public sealed class EmberPriorityBar : BoxContainer
{
    private const int CellWidth = 26;
    private const int NameWidth = 80;

    private readonly List<(Button Button, int Value)> _cells = new();
    private readonly List<string> _labels = new();
    private readonly Label _name;

    /// <summary>
    /// What one of these measures, for whoever has to budget a row around it.
    /// </summary>
    public static int WidthFor(int optionCount) => CellWidth * optionCount + 8 + NameWidth;

    public int Selected { get; private set; }

    public event Action<int>? OnSelected;

    public EmberPriorityBar(IReadOnlyList<(string Label, int Value)> items)
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 0;

        for (var i = 0; i < items.Count; i++)
        {
            var (label, value) = items[i];
            _labels.Add(label);

            var button = new Button
            {
                ToggleMode = true,
                MinSize = new Vector2(CellWidth, 24),
                HorizontalExpand = false,
                ToolTip = label,
            };

            if (i == 0)
                button.AddStyleClass(StyleBase.ButtonOpenRight);
            else if (i == items.Count - 1)
                button.AddStyleClass(StyleBase.ButtonOpenLeft);
            else
                button.AddStyleClass(StyleBase.ButtonOpenBoth);

            button.OnPressed += _ =>
            {
                Select(value);
                OnSelected?.Invoke(value);
            };

            _cells.Add((button, value));
            AddChild(button);
        }

        AddChild(_name = new Label
        {
            MinWidth = NameWidth,
            Margin = new Thickness(8, 0, 0, 0),
            ClipText = true,
            StyleClasses = { StyleBase.StyleClassLabelSubText },
        });

        if (items.Count > 0)
            Select(items[0].Value);
    }

    /// <summary>
    /// Sets the choice without raising <see cref="OnSelected"/>.
    /// </summary>
    /// <remarks>
    /// Silent because the editor calls it to display what the profile already says. Announcing a
    /// value the caller just handed us is how a redraw turns into an edit, and with the job list
    /// that means one row's refresh writing over another row's choice.
    /// </remarks>
    public void Select(int value)
    {
        Selected = value;

        var selectedIndex = _cells.FindIndex(cell => cell.Value == value);

        for (var i = 0; i < _cells.Count; i++)
        {
            var button = _cells[i].Button;
            var filled = i <= selectedIndex;

            button.Pressed = filled;

            if (filled)
                button.AddStyleClass(StyleBase.ButtonCaution);
            else
                button.RemoveStyleClass(StyleBase.ButtonCaution);
        }

        _name.Text = selectedIndex >= 0 ? _labels[selectedIndex] : string.Empty;
    }
}
