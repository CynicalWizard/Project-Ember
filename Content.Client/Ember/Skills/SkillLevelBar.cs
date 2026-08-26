using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Ember.Skills;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Ember.Skills;

/// <summary>
/// One skill's level, as a row of cells with the level spelled out beside them.
/// </summary>
/// <remarks>
/// Ember: this used to be five buttons carrying the level names - "Без навыка", "Базовый",
/// "Обученный", "Опытный", "Мастер" - which came to about six hundred pixels a row. That was
/// affordable while the skills lived in a window of their own and unaffordable the moment they
/// became a column beside the post list: the names of the skills themselves were squeezed to
/// nothing, so a player could read every level and no longer tell which skill they belonged to.
///
/// The cells carry the level names as tooltips and the current level is named once, at the end of
/// the row. That is also the encoding the interface is heading for: state read from shape rather
/// than from colour, because a one-colour terminal cannot afford colour and a filled cell against
/// an empty one survives any shader.
/// </remarks>
public sealed class SkillLevelBar : BoxContainer
{
    /// <summary>
    /// Room for the common level names, so the meters line up down the column.
    /// </summary>
    /// <remarks>
    /// Clipped rather than generous: this is one word out of five known ones, and every pixel it
    /// reserves comes out of the skill's own name, which is a phrase and cannot be guessed from
    /// its first half.
    /// </remarks>
    private const int LevelNameWidth = 80;

    private const int CellWidth = 26;

    /// <summary>
    /// What one of these measures, for whoever has to budget a column around it.
    /// </summary>
    public const int MeterWidth =
        CellWidth * ((int) SkillLevels.Max - (int) SkillLevels.Min + 1) + 8 + LevelNameWidth;

    public SkillLevelBar(
        SkillPrototype skill,
        SkillLevel current,
        Func<SkillLevel, bool>? canSelect = null,
        Action<SkillLevel>? onSelected = null)
    {
        Orientation = LayoutOrientation.Horizontal;
        SeparationOverride = 0;

        for (var value = (int) SkillLevels.Min; value <= (int) SkillLevels.Max; value++)
        {
            var level = (SkillLevel) value;
            var hasLevel = value <= skill.Levels.Count;
            var filled = hasLevel && level <= current;
            var selectable = hasLevel && (canSelect?.Invoke(level) ?? false);

            var button = new Button
            {
                ToggleMode = onSelected != null,
                Pressed = filled,
                // A level this skill does not have keeps its cell rather than losing it: the
                // meters are read as a column, and a short row that closes up early reads as a
                // different skill rather than as a shorter scale.
                Disabled = !hasLevel || (onSelected != null && !selectable),
                MinSize = new Vector2(CellWidth, 24),
                HorizontalExpand = false,
                ToolTip = hasLevel ? GetLevelName(skill, level) : null,
            };

            if (filled)
                button.AddStyleClass(StyleBase.ButtonCaution);

            if (value == (int) SkillLevels.Min)
                button.AddStyleClass(StyleBase.ButtonOpenRight);
            else if (value == (int) SkillLevels.Max)
                button.AddStyleClass(StyleBase.ButtonOpenLeft);
            else
                button.AddStyleClass(StyleBase.ButtonOpenBoth);

            if (hasLevel && onSelected != null)
                button.OnPressed += _ => onSelected(level);

            AddChild(button);
        }

        AddChild(new Label
        {
            Text = GetLevelName(skill, current),
            MinWidth = LevelNameWidth,
            Margin = new Thickness(8, 0, 0, 0),
            ClipText = true,
            StyleClasses = { StyleBase.StyleClassLabelSubText },
        });
    }

    public static string GetLevelName(SkillPrototype skill, SkillLevel level)
    {
        var levelIndex = (int) level - 1;
        if (levelIndex >= 0 && levelIndex < skill.Levels.Count)
            return Loc.GetString(skill.Levels[levelIndex]);

        return Loc.GetString($"skill-level-{level.ToString().ToLowerInvariant()}");
    }
}
