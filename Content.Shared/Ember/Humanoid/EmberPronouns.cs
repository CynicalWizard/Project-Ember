using Content.Shared.Humanoid;
using Robust.Shared.Enums;

namespace Content.Shared.Ember.Humanoid;

/// <summary>
/// The one place that decides which pronouns a body takes.
/// </summary>
/// <remarks>
/// Upstream lets a player pick sex and gender independently, and the character editor showed both
/// as adjacent dropdowns with no stated relationship - so "Пол: Женский" beside "Личное
/// местоимение: Они / Их" was not only possible but easy to produce by accident, and nothing in
/// the interface explained which of the two the game would use where. Ember keeps the choice the
/// player actually understands, sex, and derives the pronouns from it.
///
/// It lives outside the editor because the derivation has to hold at runtime as well: sex can
/// change mid-round - see ChangeableSexSystem - and a character whose pronouns stayed behind
/// would be the same contradiction, only now with no screen on which to fix it.
/// </remarks>
public static class EmberPronouns
{
    /// <summary>
    /// The pronouns that go with a sex. Sexless bodies take the neuter, which is what an IPC or a
    /// vox is called in this setting; the plural "they" is deliberately not reachable.
    /// </summary>
    public static Gender GenderFor(Sex sex) => sex switch
    {
        Sex.Male => Gender.Male,
        Sex.Female => Gender.Female,
        _ => Gender.Neuter,
    };
}
