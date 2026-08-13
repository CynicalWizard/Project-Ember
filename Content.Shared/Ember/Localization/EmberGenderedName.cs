using Robust.Shared.Enums;

namespace Content.Shared.Ember.Localization;

/// <summary>
/// Picking the right form of a name for the person it is about.
/// </summary>
/// <remarks>
/// English mostly does not need this and Russian mostly does. "Nurse" is one word; «медсестра»
/// and «медбрат» are two, and choosing between them is not the player's business — it follows
/// from the character. Offering both in a dropdown lets a man call himself a медсестра, which is
/// not a choice anybody meant to give.
///
/// The pattern is three fields rather than a nested structure on purpose: a name that needs no
/// gendering writes one line and ignores the other two, which is the overwhelmingly common case.
/// A prototype adopts this by adding two optional <c>LocId</c>s beside the name it already has
/// and calling <see cref="Pick"/>. That is deliberately cheap, because Russian will keep
/// producing these — every post whose ordinary word is a person rather than a function is a
/// candidate.
///
/// The axis is <see cref="Gender"/> and not sex. This is a question about which word to use for
/// somebody, which is the grammatical axis, and it is the one Robust already uses for pronouns.
/// <see cref="Gender.Epicene"/> and <see cref="Gender.Neuter"/> both fall back to the neutral
/// form, which is why the neutral form has to actually be neutral — in Russian that usually
/// means reaching for the function rather than the person ("медработник", not "медсестра").
/// </remarks>
public static class EmberGenderedName
{
    /// <summary>
    /// The form to use, falling back to <paramref name="neutral"/> whenever the language does
    /// not distinguish or the character does not.
    /// </summary>
    public static LocId Pick(LocId neutral, LocId? male, LocId? female, Gender gender)
    {
        return gender switch
        {
            Gender.Male => male ?? neutral,
            Gender.Female => female ?? neutral,
            _ => neutral,
        };
    }

    public static string Localize(LocId neutral, LocId? male, LocId? female, Gender gender)
    {
        return Loc.GetString(Pick(neutral, male, female, gender));
    }
}
