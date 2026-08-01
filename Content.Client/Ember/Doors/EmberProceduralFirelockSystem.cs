using Content.Client.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Doors;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.Ember.Doors;

/// <summary>
/// Ports SierraBay's firedoor visuals: the unlit layer is the pressure alert lamp rather than the vanilla
/// opening/closing glow. Which way the shutter faces is handled by
/// <see cref="EmberProceduralDoorFacingSystem"/>, shared with airlocks.
/// </summary>
public sealed class EmberProceduralFirelockSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly EmberProceduralDoorFacingSystem _facing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralFirelockComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberProceduralFirelockComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<EmberProceduralFirelockComponent, AppearanceChangeEvent>(
            OnAppearanceChange,
            after: [typeof(DoorSystem), typeof(FirelockSystem)]);
    }

    private void OnStartup(EntityUid uid, EmberProceduralFirelockComponent component, ComponentStartup args)
    {
        if (!component.Enabled)
            return;

        // Vanilla only builds a denying animation for airlocks, so the hazard shutter supplies its own.
        if (TryComp<DoorComponent>(uid, out var door))
        {
            door.DenyingAnimation = new Animation
            {
                Length = door.DenyDuration,
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = DoorVisualLayers.Base,
                        KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame(component.DenyState, 0f) },
                    },
                },
            };
        }

        _facing.UpdateFacing(uid);

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // The hazard sheet has no unlit open/close glow, only the Bay alert lamp.
        if (sprite.LayerMapTryGet(DoorVisualLayers.BaseUnlit, out _))
            sprite.LayerSetState(DoorVisualLayers.BaseUnlit, component.AlertState);

        UpdateAlert(uid, component, sprite);
    }

    private void OnAnchorChanged(EntityUid uid, EmberProceduralFirelockComponent component, ref AnchorStateChangedEvent args)
    {
        if (component.Enabled)
            _facing.DirtyDoor(uid);
    }

    private void OnAppearanceChange(EntityUid uid, EmberProceduralFirelockComponent component, ref AppearanceChangeEvent args)
    {
        if (!component.Enabled || args.Sprite == null)
            return;

        UpdateAlert(uid, component, args.Sprite, args.Component);
    }

    /// <summary>
    /// Bay drives the alert lamp off the pressure differential; SS14 only tracks a single "firelock is holding"
    /// flag, so that is what lights it up. Vanilla also flashes the unlit layer while the door moves, which the
    /// hazard sheet has no frames for, hence overriding <see cref="FirelockSystem"/> here.
    /// </summary>
    private void UpdateAlert(
        EntityUid uid,
        EmberProceduralFirelockComponent component,
        SpriteComponent sprite,
        AppearanceComponent? appearance = null)
    {
        if (!sprite.LayerMapTryGet(DoorVisualLayers.BaseUnlit, out _))
            return;

        var alarmed = _appearance.TryGetData<bool>(uid, DoorVisuals.ClosedLights, out var closedLights, appearance)
                      && closedLights;

        sprite.LayerSetState(DoorVisualLayers.BaseUnlit, component.AlertState);
        sprite.LayerSetVisible(DoorVisualLayers.BaseUnlit, alarmed);
    }
}
