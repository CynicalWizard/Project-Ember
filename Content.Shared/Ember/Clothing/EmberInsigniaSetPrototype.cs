using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Clothing;

/// <summary>
/// The patches a department - or a single post inside one - issues, one per cut of garment.
/// </summary>
/// <remarks>
/// The id is a department id or a job id, and the job wins. That is what lets a group inside one
/// department wear its own patch: the science service is one department and its field group is
/// three posts of it, which Bay draws a separate patch for and the mock-up separates on the same
/// seam. The alternative was a second field on the job pointing here, which is the same fact
/// written twice and one more thing to leave out of step.
///
/// An id with no entry issues no patch. That is the right answer for the posts that have none:
/// State Oversight watches the ship rather than belonging to it, and the Federal Police are not
/// part of the ship's organisation at all.
///
/// Written out rather than assembled from the department id and the cut, although the entity ids
/// are regular enough to make that tempting. A built id that names nothing fails silently at spawn
/// and looks like the patch was never issued; a named one is checked by the YAML linter before the
/// server boots, and can be found by grep.
/// </remarks>
[Prototype("emberInsignia")]
public sealed partial class EmberInsigniaSetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The patch for each cut of garment. A cut with no entry issues nothing rather than falling
    /// back to another cut - Fleet does not staff most departments, and a Fleet rating wearing the
    /// Corps' patch would be a worse answer than a bare sleeve.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EmberInsigniaCut, EntProtoId> Cuts = new();
}
