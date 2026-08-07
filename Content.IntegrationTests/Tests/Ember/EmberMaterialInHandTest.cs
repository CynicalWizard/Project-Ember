using System.Collections.Generic;
using System.Text.Json;
using Content.Shared.Ember.Materials;
using Content.Shared.Item;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A stack of material is carried as one greyscale drawing with the material's colour over it, the way Bay
/// carries them: one line, <c>color = material.icon_colour</c>, which takes the held icon along with the object.
/// </summary>
/// <remarks>
/// The held sprite is named by a prefix rather than looked up, so nothing complains when the state it names does
/// not exist — the item is simply carried invisibly. That is what every material sheet was doing after their
/// world sprites moved to the Bay sheet and their prefixes stayed pointing at states that had never been drawn
/// there. Hence a test that reads the file rather than trusting the name.
/// </remarks>
[TestFixture]
public sealed class EmberMaterialInHandTest
{
    [Test]
    public async Task EveryStackOfMaterialIsCarriedBySomethingThatWasDrawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();
        var resources = pair.Server.ResolveDependency<IResourceManager>();

        var stackName = factory.GetComponentName<EmberMaterialStackComponent>();
        var itemName = factory.GetComponentName<ItemComponent>();
        var states = new Dictionary<ResPath, HashSet<string>>();
        var problems = new List<string>();
        var found = 0;

        foreach (var entity in prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (entity.Abstract ||
                !entity.Components.ContainsKey(stackName) ||
                !entity.Components.TryGetComponent(itemName, out var raw))
            {
                continue;
            }

            var item = (ItemComponent) raw;

            // An item with no rsi of its own falls back on the world sprite, which is not readable from here.
            if (item.RsiPath is not { } rsi)
                continue;

            found++;
            var path = new ResPath("/Textures") / rsi;

            if (!states.TryGetValue(path, out var available))
            {
                available = ReadStates(resources, path);
                states[path] = available;
            }

            foreach (var hand in new[] { "left", "right" })
            {
                var state = item.HeldPrefix == null ? $"inhand-{hand}" : $"{item.HeldPrefix}-inhand-{hand}";

                if (!available.Contains(state))
                    problems.Add($"{entity.ID} is carried as '{state}', which is not in {path}");
            }
        }

        Assert.That(found, Is.GreaterThan(0), "No stacks of material were found at all.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    private static HashSet<string> ReadStates(IResourceManager resources, ResPath rsi)
    {
        var names = new HashSet<string>();

        if (!resources.TryContentFileRead(rsi / "meta.json", out var stream))
            return names;

        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("states", out var states))
            return names;

        foreach (var state in states.EnumerateArray())
        {
            if (state.TryGetProperty("name", out var name) && name.GetString() is { } value)
                names.Add(value);
        }

        return names;
    }
}
