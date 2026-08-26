#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Ember.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Rank boards and the department patch are properties of the character, not of the post, so they
/// cannot be listed in a starting kit and are sewn on after it is worn.
/// </summary>
/// <remarks>
/// Every failure mode here is silent in play. A patch that resolves to nothing looks like a bare
/// sleeve, which is also what "this department has no patch" looks like; a rank board issued to the
/// shirt but not the jacket looks correct until a jacket goes on; and an insignia the garment will
/// not take is deleted rather than dropped, so nothing appears on the floor to notice.
/// </remarks>
[TestFixture]
public sealed class EmberInsigniaIssueTest
{
    [Test]
    public async Task ACharacterIsIssuedTheirOwnRankAndTheirPostsDepartment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();
        var insignia = entMan.System<EmberInsigniaSystem>();
        var inventory = entMan.System<InventorySystem>();
        var accessories = entMan.System<EmberAccessorySystem>();

        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var job = protoMan.Index<JobPrototype>("EmberEngineer");
            var profile = new HumanoidCharacterProfile
            {
                Branch = "EmberBranchExpeditionaryCorps",
                Rank = "EmberRankCorpsE5",
            };

            var wearer = entMan.SpawnEntity("MobHuman", testMap.GridCoords);

            // Worn before anything is issued, exactly as the spawn path does it: the coverall takes
            // the Utility cut and the jacket over it takes the Service one.
            var uniform = entMan.SpawnEntity("EmberClothingUniformUtilityExpeditionary", testMap.GridCoords);
            var jacket = entMan.SpawnEntity("EmberClothingOuterEcserviceCrew", testMap.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(inventory.TryEquip(wearer, uniform, "jumpsuit", true, force: true), Is.True);
                Assert.That(inventory.TryEquip(wearer, jacket, "outerClothing", true, force: true), Is.True);
            });

            insignia.IssueInsignia(wearer, job, profile);

            var onUniform = Attached(entMan, accessories, uniform);
            var onJacket = Attached(entMan, accessories, jacket);

            Assert.Multiple(() =>
            {
                // The rank the character holds, not the rank the post allows - the post allows two.
                Assert.That(onUniform, Does.Contain("EmberClothingAccessoryEcrankE5"),
                    "The character's own rank boards should be on the uniform.");
                Assert.That(onJacket, Does.Contain("EmberClothingAccessoryEcrankE5"),
                    "A jacket hides the shirt, so it needs its own set rather than none.");

                // The cut comes from the garment and the department from the post.
                Assert.That(onUniform, Does.Contain("EmberClothingAccessoryDeptEngineering"),
                    "The coverall takes the utility cut of the engineering patch.");
                Assert.That(onJacket, Does.Contain("EmberClothingAccessoryDeptEngineeringService"),
                    "The service jacket sews the same patch in a different place, so it is a different sprite.");

                // The failure that would otherwise go unnoticed: an insignia the garment refuses is
                // deleted, so a leak shows up as a growing pile of entities and nothing else.
                Assert.That(entMan.EntityQuery<EmberAccessoryComponent>()
                        .Count(a => entMan.GetComponent<TransformComponent>(a.Owner).ParentUid == EntityUid.Invalid),
                    Is.Zero,
                    "An insignia that was not attached should have been deleted, not left in the world.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A post whose department has no patch for this cut issues the rank and nothing else, rather
    /// than falling back to another department's.
    /// </summary>
    [Test]
    public async Task APostOutsideTheShipsOrganisationGetsNoPatch()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();
        var insignia = entMan.System<EmberInsigniaSystem>();

        await server.WaitAssertion(() =>
        {
            // The Federal Police answer to Sol, wear a badge, and have no entry in the patch table.
            var marshal = protoMan.Index<JobPrototype>("EmberFederalMarshal");
            var engineer = protoMan.Index<JobPrototype>("EmberEngineer");

            Assert.Multiple(() =>
            {
                Assert.That(insignia.GetDepartmentInsignia(marshal), Is.Null,
                    "A police officer does not wear a ship's department patch.");
                Assert.That(insignia.GetDepartmentInsignia(engineer), Is.Not.Null,
                    "Every post that is part of the ship's organisation should have one.");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A xeno serving in the Corps is issued the Cultural Exchange Programme patch; a human in the
    /// same post is not, and neither is the same xeno on a civilian contract.
    /// </summary>
    /// <remarks>
    /// The mark only means anything if it separates one from the other, and both halves of that can
    /// break quietly. A patch issued to everybody reads as decoration; a patch issued to nobody
    /// reads as a department with no art. Neither throws.
    ///
    /// The civilian case is the one that argues for hanging this off the branch rather than off the
    /// species: the same person crewing a freighter is in no programme, and if the species carried
    /// the patch they would wear it anyway.
    /// </remarks>
    [Test]
    public async Task AXenoInTheCorpsIsIssuedTheExchangePatch()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var entMan = server.ResolveDependency<IEntityManager>();
        var insignia = entMan.System<EmberInsigniaSystem>();
        var inventory = entMan.System<InventorySystem>();
        var accessories = entMan.System<EmberAccessorySystem>();

        var testMap = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var job = protoMan.Index<JobPrototype>("EmberEngineer");

            List<string> Issue(string species, string branch, string rank)
            {
                var profile = new HumanoidCharacterProfile
                {
                    Species = species,
                    Branch = branch,
                    Rank = rank,
                };

                var wearer = entMan.SpawnEntity("MobHuman", testMap.GridCoords);
                var uniform = entMan.SpawnEntity("EmberClothingUniformUtilityExpeditionary", testMap.GridCoords);
                Assert.That(inventory.TryEquip(wearer, uniform, "jumpsuit", true, force: true), Is.True);

                insignia.IssueInsignia(wearer, job, profile);
                return Attached(entMan, accessories, uniform);
            }

            var tajaran = Issue("Tajaran", "EmberBranchExpeditionaryCorps", "EmberRankCorpsE5");
            var unathi = Issue("Reptilian", "EmberBranchExpeditionaryCorps", "EmberRankCorpsE5");
            var human = Issue("Human", "EmberBranchExpeditionaryCorps", "EmberRankCorpsE5");
            var contractor = Issue("Tajaran", "EmberBranchCivilian", "EmberRankContractor");

            Assert.Multiple(() =>
            {
                Assert.That(tajaran, Does.Contain("EmberClothingAccessoryEcpatch3"),
                    "A tajaran in the Corps is here on an intergovernmental contract and wears the patch.");
                Assert.That(unathi, Does.Contain("EmberClothingAccessoryEcpatch3"),
                    "An unathi in the Corps wears it for the same reason.");
                Assert.That(human, Does.Not.Contain("EmberClothingAccessoryEcpatch3"),
                    "A citizen is in no exchange programme, and the mark is meaningless if everyone wears it.");
                Assert.That(contractor, Does.Not.Contain("EmberClothingAccessoryEcpatch3"),
                    "The patch belongs to the Corps posting, not to the species.");

                // The rank boards still arrive, which is what would break if the new issue path
                // shadowed the old one rather than running beside it.
                Assert.That(tajaran, Does.Contain("EmberClothingAccessoryEcrankE5"),
                    "The exchange patch is issued on top of the rank boards, not instead of them.");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static List<string> Attached(
        IEntityManager entMan,
        EmberAccessorySystem accessories,
        EntityUid garment)
    {
        if (!accessories.TryGetContainer(garment, out var container))
            return new List<string>();

        return container.ContainedEntities
            .Select(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID ?? string.Empty)
            .ToList();
    }
}
