using Content.Client.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Materials;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Ember.Doors;

/// <summary>
/// Ports Bay's material doors: one sheet for every material, with the state picked from the material's door
/// icon base and the tint taken from the material itself.
/// </summary>
public sealed class EmberProceduralMaterialDoorSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        // After DoorSystem's ComponentInit, which is where the door's animations are first built from its
        // sprite state fields.
        SubscribeLocalEvent<EmberProceduralMaterialDoorComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, EmberProceduralMaterialDoorComponent component, ComponentStartup args)
    {
        if (!_prototype.TryIndex(component.Material, out EmberMaterialPrototype? material) ||
            !TryComp<SpriteComponent>(uid, out var sprite) ||
            !TryComp<DoorComponent>(uid, out var door))
        {
            return;
        }

        var states = EmberMaterialDoorVisuals.StatesFor(material.DoorIconBase);

        door.OpenSpriteState = states.Open;
        door.ClosedSpriteState = states.Closed;
        door.OpeningSpriteState = states.Opening;
        door.ClosingSpriteState = states.Closing;

        // DoorSystem already captured the old names into these, so they have to be rebuilt rather than appended to.
        door.OpenSpriteStates = new List<(DoorVisualLayers, string)> { (DoorVisualLayers.Base, states.Open) };
        door.ClosedSpriteStates = new List<(DoorVisualLayers, string)> { (DoorVisualLayers.Base, states.Closed) };
        door.OpeningAnimation = BuildFlick(states.Opening, door.OpeningAnimationTime);
        door.ClosingAnimation = BuildFlick(states.Closing, door.ClosingAnimationTime);

        var initial = door.State == DoorState.Open ? states.Open : states.Closed;
        sprite.LayerSetSprite(0, new SpriteSpecifier.Rsi(component.Sprite, initial));
        sprite.LayerSetColor(0, material.Color.WithAlpha(EmberMaterialDoorVisuals.AlphaFor(material)));
    }

    private static Animation BuildFlick(string state, float length)
    {
        return new Animation
        {
            Length = TimeSpan.FromSeconds(length),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = DoorVisualLayers.Base,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(state, 0f) },
                },
            },
        };
    }
}
