using Content.Shared.Storage;
using Content.Shared.Tools.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Ember.Materials;

/// <summary>
/// Names a piece of debris after the material it came off and decides what welding it gives back.
/// </summary>
/// <remarks>
/// SS14 already has shards, with stepping on them, welding them into sheets and sharpening them into shivs all
/// working, so this drives that machinery from the material rather than adding a second kind of debris. What
/// Bay contributes is that any material can produce it, under its own name and in its own colour.
/// </remarks>
public sealed class EmberProceduralShardSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralShardComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<EmberProceduralShardComponent> ent, ref MapInitEvent args)
    {
        if (!_prototype.TryIndex(ent.Comp.Material, out EmberMaterialPrototype? material))
            return;

        if (_net.IsServer)
        {
            // Rolled once and sent out, so every client sees the same piece.
            ent.Comp.Size = _random.Next(EmberShardTypes.Sizes.Length);
            Dirty(ent);
        }

        Rename(ent, material);

        // Bay will not let you weld splinters back into a plank, and a material with no sheet form has nothing
        // to give back either way.
        if (material.ShardCanRepair && material.StackEntity is { } sheet)
        {
            EnsureComp<ToolRefinableComponent>(ent).RefineResult =
                new HashSet<EntitySpawnEntry> { new() { PrototypeId = sheet } };
        }
        else
        {
            RemComp<ToolRefinableComponent>(ent);
        }
    }

    /// <summary>
    /// Both forms of the material name go to Fluent because the two languages want different ones: English says
    /// "steel shrapnel", Russian wants the genitive, "шрапнель стали". Each locale takes the one it needs, and a
    /// material with no genitive written for it falls back to the plain name rather than failing.
    /// </summary>
    private void Rename(EntityUid uid, EmberMaterialPrototype material)
    {
        if (EmberShardTypes.GetNameId(material.ShardType) is not { } nameId)
            return;

        var nominative = Loc.GetString(material.DisplayName);

        // Checked with HasString rather than TryGetString: the latter logs an error for every material that has
        // no declined form written for it, and most do not need one.
        var genitiveId = $"{material.DisplayName}-genitive";
        var genitive = Loc.HasString(genitiveId) ? Loc.GetString(genitiveId) : nominative;

        _metaData.SetEntityName(
            uid,
            Loc.GetString(nameId, ("material", nominative), ("materialGenitive", genitive)));

        if (EmberShardTypes.GetDescriptionId(material.ShardType) is { } descriptionId)
        {
            _metaData.SetEntityDescription(
                uid,
                Loc.GetString(descriptionId, ("material", nominative), ("materialGenitive", genitive)));
        }
    }
}
