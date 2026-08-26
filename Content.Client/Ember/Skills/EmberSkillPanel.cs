using Content.Client.Stylesheets;
using Content.Shared.Ember.Skills;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Ember.Skills;

/// <summary>
/// The character's skills, as a panel that lives beside the post list.
/// </summary>
/// <remarks>
/// Ember: this was a modal window opened from a button, and that was the third of the three
/// complaints the editor rework exists to answer. Finding out what a post was missing meant
/// closing the window, finding the row, reading it, and opening the window again - a
/// look-forget-return cycle repeated once per post the player was considering. A skill set is one
/// per character rather than one per post, so it has no business being modal in the first place;
/// as a column, the requirement and its fulfilment are on screen at the same time.
/// </remarks>
public sealed class EmberSkillPanel : BoxContainer
{
    /// <summary>
    /// Room for the longest skill name there is - "Внекорабельная деятельность".
    /// </summary>
    private const int SkillNameWidth = 245;

    private readonly Label _pointsLabel;
    private readonly ProgressBar _pointsBar;
    private readonly BoxContainer _content;

    private IReadOnlyList<SkillPrototype> _skills = Array.Empty<SkillPrototype>();
    private Func<ProtoId<SkillCategoryPrototype>, string>? _getCategoryName;
    private Action<SkillPrototype, SkillLevel>? _onSelected;

    public EmberSkillPanel()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        // Stated here because it cannot be inferred. The rows live inside a ScrollContainer with
        // horizontal scrolling off, and such a container reports no minimum width of its own - so
        // however wide a row needs to be, the panel asks for none of it, gets whatever is left
        // after the post list, and clips the rows from the left, showing their last few letters.
        // The number is the row: name + meter + level name + margins.
        MinWidth = SkillNameWidth + 8 + SkillLevelBar.MeterWidth + 18;

        AddChild(new Label
        {
            Text = Loc.GetString("humanoid-profile-editor-skills-window-title"),
            HorizontalAlignment = HAlignment.Center,
            HorizontalExpand = true,
            StyleClasses = { StyleBase.StyleClassLabelHeading },
        });

        // A RichTextLabel rather than a Label, and not for markup: a Label's minimum width is its
        // entire string, so this one sentence claimed six hundred pixels of the section and took
        // them out of the post list beside it.
        //
        // MaxWidth is what actually makes it wrap. Given no upper bound a RichTextLabel measures
        // itself on one line and asks for the whole sentence too, which is the same six hundred
        // pixels by another route - the bound is the wrap point, not a cosmetic limit.
        var hint = new RichTextLabel { MaxWidth = 320, Margin = new Thickness(4, 0) };
        hint.SetMessage(Loc.GetString("humanoid-profile-editor-skills-hint"));
        AddChild(hint);

        AddChild(_pointsLabel = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            HorizontalExpand = true,
        });

        AddChild(_pointsBar = new ProgressBar
        {
            MaxValue = 1,
            Value = 0,
            MaxHeight = 8,
            Margin = new Thickness(0, 5),
        });

        var scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            VScrollEnabled = true,
        };

        scroll.AddChild(_content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(4),
            HorizontalExpand = true,
            VerticalExpand = true,
        });

        AddChild(scroll);
    }

    public void SetSkills(
        IReadOnlyList<SkillPrototype> skills,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> allocation,
        int skillPointBudget,
        Func<ProtoId<SkillCategoryPrototype>, string> getCategoryName,
        Action<SkillPrototype, SkillLevel> onSelected)
    {
        _skills = skills;
        _getCategoryName = getCategoryName;
        _onSelected = onSelected;

        Rebuild(allocation, skillPointBudget);
    }

    private void Rebuild(
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> allocation,
        int skillPointBudget)
    {
        if (_getCategoryName == null)
            return;

        var values = SharedSkillsSystem.SanitizeAllocation(_skills, allocation, skillPointBudget);
        var remaining = SharedSkillsSystem.GetRemainingPoints(_skills, values, skillPointBudget);

        _pointsLabel.Text = Loc.GetString("humanoid-profile-editor-skills-points-label",
            ("points", remaining),
            ("max", skillPointBudget));
        _pointsBar.MaxValue = Math.Max(1, skillPointBudget);
        _pointsBar.Value = Math.Max(0, remaining);

        _content.DisposeAllChildren();

        ProtoId<SkillCategoryPrototype>? currentCategory = null;
        BoxContainer? categoryRows = null;

        foreach (var skill in _skills)
        {
            if (currentCategory != skill.Category)
            {
                currentCategory = skill.Category;
                categoryRows = AddCategory(_getCategoryName(skill.Category));
            }

            var level = values.GetValueOrDefault(skill.ID, SkillLevels.Min);
            // Refunding what this skill already costs lets the player move it up and down
            // freely without first having to zero it out.
            var availablePoints = remaining + SharedSkillsSystem.GetTotalCost(skill, level);

            categoryRows!.AddChild(CreateSkillRow(skill, level, values, availablePoints));
        }
    }

    private BoxContainer AddCategory(string name)
    {
        var panel = new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 8),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#202124"),
                BorderColor = Color.FromHex("#3A3A3D"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 6,
            },
        };

        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        box.AddChild(new Label
        {
            Text = name,
            StyleClasses = { StyleBase.StyleClassLabelHeading },
            Margin = new Thickness(0, 0, 0, 4),
        });

        var rows = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        box.AddChild(rows);
        panel.AddChild(box);
        _content.AddChild(panel);
        return rows;
    }

    private BoxContainer CreateSkillRow(
        SkillPrototype skill,
        SkillLevel level,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> values,
        int availablePoints)
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(0, 2),
        };

        var labelBox = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = SkillNameWidth,
            HorizontalExpand = true,
        };

        labelBox.AddChild(new Label
        {
            Text = Loc.GetString(skill.Name),
            ToolTip = Loc.GetString(skill.Description),
            HorizontalExpand = true,
            ClipText = true,
        });

        row.AddChild(labelBox);
        row.AddChild(new SkillLevelBar(
            skill,
            level,
            target => CanSetSkillLevel(skill, target, values, availablePoints),
            target => _onSelected?.Invoke(skill, target)));

        return row;
    }

    private static bool CanSetSkillLevel(
        SkillPrototype skill,
        SkillLevel target,
        IReadOnlyDictionary<ProtoId<SkillPrototype>, SkillLevel> values,
        int availablePoints)
    {
        if (target < SkillLevels.Min || target > skill.DefaultMax)
            return false;

        if (SharedSkillsSystem.GetTotalCost(skill, target) > availablePoints)
            return false;

        var targetValues = new Dictionary<ProtoId<SkillPrototype>, SkillLevel>(values)
        {
            [skill.ID] = target,
        };

        return SharedSkillsSystem.CheckPrerequisites(skill, targetValues);
    }
}
