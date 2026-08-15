#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Shared.Ember.Background;
using Content.Shared.Ember.Skills;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Everything Ember added to a character profile survives being written to the database and read
/// back: the four background axes, the posting, the skills and the job titles.
/// </summary>
/// <remarks>
/// This is here because the branch and the rank spent a while not being persisted at all and
/// nothing noticed. Within one session it looks like it works - the lobby holds the profile in
/// memory and pushes the whole thing on every edit - so the loss only shows up on a reconnect,
/// which is the point at which a player concludes the game ate their character rather than that
/// one field was never stored.
///
/// A field added to the profile and forgotten in <c>ConvertProfiles</c> fails exactly that way, so
/// the assertion is a round trip through the real Sqlite path rather than a check that some list of
/// columns exists.
/// </remarks>
[TestFixture]
public sealed class EmberProfilePersistenceTest
{
    private static ServerDbSqlite GetDb(RobustIntegrationTest.ServerIntegrationInstance server)
    {
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var opsLog = server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
        var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        builder.UseSqlite(conn);
        return new ServerDbSqlite(() => builder.Options, true, cfg, true, opsLog);
    }

    [Test]
    public async Task AnEmberProfileSurvivesARoundTrip()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var db = GetDb(server);

        // Deliberately not the defaults on any axis: a field that is never written still reads back
        // as its default, so a test built from defaults passes whether or not the code works.
        var branch = protoMan.EnumeratePrototypes<Content.Shared.Ember.Ranks.EmberBranchPrototype>()
            .OrderBy(o => o.ID)
            .First(o => o.Ranks.Count > 0);
        var rank = branch.Ranks[0];
        var skill = protoMan.EnumeratePrototypes<SkillPrototype>().OrderBy(o => o.ID).First();
        var job = protoMan.EnumeratePrototypes<JobPrototype>().OrderBy(o => o.ID).First();

        var original = new HumanoidCharacterProfile
        {
            Name = "Amelie Chartreuse",
            Species = "Human",
            Homeworld = "EmberHomeworldAmelia",
            Culture = "EmberCultureAmelia",
            Faction = "EmberFactionGCC",
            Religion = "EmberReligionOldBelief",
            Branch = branch.ID,
            Rank = rank,
        };

        original = original.WithSkill(skill.ID, SkillLevel.Trained)
            .WithJobTitle(job.ID, "Second Assistant to Nobody");

        var username = new NetUserId(new Guid("2f6a2b4c-1d3e-4f50-8a91-0b7c6d5e4f30"));
        await db.InitPrefsAsync(username, original);

        var loaded = (HumanoidCharacterProfile) (await db.GetPlayerPreferencesAsync(username))!
            .Characters[0];

        Assert.Multiple(() =>
        {
            Assert.That(loaded.Homeworld, Is.EqualTo(original.Homeworld), "homeworld was not stored");
            Assert.That(loaded.Culture, Is.EqualTo(original.Culture), "upbringing was not stored");
            Assert.That(loaded.Faction, Is.EqualTo(original.Faction), "allegiance was not stored");
            Assert.That(loaded.Religion, Is.EqualTo(original.Religion), "belief was not stored");
            Assert.That(loaded.Branch, Is.EqualTo(original.Branch), "branch was not stored");
            Assert.That(loaded.Rank, Is.EqualTo(original.Rank), "rank was not stored");
            Assert.That(loaded.Skills[skill.ID], Is.EqualTo(SkillLevel.Trained), "skill was not stored");
            Assert.That(loaded.JobTitles[job.ID], Is.EqualTo("Second Assistant to Nobody"),
                "job title was not stored");
        });

        await pair.CleanReturnAsync();
    }
}
