using Content.Shared.Ember.Ranks;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Clothing;

/// <summary>
/// Sews a character's rank boards and department patch onto what they are wearing, at spawn and in
/// the lobby preview.
/// </summary>
/// <remarks>
/// This is the one thing about a uniform that the uniform cannot know. Which coverall you are
/// issued is a fact about your service, so it lives in the kit; what is on the sleeve is a fact
/// about you, and two people in the same post wearing the same coverall do not wear the same
/// boards.
///
/// SierraBay12 solves it by multiplying prototypes - <c>utility/expeditionary/officer/medical</c>
/// is a whole garment that exists to carry one patch, and there are sixty-two of them - and by
/// handing the rank boards over in a duffelbag for the player to pin on themselves. We resolve it
/// instead, which is what keeps the uniform count at the number of sprites that were actually
/// drawn.
///
/// Everything is issued to every garment that will take it, rather than to one. That is not
/// generosity: an outer layer hides the uniform underneath it completely, so boards on the shirt
/// alone would vanish the moment a jacket went on. Real services buy insignia per garment for the
/// same reason.
/// </remarks>
public sealed class EmberInsigniaSystem : EntitySystem
{
    [Dependency] private readonly EmberAccessorySystem _accessory = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>
    /// Department per job, built on first use. The reverse lookup is a scan of every department's
    /// role list, and spawning a shift asks for it once per player.
    /// </summary>
    private Dictionary<string, string>? _departmentByJob;

    public override void Initialize()
    {
        base.Initialize();

        // A department gaining or losing a role changes the answer, and prototype reload is the
        // only way that happens outside a restart.
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<DepartmentPrototype>())
            _departmentByJob = null;
    }

    /// <summary>
    /// Attaches the insignia this character is entitled to in this post to everything they are
    /// wearing that takes it. Safe to call on a mob wearing nothing.
    /// </summary>
    public void IssueInsignia(EntityUid wearer, JobPrototype job, HumanoidCharacterProfile profile)
    {
        var rank = profile.Rank is { } rankId && _proto.TryIndex(rankId, out EmberRankPrototype? rankProto)
            ? rankProto
            : null;

        var insignia = GetDepartmentInsignia(job);
        var speciesAccessories = GetSpeciesAccessories(profile);

        // Nothing to hand out is the ordinary case for a contractor in a civilian post, and it must
        // not cost an inventory walk.
        if (rank is not { Accessories.Count: > 0 } && insignia == null && speciesAccessories == null)
            return;

        if (!TryComp<InventoryComponent>(wearer, out var inventory))
            return;

        var enumerator = _inventory.GetSlotEnumerator((wearer, inventory));
        while (enumerator.NextItem(out var garment, out _))
        {
            if (!TryComp<EmberAccessoryHolderComponent>(garment, out var holder))
                continue;

            if (rank != null)
            {
                foreach (var board in rank.Accessories)
                {
                    Attach(garment, holder, board);
                }
            }

            if (insignia != null && insignia.Cuts.TryGetValue(holder.InsigniaCut, out var patch))
                Attach(garment, holder, patch);

            if (speciesAccessories == null)
                continue;

            foreach (var accessory in speciesAccessories)
            {
                Attach(garment, holder, accessory);
            }
        }
    }

    /// <summary>
    /// What this character's branch issues them for being the species they are, or null where it
    /// issues nothing.
    /// </summary>
    /// <remarks>
    /// The Cultural Exchange Programme patch, in practice: a tajaran or an unathi in the
    /// Expeditionary Corps is there under an agreement between two governments, and the patch is
    /// how that reads at a glance. A human in the same post has no entry and is issued nothing,
    /// which is the asymmetry the mark exists to draw.
    ///
    /// Read off the branch rather than off the species because the same person crewing a civilian
    /// freighter is in no programme and wears no patch. The species is constant; the arrangement
    /// that put them aboard is not.
    /// </remarks>
    public List<EntProtoId>? GetSpeciesAccessories(HumanoidCharacterProfile profile)
    {
        if (profile.Branch is not { } branchId
            || !_proto.TryIndex(branchId, out EmberBranchPrototype? branch)
            || !branch.SpeciesAccessories.TryGetValue(profile.Species, out var accessories)
            || accessories.Count == 0)
        {
            return null;
        }

        return accessories;
    }

    /// <summary>
    /// Spawns one insignia and attaches it, or deletes it again if this garment will not take it.
    /// </summary>
    /// <remarks>
    /// Spawn-then-check rather than check-then-spawn because <see cref="EmberAccessorySystem"/>
    /// answers questions about entities and not about prototypes - the categories and limits it
    /// enforces are on the component, which does not exist until the thing does. Garments are
    /// filtered by their own <see cref="EmberAccessoryHolderComponent.ValidSlots"/> first, so the
    /// wasted spawn only happens where a garment has run out of room for a category it does accept.
    /// </remarks>
    private void Attach(EntityUid garment, EmberAccessoryHolderComponent holder, EntProtoId proto)
    {
        var accessory = Spawn(proto, Transform(garment).Coordinates);

        if (!TryComp<EmberAccessoryComponent>(accessory, out var comp)
            || !holder.ValidSlots.Contains(comp.Slot)
            || !_accessory.TryAttach((garment, holder), (accessory, comp), user: null))
        {
            Del(accessory);
        }
    }

    /// <summary>
    /// The patch set this post wears, or null where it wears none.
    /// </summary>
    /// <remarks>
    /// The post's own entry wins over its department's, which is how a group inside a department
    /// carries a different patch from the rest of it - the science service's field group being the
    /// case that exists. A post with an entry of its own is not asked about its department at all.
    /// </remarks>
    public EmberInsigniaSetPrototype? GetDepartmentInsignia(JobPrototype job)
    {
        if (_proto.TryIndex(job.ID, out EmberInsigniaSetPrototype? own))
            return own;

        _departmentByJob ??= BuildDepartmentIndex();

        if (!_departmentByJob.TryGetValue(job.ID, out var department))
            return null;

        return _proto.TryIndex(department, out EmberInsigniaSetPrototype? insignia)
            ? insignia
            : null;
    }

    private Dictionary<string, string> BuildDepartmentIndex()
    {
        var index = new Dictionary<string, string>();

        foreach (var department in _proto.EnumeratePrototypes<DepartmentPrototype>())
        {
            foreach (var role in department.Roles)
            {
                // A job listed by two departments keeps the first. Vanilla has several of those and
                // none of ours are, so picking a winner here would be inventing a rule to cover a
                // case that does not arise; when it does, the second department is the one to fix.
                index.TryAdd(role.Id, department.ID);
            }
        }

        return index;
    }
}
