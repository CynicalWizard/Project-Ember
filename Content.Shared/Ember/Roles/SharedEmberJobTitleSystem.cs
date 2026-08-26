using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Ember.Localization;
using Content.Shared.Ember.Ranks;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Roles;

/// <summary>
/// Resolving which of a job's names a character holds it under.
/// </summary>
/// <remarks>
/// Static and side-effect free for the same reason <see cref="SharedEmberRanksSystem"/> is: the
/// lobby, the profile validator and the spawn code all have to agree about what a character is
/// called, and they run in three different places.
/// </remarks>
public static class SharedEmberJobTitleSystem
{
    /// <summary>
    /// The title with this id, or null — for a job with no such title, and for the null id that
    /// means "the job's own name". Both are ordinary cases rather than errors: a stored id
    /// outlives the data that defined it whenever a title is renamed or removed.
    /// </summary>
    public static bool TryGetTitle(
        JobPrototype job,
        string? id,
        [NotNullWhen(true)] out EmberJobTitle? title)
    {
        title = id is null
            ? null
            : job.AltTitles.FirstOrDefault(candidate => candidate.Id == id);

        return title != null;
    }

    /// <summary>
    /// What this character is called on their ID card and in the manifest. Falls back to the
    /// job's own name, which is what an unrecognised id has to mean — a character whose title
    /// was deleted out from under them is still an engineer.
    /// </summary>
    /// <remarks>
    /// Gender chooses between forms of the same name where a language has them — «медсестра»
    /// against «медбрат» — and never between different names. Callers that have no character to
    /// hand may leave it, and get the neutral form.
    /// </remarks>
    public static string GetLocalizedName(
        JobPrototype job,
        string? titleId,
        Gender gender = Gender.Epicene)
    {
        return TryGetTitle(job, titleId, out var title)
            ? EmberGenderedName.Localize(title.Name, title.NameMale, title.NameFemale, gender)
            : job.LocalizedName;
    }

    public static string? GetLocalizedDescription(JobPrototype job, string? titleId)
    {
        if (TryGetTitle(job, titleId, out var title) && title.Description is { } description)
            return Loc.GetString(description);

        return job.LocalizedDescription;
    }

    /// <summary>
    /// Whether this character may hold the job under this name.
    /// </summary>
    /// <remarks>
    /// Species and age only. Skills are deliberately not checked here, and the difference is
    /// worth naming: a skill floor is something the player can go and fix by spending points, so
    /// it is reported through the ordinary requirement machinery and locks the row until they
    /// do. Species and age are not fixable from the jobs tab, so they simply remove the name
    /// from the list — a lock the player cannot act on is just a wall with writing on it.
    ///
    /// A null age skips the check, the same way a null species does, for callers that have no
    /// character to hand.
    /// </remarks>
    public static bool IsTitleAllowed(
        EmberJobTitle title,
        ProtoId<SpeciesPrototype>? species,
        int? age = null)
    {
        if (age is { } years && years < title.MinAge)
            return false;

        return SharedEmberRanksSystem.IsSpeciesAllowed(
            title.SpeciesWhitelist,
            title.SpeciesBlacklist,
            species);
    }

    /// <summary>
    /// The names this character may pick from, in prototype order. The job's own name is always
    /// available and is not in this list — it is the null selection.
    /// </summary>
    public static IEnumerable<EmberJobTitle> GetSelectableTitles(
        JobPrototype job,
        ProtoId<SpeciesPrototype>? species,
        int? age = null)
    {
        return job.AltTitles.Where(title => IsTitleAllowed(title, species, age));
    }

    /// <summary>
    /// The stored id, dropped if this character can no longer hold it. Returning null rather
    /// than refusing the profile is deliberate: a player who changes species or winds the age
    /// back should find their doctor is no longer a surgeon, not find their character rejected.
    /// </summary>
    public static string? SanitizeTitle(
        JobPrototype job,
        string? titleId,
        ProtoId<SpeciesPrototype>? species,
        int? age = null)
    {
        if (!TryGetTitle(job, titleId, out var title))
            return null;

        return IsTitleAllowed(title, species, age) ? title.Id : null;
    }
}
