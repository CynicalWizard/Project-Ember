namespace Content.Shared.Ember.Ranks;

/// <summary>
/// Broad class of a rank, used wherever something needs to treat "officers" differently from
/// "everyone else" — access to an officers' mess, chain of command, who may sign what.
/// </summary>
/// <remarks>
/// SierraBay12's <c>/singleton/rank_category</c>. Bay derives it from the grade number in
/// <c>/datum/mil_rank/rank_category()</c>; we derive it the same way by default but let a
/// prototype say otherwise, because an organisation with a ladder unlike the SCG's should not
/// have to bend its grade numbers to land in the right class.
/// </remarks>
public enum EmberRankCategory : byte
{
    /// <summary>
    /// Not a graded rank: civilians, appointees, contractors, machines. Bay returns null here.
    /// </summary>
    None,

    /// <summary>Enlisted and non-commissioned.</summary>
    Enlisted,

    /// <summary>Commissioned officers.</summary>
    Commissioned,
}
