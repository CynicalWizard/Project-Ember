using System.Collections.Generic;
using System.Linq;
using Content.Shared.Maps;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// The Bay floors were baked from the sprite sheet rather than drawn by hand, so what is worth checking is
/// that the textures and the prototypes stay in step.
/// </summary>
[TestFixture]
public sealed class EmberTileTest
{
    private static readonly ResPath Textures = new("/Textures/Ember/Tiles");

    [Test]
    public async Task EveryTileTextureIsUsedAndEveryUsedTextureExists()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var resourceManager = pair.Server.ResolveDependency<IResourceManager>();

        var problems = new List<string>();
        var used = new HashSet<ResPath>();

        foreach (var tile in protoManager.EnumeratePrototypes<ContentTileDefinition>())
        {
            if (tile.Sprite is not { } sprite || !sprite.TryRelativeTo(Textures, out _))
                continue;

            used.Add(sprite);

            if (!resourceManager.ContentFileExists(sprite))
                problems.Add($"{tile.ID} draws {sprite}, which is not there");
        }

        var baked = resourceManager.ContentFindFiles(Textures).Where(f => f.Extension == "png").ToList();

        foreach (var file in baked)
        {
            if (!used.Contains(file))
                problems.Add($"{file} was baked but no tile draws it");
        }

        Assert.That(problems, Is.Empty, string.Join("\n", problems));
        Assert.That(used, Is.Not.Empty, "No Ember tile textures were found at all.");

        // Without this the sweep above passes for free if the search turns up nothing.
        Assert.That(baked, Has.Count.GreaterThanOrEqualTo(used.Count));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A resprite must not shorten the strip: the renderer skips a tile whose stored variant runs off the end
    /// of the atlas region, so an old map would come back with holes in the floor rather than the wrong floor.
    /// </summary>
    [Test]
    public async Task EveryTileHasAFrameForEveryVariant()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var resourceManager = pair.Server.ResolveDependency<IResourceManager>();

        var problems = new List<string>();

        foreach (var tile in protoManager.EnumeratePrototypes<ContentTileDefinition>())
        {
            if (tile.Sprite is not { } sprite ||
                !sprite.TryRelativeTo(Textures, out _) ||
                !resourceManager.TryContentFileRead(sprite, out var stream))
                continue;

            using var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(stream);

            var frames = image.Width / image.Height;

            if (frames < tile.Variants)
                problems.Add($"{tile.ID} wants {tile.Variants} variants but {sprite} holds {frames}");
        }

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }
}
