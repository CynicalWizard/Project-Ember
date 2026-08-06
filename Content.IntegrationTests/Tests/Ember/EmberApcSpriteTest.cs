using System.Collections.Generic;
using System.Linq;
using Content.Client.Power.APC;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// The APC is drawn from parts named at runtime, so nothing checks that the parts exist until one is looked at.
/// </summary>
/// <remarks>
/// Its visualiser builds every state name out of prefixes and suffixes it reads from the prototype — screen,
/// lock indicator, one per output channel — and asks the sprite for them. A name the sheet does not have draws
/// as an error, and only for the APCs that happen to be in that state: an APC whose lights are all on looks
/// perfectly fine while the "manual off" ones are broken. Bay draws these with one grey lamp per channel and
/// colours it, we ask for a state per colour, so the conversion invents twelve names that have to line up.
/// </remarks>
[TestFixture]
public sealed class EmberApcSpriteTest
{
    [Test]
    public async Task EveryPartTheApcAsksForIsInItsSheet()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var protoManager = pair.Client.ResolveDependency<IPrototypeManager>();
        var factory = pair.Client.ResolveDependency<IComponentFactory>();
        var cache = pair.Client.ResolveDependency<IResourceCache>();

        var problems = new List<string>();
        var found = 0;

        await pair.Client.WaitPost(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract ||
                    !proto.TryGetComponent<ApcVisualsComponent>(out var visuals, factory) ||
                    !proto.TryGetComponent<SpriteComponent>(out var sprite, factory))
                {
                    continue;
                }

                if (sprite.BaseRSI?.Path is not { } path)
                {
                    problems.Add($"{proto.ID} has an APC visualiser and no sheet to draw from");
                    continue;
                }

                found++;
                var rsi = cache.GetResource<RSIResource>(path).RSI;

                foreach (var wanted in Wanted(visuals))
                {
                    if (!rsi.TryGetState(wanted, out _))
                        problems.Add($"{proto.ID}: {path} has nothing called {wanted}");
                }
            }
        });

        Assert.That(found, Is.GreaterThan(0), "No APCs at all, so this test proves nothing.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>Every state name the visualiser can build, exactly as it builds them.</summary>
    private static IEnumerable<string> Wanted(ApcVisualsComponent visuals)
    {
        foreach (var suffix in visuals.ScreenSuffixes)
        {
            yield return $"{visuals.ScreenPrefix}-{suffix}";
        }

        yield return visuals.EmaggedScreenState;

        for (var i = 0; i < visuals.LockIndicators; i++)
        {
            foreach (var suffix in visuals.LockSuffixes)
            {
                yield return $"{visuals.LockPrefix}{i}-{suffix}";
            }
        }

        for (var i = 0; i < visuals.ChannelIndicators; i++)
        {
            foreach (var suffix in visuals.ChannelSuffixes)
            {
                yield return $"{visuals.ChannelPrefix}{i}-{suffix}";
            }
        }
    }
}
