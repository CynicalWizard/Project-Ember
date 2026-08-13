using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Ranks;

/// <summary>
/// Decides which branches and ranks a character may hold. Everything here is pure and static so
/// the lobby, the server-side profile check and the tests all ask exactly the same question.
/// </summary>
/// <remarks>
/// SierraBay12 spreads this across the map datum (<c>is_species_branch_restricted</c>,
/// <c>is_species_rank_restricted</c>) and <c>/datum/mil_branch/spawn_ranks()</c>. We keep the
/// data on the prototypes and the rules here.
/// </remarks>
public sealed class SharedEmberRanksSystem : EntitySystem
{
    /// <summary>
    /// Whether a character of this species may serve in this branch at all.
    /// A null species means the character has not picked one yet, and is not grounds to hide
    /// anything: the lobby would otherwise show an empty list before a species is chosen.
    /// </summary>
    public static bool IsBranchAllowed(EmberBranchPrototype branch, ProtoId<SpeciesPrototype>? species)
    {
        return IsSpeciesAllowed(branch.SpeciesWhitelist, branch.SpeciesBlacklist, species);
    }

    /// <summary>
    /// Whether a character of this species may hold this rank in this branch. Says nothing about
    /// whether they may pick it at character creation — see <see cref="IsRankSelectable"/>.
    /// </summary>
    public static bool IsRankAllowed(
        EmberBranchPrototype branch,
        EmberRankPrototype rank,
        ProtoId<SpeciesPrototype>? species,
        int? age = null)
    {
        if (!branch.Ranks.Contains(rank.ID))
            return false;

        if (!IsBranchAllowed(branch, species))
            return false;

        // Only the floor is enforced. The upper end of a bracket is what the service expects,
        // not what it forbids: people do stay in junior posts.
        if (age is { } years && years < rank.MinAge)
            return false;

        return IsSpeciesAllowed(rank.SpeciesWhitelist, rank.SpeciesBlacklist, species);
    }

    /// <summary>
    /// Whether a player may choose this rank at character creation. Narrower than
    /// <see cref="IsRankAllowed"/>: Admiral is a legal rank to hold but not one to spawn as.
    /// </summary>
    /// <remarks>
    /// An empty <see cref="EmberBranchPrototype.SpawnRanks"/> means every rank is selectable, the
    /// way Bay leaves <c>spawn_rank_types</c> off branches where the distinction does not matter.
    /// Reading it as "nothing is selectable" would silently empty the dropdown.
    /// </remarks>
    public static bool IsRankSelectable(
        EmberBranchPrototype branch,
        EmberRankPrototype rank,
        ProtoId<SpeciesPrototype>? species,
        int? age = null)
    {
        if (!IsRankAllowed(branch, rank, species, age))
            return false;

        return branch.SpawnRanks.Count == 0 || branch.SpawnRanks.Contains(rank.ID);
    }

    /// <summary>
    /// Employer id meaning the character is on nobody's payroll.
    /// </summary>
    public const string NoEmployer = "Unemployed";

    /// <summary>
    /// Whether a character in this branch may also name an employer. An unaffiliated character
    /// may: having no branch at all is exactly the case of someone who works for a living.
    /// </summary>
    public static bool AllowsEmployer(EmberBranchPrototype? branch)
    {
        return branch?.AllowsEmployer ?? true;
    }

    /// <summary>
    /// Reconciles service with employment, which are alternatives rather than layers. Someone
    /// the state has posted somewhere is not simultaneously on a company's books, so taking the
    /// posting clears the payroll entry rather than the two sitting side by side.
    /// </summary>
    public static string ResolveEmployer(EmberBranchPrototype? branch, string employer)
    {
        return AllowsEmployer(branch) ? employer : NoEmployer;
    }

    /// <summary>
    /// A whitelist says who may, a blacklist says who may not. A species named in both is a
    /// mistake in the data, and the safe reading of a mistake is to refuse.
    /// </summary>
    private static bool IsSpeciesAllowed(
        HashSet<ProtoId<SpeciesPrototype>> whitelist,
        HashSet<ProtoId<SpeciesPrototype>> blacklist,
        ProtoId<SpeciesPrototype>? species)
    {
        if (species is not { } id)
            return true;

        if (blacklist.Contains(id))
            return false;

        return whitelist.Count == 0 || whitelist.Contains(id);
    }
}
