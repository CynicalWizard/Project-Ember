using Content.Shared.Ember.Materials;

namespace Content.Shared.Ember.Structures;

/// <summary>What a table's materials make of it, as Bay's <c>update_material</c> works it out.</summary>
public readonly record struct EmberTableStats(float Health, float MinimumDamage, float DamageMultiplier);

/// <summary>
/// A table is only as strong as what it is plated with. Bay reads all of it off the material rather than off the
/// table: half the material's integrity for how much it takes, a tenth of its hardness for the smallest hit that
/// registers, and four times the damage if the plating is brittle and nothing sturdier is holding it together.
/// </summary>
public static class EmberTableMaterialStats
{
    /// <summary>A steel table in Bay: integrity 150, so 75 health. Everything else is measured against it.</summary>
    public const float ReferenceHealth = 75f;

    /// <summary>
    /// What a steel table takes to break here. Anchoring to it means a steel table behaves exactly as it did
    /// before any of this existed and only the other materials move, which is how walls are pinned too.
    /// </summary>
    public const float ReferenceThreshold = 125f;

    /// <summary>Bay's <c>TABLE_BRITTLE_MATERIAL_MULTIPLIER</c>: glass gives way four times as fast.</summary>
    public const float BrittleMultiplier = 4f;

    /// <summary>A table with nothing on its frame. Bay gives it a flat ten and no floor at all.</summary>
    public const float BareHealth = 10f;

    public static EmberTableStats For(EmberMaterialPrototype? material, EmberMaterialPrototype? reinforcement)
    {
        if (material == null)
            return new EmberTableStats(BareHealth, 0f, 1f);

        var health = material.Integrity / 2f;
        var hardness = (float) material.Hardness;

        if (reinforcement != null)
        {
            health += reinforcement.Integrity / 2f;
            hardness += reinforcement.Hardness;
        }

        // Reinforcing brittle plating with something that is not brittle takes the multiplier off; reinforcing
        // glass with more glass does not.
        var brittle = material.Brittle && (reinforcement == null || reinforcement.Brittle);

        // Bay's round() takes the floor, so a bronze table's 25 hardness comes out as 2 rather than 3.
        return new EmberTableStats(
            health,
            MathF.Floor(hardness / 10f),
            brittle ? BrittleMultiplier : 1f);
    }

    /// <summary>What the table's own destruction threshold becomes, on the scale the rest of the game uses.</summary>
    public static float ThresholdFor(float health)
    {
        return health / ReferenceHealth * ReferenceThreshold;
    }
}
