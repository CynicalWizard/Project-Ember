using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared.Ember.Structures;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Bay has no phoron glass, so the maps are not supposed to have any either — they were migrated to borosilicate.
/// Phoron windows stay buildable only because phoron glass is still propping up recipes elsewhere.
/// </summary>
/// <remarks>
/// Counting these by hand is how they got missed the first time: the ids nest, and searching for PlasmaWindow
/// happily finds every ReinforcedPlasmaWindow as well. So count them here instead.
/// </remarks>
[TestFixture]
public sealed class EmberPhoronGlassOnMapsTest
{
    private const string PhoronGlass = "EmberPhoronGlass";

    private static readonly Regex PrototypeReference = new(@"^\s*-?\s*proto:\s*(\w+)\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    [Test]
    public async Task NoMapStillPlacesPhoronGlass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var resourceManager = pair.Server.ResolveDependency<IResourceManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();

        var structureName = componentFactory.GetComponentName<EmberProceduralStructureComponent>();
        var tintName = componentFactory.GetComponentName<EmberMaterialTintComponent>();

        var migrations = ReadMigrations(resourceManager);
        var problems = new List<string>();
        var maps = 0;
        var placed = new HashSet<string>();

        foreach (var path in resourceManager.ContentFindFiles(new ResPath("/Maps")))
        {
            if (path.Extension != "yml" || !resourceManager.TryContentFileRead(path, out var stream))
                continue;

            maps++;
            using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
            var seen = new HashSet<string>();

            foreach (Match match in PrototypeReference.Matches(reader.ReadToEnd()))
            {
                var id = match.Groups[1].Value;

                if (!seen.Add(id))
                    continue;

                // A map entry that names something deleted outright is somebody else's problem.
                if (migrations.TryGetValue(id, out var migrated))
                {
                    if (migrated == null)
                        continue;

                    id = migrated;
                }

                if (!protoManager.TryIndex(id, out EntityPrototype? entity))
                    continue;

                placed.Add(id);

                if (IsPhoronGlass(entity, structureName, tintName, componentFactory))
                    problems.Add($"{path} still places {match.Groups[1].Value}, which is made of phoron glass");
            }
        }

        Assert.That(maps, Is.GreaterThan(0), "No maps were read at all.");

        // Without this the whole fixture passes by reading nothing, which is exactly how it behaved when the
        // pattern did not account for the leading dash on a map's prototype entries.
        Assert.That(placed, Has.Count.GreaterThan(100), "Barely any prototypes were found; the scan is not working.");
        Assert.That(placed, Does.Contain("Window"), "Not one plain window was found on any map.");

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    private static bool IsPhoronGlass(
        EntityPrototype entity,
        string structureName,
        string tintName,
        IComponentFactory componentFactory)
    {
        if (entity.Components.TryGetComponent(structureName, out var structure) &&
            ((EmberProceduralStructureComponent) structure).Material == PhoronGlass)
        {
            return true;
        }

        return entity.Components.TryGetComponent(tintName, out var tint) &&
               ((EmberMaterialTintComponent) tint).Material == PhoronGlass;
    }

    private static Dictionary<string, string?> ReadMigrations(IResourceManager resourceManager)
    {
        var migrations = new Dictionary<string, string?>();

        foreach (var path in resourceManager.ContentFindFiles(new ResPath("/Migrations")))
        {
            if (!resourceManager.TryContentFileRead(path, out var stream))
                continue;

            using var reader = new StreamReader(stream, EncodingHelpers.UTF8);
            var document = DataNodeParser.ParseYamlStream(reader).FirstOrDefault();

            if (document?.Root is not MappingDataNode mapping)
                continue;

            foreach (var (key, value) in mapping)
            {
                if (value is not ValueDataNode to)
                    continue;

                migrations.TryAdd(key,
                    string.IsNullOrWhiteSpace(to.Value) || to.Value == "null" ? null : to.Value);
            }
        }

        return migrations;
    }
}
