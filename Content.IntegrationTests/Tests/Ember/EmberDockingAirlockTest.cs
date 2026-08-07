using System.Collections.Generic;
using System.Linq;
using Content.Server.Shuttles.Components;
using Content.Shared.Ember.Doors;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Ember;

/// <summary>
/// A docking port is an external airlock in a colour of its own, and the one door whose rotation is data rather
/// than decoration.
/// </summary>
/// <remarks>
/// Two ports connect when each one's world rotation points at the other, so which way a port faces decides
/// whether a shuttle can arrive at all. That has to be visible to whoever places it — hence the collar along
/// the edge it faces — and it has to be visible truthfully, which means the picture is not allowed to be turned
/// off that rotation to sit square in a wall the way every other door is.
/// </remarks>
[TestFixture]
public sealed class EmberDockingAirlockTest
{
    [Test]
    public async Task EveryDockingPortShowsWhichWayItFaces()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        var problems = new List<string>();
        var found = 0;

        await pair.Server.WaitPost(() =>
        {
            foreach (var proto in protoManager.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.TryGetComponent<DockingComponent>(out _, factory))
                    continue;

                found++;

                if (!proto.TryGetComponent<EmberProceduralAirlockComponent>(out var airlock, factory))
                {
                    problems.Add($"{proto.ID} is a docking port drawn by something else entirely");
                    continue;
                }

                if (!airlock.Enabled)
                    problems.Add($"{proto.ID} is a docking port still on the old flat sprite");

                if (!airlock.Docking)
                    problems.Add($"{proto.ID} is a docking port with no collar, so it does not show its facing");

                if (airlock.FacesWalls)
                {
                    problems.Add($"{proto.ID} is a docking port that turns its picture to fit the wall, so it "
                        + "will show a facing shuttles cannot arrive from");
                }
            }
        });

        Assert.That(found, Is.GreaterThan(0), "No docking ports at all, so this test proves nothing.");
        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Whichever way a docking port is turned, the collar is drawn on the edge a shuttle has to arrive at.
    /// </summary>
    /// <remarks>
    /// Doors are normally turned to face their neighbouring walls by an offset applied to every layer, which
    /// leaves the drawn direction and the entity's own rotation disagreeing. On any other door that is what you
    /// want. Here the two must agree, so this asks for the offset directly rather than trusting the flag.
    /// </remarks>
    [Test]
    public async Task ADockingPortIsDrawnFacingTheWayItDocks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var serverEnts = server.ResolveDependency<IEntityManager>();
        var clientEnts = pair.Client.ResolveDependency<IEntityManager>();
        var transform = server.System<SharedTransformSystem>();
        var map = await pair.CreateTestMap();

        var problems = new List<string>();

        foreach (var spin in new[] { 0, 90, 180, 270 })
        {
            EntityUid airlock = default;

            await server.WaitPost(() =>
            {
                airlock = serverEnts.SpawnEntity("AirlockShuttle", map.GridCoords);
                transform.SetLocalRotation(airlock, Angle.FromDegrees(spin));
            });

            await pair.RunTicksSync(15);

            await pair.Client.WaitPost(() =>
            {
                var clientUid = clientEnts.GetEntity(serverEnts.GetNetEntity(airlock));
                var sprite = clientEnts.GetComponent<SpriteComponent>(clientUid);

                var offsets = sprite.AllLayers
                    .OfType<SpriteComponent.Layer>()
                    .Where(layer => layer.Visible && layer.DirOffset != SpriteComponent.DirectionOffset.None)
                    .Select(layer => layer.DirOffset)
                    .Distinct()
                    .ToList();

                if (offsets.Count > 0)
                {
                    problems.Add($"turned {spin}, it is drawn turned a further {string.Join(",", offsets)} "
                        + "to fit the wall, which is not where it docks");
                }
            });

            await server.WaitPost(() => serverEnts.DeleteEntity(airlock));
        }

        Assert.That(problems, Is.Empty, string.Join("\n", problems));

        await pair.CleanReturnAsync();
    }
}
