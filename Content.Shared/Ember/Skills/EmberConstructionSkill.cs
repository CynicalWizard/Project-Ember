using System.Diagnostics.CodeAnalysis;
using Content.Shared.Construction;
using Content.Shared.Construction.Steps;
using Content.Shared.Ember.Materials;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Skills;

/// <summary>
/// How Bay ties building things to the construction skill. A recipe is as hard as the most demanding material it
/// consumes, and that difficulty doubles as the skill level at which you stop ruining your materials.
/// </summary>
/// <remarks>
/// The server decides the outcome and the construction menu warns about it beforehand, so both sides have to
/// reach the same number. They used to work it out separately from copies of the same loop.
/// </remarks>
public static class EmberConstructionSkill
{
    public static readonly ProtoId<SkillPrototype> Skill = "construction";

    /// <summary>MATERIAL_EASY_DIY.</summary>
    public const int MinDifficulty = 0;

    /// <summary>MATERIAL_VERY_HARD_DIY.</summary>
    public const int MaxDifficulty = 3;

    /// <summary>The chance an unskilled builder ruins the materials, which Bay passes for every stack recipe.</summary>
    public const int UnskilledFailChance = 90;

    /// <summary>
    /// Bay: <c>difficulty = clamp(1 + material.construction_difficulty, MATERIAL_EASY_DIY, MATERIAL_VERY_HARD_DIY)</c>,
    /// so a recipe using only easy materials still asks for Unskilled and never fails.
    /// </summary>
    public static int GetDifficulty(
        ConstructionGraphEdge edge,
        IPrototypeManager prototype,
        IComponentFactory factory)
    {
        var hardest = 0;

        foreach (var step in edge.Steps)
        {
            if (step is not MaterialConstructionGraphStep material)
                continue;

            if (TryGetMaterial(material.MaterialPrototypeId, prototype, factory, out var ember))
                hardest = Math.Max(hardest, ember.ConstructionDifficulty);
        }

        return Math.Clamp(1 + hardest, MinDifficulty, MaxDifficulty);
    }

    /// <summary>
    /// The skill level at or above which the recipe is safe to attempt. Difficulty is expressed on the same
    /// scale as skill levels, which is what lets Bay hand it straight to the failure roll.
    /// </summary>
    public static SkillLevel GetRequiredLevel(int difficulty)
    {
        return (SkillLevel) Math.Clamp(difficulty, (int) SkillLevels.Min, (int) SkillLevels.Max);
    }

    /// <summary>
    /// Walks a construction step's stack id to the Ember material behind it. A stack with no Ember material —
    /// most of the non-ported ones — has no difficulty to contribute and leaves the recipe at its base level.
    /// </summary>
    public static bool TryGetMaterial(
        ProtoId<StackPrototype> stackId,
        IPrototypeManager prototype,
        IComponentFactory factory,
        [NotNullWhen(true)] out EmberMaterialPrototype? material)
    {
        material = null;

        if (!prototype.TryIndex(stackId, out StackPrototype? stack) ||
            string.IsNullOrEmpty(stack.Spawn.Id) ||
            !prototype.TryIndex(stack.Spawn, out EntityPrototype? entity) ||
            !entity.TryGetComponent<EmberMaterialStackComponent>(out var stackComponent, factory))
        {
            return false;
        }

        return prototype.TryIndex(stackComponent.Material, out material);
    }
}
