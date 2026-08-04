using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Materials;

/// <summary>
/// What a thing is built out of, wherever it happens to keep that.
/// </summary>
/// <remarks>
/// There is no one component that means "made of something": a wall names a wall material that points at the
/// physical one, a table names its plating and its reinforcement separately, and a sheet names itself. Anything
/// that wants to reason about the material — what a thing melts at, what it leaves behind when it breaks — needs
/// the same lookup, so it lives here rather than in each of them.
/// </remarks>
public static class EmberMaterialLookup
{
    /// <summary>The frame under a table's plating is steel whatever it is topped with, exactly as on Bay.</summary>
    public static readonly ProtoId<EmberMaterialPrototype> TableFrame = "Steel";

    /// <summary>What an airlock is, since a style says nothing about substance.</summary>
    public static readonly ProtoId<EmberMaterialPrototype> AirlockShell = "Steel";

    /// <summary>And what is set into a glass one.</summary>
    public static readonly ProtoId<EmberMaterialPrototype> AirlockWindow = "Glass";

    /// <summary>
    /// Every material an entity is made of, most important first. Most things are one material; a table is
    /// three, and a reinforced wall is two.
    /// </summary>
    public static IEnumerable<ProtoId<EmberMaterialPrototype>> Materials(
        IEntityManager entities,
        IPrototypeManager prototypes,
        EntityUid uid)
    {
        if (entities.TryGetComponent(uid, out EmberProceduralWallComponent? wall))
        {
            if (Physical(prototypes, wall.Material) is { } material)
                yield return material;

            if (wall.ReinforcementMaterial is { } reinforcement &&
                Physical(prototypes, reinforcement) is { } reinforcementMaterial)
            {
                yield return reinforcementMaterial;
            }

            yield break;
        }

        if (entities.TryGetComponent(uid, out EmberProceduralStructureComponent? structure))
        {
            if (Physical(prototypes, structure.Material) is { } material)
                yield return material;

            yield break;
        }

        if (entities.TryGetComponent(uid, out EmberProceduralTableComponent? table))
        {
            yield return TableFrame;

            if (table.Material is { } plating)
                yield return plating;

            if (table.Reinforcement is { } reinforcement)
                yield return reinforcement;

            yield break;
        }

        if (entities.TryGetComponent(uid, out EmberMaterialTintComponent? tint))
        {
            if (Physical(prototypes, tint.Material) is { } material)
                yield return material;

            yield break;
        }

        if (entities.TryGetComponent(uid, out EmberProceduralAirlockComponent? airlock))
        {
            // An airlock carries a style, which is a set of colours rather than a substance. Bay's station
            // airlocks are steel, and a glass one is steel and a pane, which is what it counts as made of.
            yield return AirlockShell;

            if (airlock.Glass)
                yield return AirlockWindow;

            yield break;
        }

        if (entities.TryGetComponent(uid, out EmberProceduralMaterialDoorComponent? door))
        {
            yield return door.Material;
            yield break;
        }

        if (entities.TryGetComponent(uid, out EmberMaterialStackComponent? stack) &&
            stack.Material is { } stackMaterial)
        {
            yield return stackMaterial;
        }
    }

    private static ProtoId<EmberMaterialPrototype>? Physical(
        IPrototypeManager prototypes,
        ProtoId<EmberWallMaterialPrototype> material)
    {
        return prototypes.TryIndex(material, out EmberWallMaterialPrototype? wall)
            ? wall.PhysicalMaterial
            : null;
    }
}
