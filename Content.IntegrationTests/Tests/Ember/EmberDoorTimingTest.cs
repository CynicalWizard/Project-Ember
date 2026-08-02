using System.Collections.Generic;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Materials;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// Nothing ties a door's animation length to the sprite sheet it plays or to how long the door actually takes to
/// operate, so the three drift apart silently. HighSecDoor shipped that way: it hangs off BaseStructure instead
/// of Airlock, so widening the base timings for the 1.2 second Bay sheet never reached it and its closing
/// animation was cut in half.
/// </summary>
[TestFixture]
public sealed class EmberDoorTimingTest
{
    [Test]
    public async Task ProceduralDoorAnimationsFitTheirSheetAndTheirOperatingTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var componentFactory = pair.Server.ResolveDependency<IComponentFactory>();
        var cache = pair.Client.ResolveDependency<IResourceCache>();

        var airlockName = componentFactory.GetComponentName<EmberProceduralAirlockComponent>();
        var materialDoorName = componentFactory.GetComponentName<EmberProceduralMaterialDoorComponent>();
        var doorName = componentFactory.GetComponentName<DoorComponent>();

        var problems = new List<string>();

        await pair.Client.WaitPost(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.Components.TryGetComponent(doorName, out var rawDoor))
                    continue;

                var door = (DoorComponent) rawDoor;

                if (proto.Components.TryGetComponent(airlockName, out var rawAirlock))
                {
                    var airlock = (EmberProceduralAirlockComponent) rawAirlock;
                    CheckState(cache, airlock.DoorSprite, "opening", door.OpeningAnimationTime, proto.ID, problems);
                    CheckState(cache, airlock.DoorSprite, "closing", door.ClosingAnimationTime, proto.ID, problems);
                }
                else if (proto.Components.TryGetComponent(materialDoorName, out var rawMaterialDoor))
                {
                    var materialDoor = (EmberProceduralMaterialDoorComponent) rawMaterialDoor;
                    var iconBase = protoManager.TryIndex(materialDoor.Material, out EmberMaterialPrototype? material)
                        ? EmberMaterialDoorVisuals.Resolve(material.DoorIconBase)
                        : EmberMaterialDoorVisuals.FallbackBase;
                    var states = EmberMaterialDoorVisuals.StatesFor(iconBase);

                    CheckState(cache, materialDoor.Sprite, states.Opening, door.OpeningAnimationTime, proto.ID, problems);
                    CheckState(cache, materialDoor.Sprite, states.Closing, door.ClosingAnimationTime, proto.ID, problems);
                }
                else
                {
                    continue;
                }

                // SharedDoorSystem leaves the transition state after these two, and the client stops the
                // animation the moment it does, so anything longer is simply never seen.
                var opening = (door.OpenTimeOne + door.OpenTimeTwo).TotalSeconds;
                if (opening + 0.001 < door.OpeningAnimationTime)
                {
                    problems.Add(
                        $"{proto.ID} opens in {opening:0.##}s but its opening animation is {door.OpeningAnimationTime:0.##}s");
                }

                var closing = (door.CloseTimeOne + door.CloseTimeTwo).TotalSeconds;
                if (closing + 0.001 < door.ClosingAnimationTime)
                {
                    problems.Add(
                        $"{proto.ID} closes in {closing:0.##}s but its closing animation is {door.ClosingAnimationTime:0.##}s");
                }
            }
        });

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    private static void CheckState(
        IResourceCache cache,
        Robust.Shared.Utility.ResPath rsi,
        string state,
        float animationTime,
        string protoId,
        List<string> problems)
    {
        // DoorSprite is already rooted at /Textures, unlike the relative paths DoorVisuals.BaseRSI carries.
        if (!cache.TryGetResource<RSIResource>(rsi, out var resource))
        {
            problems.Add($"{protoId} points at {rsi}, which does not load");
            return;
        }

        if (!resource.RSI.TryGetState(state, out var rsiState))
        {
            problems.Add($"{protoId}'s sheet {rsi} has no '{state}' state");
            return;
        }

        var length = 0f;
        for (var i = 0; i < rsiState.DelayCount; i++)
        {
            length += rsiState.GetDelay(i);
        }

        if (Math.Abs(length - animationTime) > 0.001f)
        {
            problems.Add($"{protoId}'s '{state}' animation is {animationTime:0.##}s but the sheet runs {length:0.##}s");
        }
    }
}
