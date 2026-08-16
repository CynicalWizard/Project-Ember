using Content.Shared.Customization.Systems;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Background;

/// <summary>
/// Selection and validation for the four background axes.
/// </summary>
/// <remarks>
/// The static half exists so <see cref="Preferences.HumanoidCharacterProfile"/> can coerce a saved
/// profile without an entity manager, the same arrangement <see cref="Ranks.SharedEmberRanksSystem"/>
/// uses for branch and rank.
///
/// Only the species requirement is evaluated here. The rest of a background's requirements need a
/// job, play times and a whitelist flag to answer, none of which the profile has, and all of which
/// the lobby and the spawn path do have. Species is the one that has to be checked at this level
/// because it can be invalidated after the fact: a player picks a Martian upbringing, then changes
/// the character's species to IPC, and something has to notice.
/// </remarks>
public sealed class SharedEmberBackgroundSystem : EntitySystem
{
    /// <summary>
    /// Whether a character of this species may hold this background.
    /// </summary>
    /// <remarks>
    /// A null species means the character has not picked one yet, which is not grounds to hide
    /// anything - the lobby would otherwise show four empty lists before a species is chosen.
    /// </remarks>
    public static bool IsSelectable(EmberBackgroundPrototype background, ProtoId<SpeciesPrototype>? species)
    {
        if (background.Hidden)
            return false;

        if (species == null)
            return true;

        foreach (var requirement in background.Requirements)
        {
            if (requirement is not CharacterSpeciesRequirement speciesRequirement)
                continue;

            // Inverted requirements read as "anyone except these", so the sense of the membership
            // test flips with them rather than the entry simply being unavailable.
            if (speciesRequirement.Species.Contains(species.Value) == speciesRequirement.Inverted)
                return false;
        }

        return true;
    }

    /// <summary>
    /// The entries of one axis a character of this species may choose from, in display order:
    /// heaviest first, then alphabetically by displayed name.
    /// </summary>
    public static List<EmberBackgroundPrototype> GetSelectable(
        IPrototypeManager prototypes,
        EmberBackgroundAxis axis,
        ProtoId<SpeciesPrototype>? species)
    {
        var result = new List<EmberBackgroundPrototype>();

        foreach (var background in prototypes.EnumeratePrototypes<EmberBackgroundPrototype>())
        {
            if (background.Axis == axis && IsSelectable(background, species))
                result.Add(background);
        }

        result.Sort(static (a, b) =>
        {
            var byWeight = b.Weight.CompareTo(a.Weight);
            return byWeight != 0
                ? byWeight
                : string.Compare(Robust.Shared.Localization.Loc.GetString(a.Name),
                    Robust.Shared.Localization.Loc.GetString(b.Name), StringComparison.CurrentCulture);
        });

        return result;
    }

    /// <summary>
    /// The stored choice if it is still a valid answer on this axis, and something this character
    /// can actually hold otherwise.
    /// </summary>
    /// <remarks>
    /// A choice goes invalid three ways: the prototype was deleted, it was moved to another axis,
    /// or the character's species changed out from under it. All three end the same way, because a
    /// profile that names a background it cannot hold is a profile that will be rejected somewhere
    /// less convenient later.
    ///
    /// The fallback is checked against the species too, and that is not belt and braces - it is the
    /// whole difficulty. The default homeworld is Mars, which is human and IPC only, so handing it
    /// to a tajaran replaces one invalid answer with another. The lobby then tries to correct the
    /// correction, and since the result is invalid again it corrects it again: this recursed until
    /// the client hung. Where the fallback does not fit either, the heaviest entry the species can
    /// hold is taken instead, which is the top of the list they would have been offered.
    /// </remarks>
    public static ProtoId<EmberBackgroundPrototype> Resolve(
        IPrototypeManager prototypes,
        ProtoId<EmberBackgroundPrototype> chosen,
        EmberBackgroundAxis axis,
        ProtoId<SpeciesPrototype>? species,
        ProtoId<EmberBackgroundPrototype> fallback)
    {
        if (IsValidFor(prototypes, chosen, axis, species))
            return chosen;

        if (IsValidFor(prototypes, fallback, axis, species))
            return fallback;

        var selectable = GetSelectable(prototypes, axis, species);

        // Nothing at all on this axis is open to the species. Keeping the fallback is then the
        // least bad answer: it is at least a real prototype, and inventing a different wrong one
        // would only move the problem.
        return selectable.Count > 0 ? selectable[0].ID : fallback;
    }

    /// <summary>
    /// Whether this id names a background on this axis that this species may hold.
    /// </summary>
    public static bool IsValidFor(
        IPrototypeManager prototypes,
        ProtoId<EmberBackgroundPrototype> id,
        EmberBackgroundAxis axis,
        ProtoId<SpeciesPrototype>? species)
    {
        return prototypes.TryIndex(id, out var background)
            && background.Axis == axis
            && IsSelectable(background, species);
    }
}
