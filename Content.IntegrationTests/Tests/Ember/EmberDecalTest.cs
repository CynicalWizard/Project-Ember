using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Content.Shared.Decals;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// The Bay decals were generated from the sprite sheet rather than written by hand, so the thing worth checking
/// is that the two never drift: a decal naming a state that is not in the sheet draws as a missing texture, and
/// a state nobody names is a decal quietly missing from the mapper's list.
/// </summary>
[TestFixture]
public sealed class EmberDecalTest
{
    private const string Prefix = "EmberDecal";

    private static readonly ResPath TextureRoot = new("/Textures");

    [Test]
    public async Task EveryDecalNamesAStateThatExists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var resourceManager = pair.Server.ResolveDependency<IResourceManager>();

        var sheets = new Dictionary<ResPath, HashSet<string>>();
        var used = new Dictionary<ResPath, HashSet<string>>();
        var problems = new List<string>();
        var found = 0;

        foreach (var decal in protoManager.EnumeratePrototypes<DecalPrototype>())
        {
            if (!decal.ID.StartsWith(Prefix) || decal.Sprite is not SpriteSpecifier.Rsi rsi)
                continue;

            found++;

            if (!sheets.TryGetValue(rsi.RsiPath, out var states))
            {
                states = ReadStates(resourceManager, rsi.RsiPath);
                sheets[rsi.RsiPath] = states;
            }

            if (!states.Contains(rsi.RsiState))
                problems.Add($"{decal.ID} draws '{rsi.RsiState}', which is not in {rsi.RsiPath}");

            used.GetOrNew(rsi.RsiPath).Add(rsi.RsiState);
        }

        foreach (var (path, states) in sheets)
        {
            foreach (var state in states)
            {
                if (!used[path].Contains(state))
                    problems.Add($"{path} has '{state}', which no decal offers to the mapper");
            }
        }

        Assert.That(problems, Is.Empty, string.Join("\n", problems));
        Assert.That(found, Is.GreaterThan(0), "No Ember decals were found at all.");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The mapping window builds its tree out of prototype inheritance, so a decal with no parent lands loose at
    /// the top of the list rather than in its category.
    /// </summary>
    [Test]
    public async Task EveryDecalSitsInACategory()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();

        var problems = new List<string>();
        var categories = new HashSet<string>();

        foreach (var decal in protoManager.EnumeratePrototypes<DecalPrototype>())
        {
            if (!decal.ID.StartsWith(Prefix))
                continue;

            if (decal.Parents is not { Length: > 0 } parents)
            {
                problems.Add($"{decal.ID} has no category, so it lands loose at the top of the list");
                continue;
            }

            categories.Add(parents[0]);
        }

        Assert.That(problems, Is.Empty, string.Join("\n", problems));
        Assert.That(categories, Has.Count.GreaterThan(1), "Every decal ended up in the same category.");

        await pair.CleanReturnAsync();
    }

    private static HashSet<string> ReadStates(IResourceManager resourceManager, ResPath rsi)
    {
        var names = new HashSet<string>();

        // A decal's sprite path is written relative to the texture root, the way it appears in YAML.
        if (!resourceManager.TryContentFileRead(TextureRoot / rsi / "meta.json", out var stream))
            return names;

        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("states", out var states))
            return names;

        foreach (var state in states.EnumerateArray())
        {
            if (!state.TryGetProperty("name", out var name) || name.GetString() is not { } value)
                continue;

            // A decal is one texture that the editor turns. A state with directions therefore loses every frame
            // but the first, which is how Bay's multi-part decals — the quarters of a logo, the bevelled edges of
            // a border — would quietly go missing.
            if (state.TryGetProperty("directions", out var directions) && directions.GetInt32() > 1)
            {
                Assert.Fail($"{rsi} state '{value}' has {directions.GetInt32()} directions; " +
                            "split it into separate states when the sheet is built.");
            }

            names.Add(value);
        }

        return names;
    }
}
