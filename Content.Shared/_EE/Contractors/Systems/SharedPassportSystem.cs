using Content.Shared._EE.Contractors.Components;
using Content.Shared._EE.Contractors.Prototypes;
using Content.Shared.Ember.Background;
using Content.Shared.Administration.Logs;
using Content.Shared.Clothing.Loadouts.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared;
using Content.Shared.CCVar;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;


namespace Content.Shared._EE.Contractors.Systems;

public class SharedPassportSystem : EntitySystem
{
    /// <summary>
    /// Ember's present, minus the real one. The chronology picks 2331 because it is
    /// calendar-identical to 2026 - same weekday for 1 January, both common years - so a date on a
    /// terminal lands on the right day of the week without anyone arranging it. Keeping that as an
    /// offset rather than a constant is what makes the property survive the year turning over.
    /// </summary>
    public const int YearOffset = 305;

    public static int CurrentYear => DateTime.UtcNow.Year + YearOffset;
    const string PIDChars = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _sharedTransformSystem = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerLoadoutAppliedEvent>(OnPlayerLoadoutApplied);
        SubscribeLocalEvent<PassportComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, PassportComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || component.OwnerProfile == null)
            return;

        var profile = component.OwnerProfile;
        var species = _prototypeManager.Index<SpeciesPrototype>(profile.Species);

        args.PushMarkup(Loc.GetString("passport-examine-name", ("name", profile.Name)), 50);
        args.PushMarkup(Loc.GetString("passport-examine-species",
            ("species", Loc.GetString(species.Name))), 49);
        args.PushMarkup(Loc.GetString("passport-examine-sex", ("sex", profile.Gender)), 48);
        args.PushMarkup(Loc.GetString("passport-examine-height",
            ("height", MathF.Round(profile.Height * species.AverageHeight))), 47);
        args.PushMarkup(Loc.GetString("passport-examine-birth-year",
            ("year", CurrentYear - profile.Age)), 46);

        // Ember: the homeworld is the one axis a passport prints rather than is issued by, and it
        // is what makes splitting the two visible in play - an Amelian with SCG citizenship holds a
        // Sol passport that says where they were actually born.
        if (_prototypeManager.TryIndex(profile.Homeworld, out EmberBackgroundPrototype? homeworld))
        {
            args.PushMarkup(Loc.GetString("passport-examine-birthplace",
                ("place", Loc.GetString(homeworld.Name))), 46);
        }

        args.PushMarkup(
            Loc.GetString("passport-examine-pid", ("pid", GenerateIdentityString(profile.Name
                + profile.Height
                + profile.Age
                + profile.Height
                + profile.FlavorText))),
            45);
    }

    private void OnPlayerLoadoutApplied(PlayerLoadoutAppliedEvent ev) =>
        SpawnPassportForPlayer(ev.Mob, ev.Profile, ev.JobId);

    public void SpawnPassportForPlayer(EntityUid mob, HumanoidCharacterProfile profile, string? jobId)
    {
        if (jobId == null || !_prototypeManager.TryIndex(
                jobId,
                out JobPrototype? jobPrototype)
            || !jobPrototype.CanHavePassport
            || Deleted(mob)
            || !Exists(mob)
            || !ShouldSpawnPassports)
            return;

        // Ember: issued by citizenship rather than by birthplace. A faction whose Passport is null
        // issues none at all, which is not an oversight - it is what the stateless entry means, and
        // the missing document is the point of it.
        if (!_prototypeManager.TryIndex(profile.Faction, out EmberBackgroundPrototype? faction)
            || faction.Passport is not { } passportId
            || !_prototypeManager.TryIndex(passportId, out EntityPrototype? entityPrototype))
            return;

        var passportEntity = _entityManager.SpawnEntity(entityPrototype.ID, _sharedTransformSystem.GetMapCoordinates(mob));
        var passportComponent = _entityManager.GetComponent<PassportComponent>(passportEntity);

        UpdatePassportProfile(new(passportEntity, passportComponent), profile);

        // Try to find back-mounted storage apparatus
        if (_inventory.TryGetSlotEntity(mob, "back", out var item) &&
                EntityManager.TryGetComponent<StorageComponent>(item, out var inventory))
            // Try inserting the entity into the storage, if it can't, it leaves the loadout item on the ground
        {
            if (!EntityManager.TryGetComponent<ItemComponent>(passportEntity, out var itemComp)
                || !_storage.CanInsert(item.Value, passportEntity, out _, inventory, itemComp)
                || !_storage.Insert(item.Value, passportEntity, out _, playSound: false))
            {
                _adminLogManager.Add(
                    LogType.EntitySpawn,
                    LogImpact.Low,
                    $"Passport for {profile.Name} was spawned on the floor due to missing bag space");
            }
        }
    }

    private bool ShouldSpawnPassports =>
        _configManager.GetCVar(CCVar.CCVars.ContractorsEnabled) &&
        _configManager.GetCVar(CCVar.CCVars.ContractorsPassportEnabled);

    public void UpdatePassportProfile(Entity<PassportComponent> passport, HumanoidCharacterProfile profile)
    {
        passport.Comp.OwnerProfile = profile;
    }

    private static string GenerateIdentityString(string seed)
    {
        var hashCode = seed.GetHashCode();
        System.Random random = new System.Random(hashCode);

        char[] result = new char[17]; // 15 characters + 2 dashes

        int j = 0;
        for (int i = 0; i < 15; i++)
        {
            if (i == 5 || i == 10)
            {
                result[j++] = '-';
            }
            result[j++] = PIDChars[random.Next(PIDChars.Length)];
        }

        return new string(result);
    }
}
