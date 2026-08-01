using Content.Client.Doors;
using Content.Client.Wires.Visualizers;
using Content.Shared.Doors.Components;
using Content.Shared.Ember.Doors;
using Content.Shared.Roles;
using Content.Shared.Tools.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Ember.Doors;

public sealed class EmberProceduralAirlockSystem : EntitySystem
{
    private static readonly object[] DirectionalViewLayers =
    {
        DoorVisualLayers.Base,
        DoorVisualLayers.BaseUnlit,
        DoorVisualLayers.BaseBolted,
        DoorVisualLayers.BaseEmergencyAccess,
        WeldableLayers.BaseWelded,
        WiresVisualLayers.MaintenancePanel,
        EmberAirlockLayer.Color,
        EmberAirlockLayer.Fill,
        EmberAirlockLayer.Stripe,
        EmberAirlockLayer.StripeFill,
        EmberAirlockLayer.GreenLights,
        EmberAirlockLayer.DenyLights,
        EmberAirlockLayer.BoltLights,
        EmberAirlockLayer.Emag,
    };

    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly EmberProceduralDoorFacingSystem _facing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralAirlockComponent, ComponentStartup>(
            OnStartup,
            after: [typeof(AirlockSystem)]);
        SubscribeLocalEvent<EmberProceduralAirlockComponent, AppearanceChangeEvent>(
            OnAppearanceChange,
            after: [typeof(DoorSystem), typeof(AirlockSystem), typeof(WiresVisualizerSystem)]);
        SubscribeLocalEvent<EmberProceduralAirlockComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<EmberProceduralAirlockComponent, AfterAutoHandleStateEvent>(OnAfterAutoHandleState);
    }

    private void OnStartup(EntityUid uid, EmberProceduralAirlockComponent component, ComponentStartup args)
    {
        if (!component.Enabled)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Loop = false;
        SetupMappedLayer(sprite, DoorVisualLayers.Base, component.DoorSprite, "closed", true);
        SetupMappedLayer(sprite, DoorVisualLayers.BaseUnlit, component.DoorSprite, "blank", false);
        SetupMappedLayer(sprite, DoorVisualLayers.BaseBolted, component.DoorSprite, "blank", false);
        SetupMappedLayer(sprite, DoorVisualLayers.BaseEmergencyAccess, component.DoorSprite, "blank", false);
        SetupMappedLayer(sprite, WeldableLayers.BaseWelded, component.WeldedSprite, "closed", false);
        SetupMappedLayer(sprite, WiresVisualLayers.MaintenancePanel, component.PanelSprite, "closed", false);

        SetupLayer(sprite, EmberAirlockLayer.Color, component.ColorSprite, "closed");
        SetupLayer(sprite, EmberAirlockLayer.Fill, component.SteelFillSprite, "closed");
        SetupLayer(sprite, EmberAirlockLayer.Stripe, component.StripeSprite, "closed");
        SetupLayer(sprite, EmberAirlockLayer.StripeFill, component.StripeFillSprite, "closed");
        SetupLayer(sprite, EmberAirlockLayer.GreenLights, component.GreenLightsSprite, "opening");
        SetupLayer(sprite, EmberAirlockLayer.DenyLights, component.DenyLightsSprite, "deny");
        SetupLayer(sprite, EmberAirlockLayer.BoltLights, component.BoltLightsSprite, "closed");
        SetupLayer(sprite, EmberAirlockLayer.Emag, component.EmagSprite, "deny");

        ApplyLayerRendering(sprite);
        _facing.UpdateFacing(uid);
        UpdateVisuals(uid, component, sprite);
    }

    private void OnAppearanceChange(EntityUid uid, EmberProceduralAirlockComponent component, ref AppearanceChangeEvent args)
    {
        if (!component.Enabled)
            return;

        if (args.Sprite == null)
            return;

        UpdateVisuals(uid, component, args.Sprite, args.Component);
    }

    private void OnAnchorChanged(EntityUid uid, EmberProceduralAirlockComponent component, ref AnchorStateChangedEvent args)
    {
        if (component.Enabled)
            _facing.DirtyDoor(uid);
    }

    private void OnAfterAutoHandleState(EntityUid uid, EmberProceduralAirlockComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (!component.Enabled)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        UpdateVisuals(uid, component, sprite);
    }

    private void UpdateVisuals(
        EntityUid uid,
        EmberProceduralAirlockComponent component,
        SpriteComponent sprite,
        AppearanceComponent? appearance = null)
    {
        if (!_prototype.TryIndex(component.Style, out EmberAirlockStylePrototype? style))
            return;

        var visuals = EmberProceduralAirlockVisuals.Resolve(
            component,
            style,
            ResolveDepartment(style.DoorDepartment),
            ResolveDepartment(style.StripeDepartment),
            ResolveDepartment(style.WindowDepartment));

        if (!_appearance.TryGetData<DoorState>(uid, DoorVisuals.State, out var state, appearance))
            state = DoorState.Closed;

        var powered = _appearance.TryGetData<bool>(uid, DoorVisuals.Powered, out var isPowered, appearance) && isPowered;
        var boltLights = _appearance.TryGetData<bool>(uid, DoorVisuals.BoltLights, out var hasBoltLights, appearance) && hasBoltLights;
        var stateName = EmberProceduralAirlockVisuals.SpriteStateFor(state);
        var animateState = EmberProceduralAirlockVisuals.IsTransitionState(state);

        // Swapping a layer's sprite resets its rendering strategy, so the layer setup has to be reapplied here.
        ApplyLayerRendering(sprite);

        SetSpriteState(sprite, DoorVisualLayers.Base, component.DoorSprite, stateName, animateState);
        SetSpriteState(sprite, DoorVisualLayers.BaseUnlit, component.DoorSprite, "blank");
        SetSpriteState(sprite, DoorVisualLayers.BaseBolted, component.DoorSprite, "blank");
        SetSpriteState(sprite, DoorVisualLayers.BaseEmergencyAccess, component.DoorSprite, "blank");
        SetSpriteState(sprite, WeldableLayers.BaseWelded, component.WeldedSprite, "closed");
        SetSpriteState(sprite, WiresVisualLayers.MaintenancePanel, component.PanelSprite, stateName, animateState);

        SetSpriteState(sprite, EmberAirlockLayer.Color, component.ColorSprite, stateName, animateState);
        SetSpriteState(sprite, EmberAirlockLayer.Fill, FillSprite(component, visuals.Fill), stateName, animateState);
        SetSpriteState(sprite, EmberAirlockLayer.Stripe, component.StripeSprite, stateName, animateState);
        SetSpriteState(sprite, EmberAirlockLayer.StripeFill, component.StripeFillSprite, stateName, animateState);
        SetSpriteState(sprite, EmberAirlockLayer.DenyLights, component.DenyLightsSprite, "deny", state == DoorState.Denying);
        SetSpriteState(sprite, EmberAirlockLayer.BoltLights, component.BoltLightsSprite, "closed");
        SetSpriteState(sprite, EmberAirlockLayer.Emag, component.EmagSprite, "deny", state == DoorState.Emagging);

        SetLayerColorSafely(sprite, EmberAirlockLayer.Color, visuals.DoorColor ?? Color.White);
        SetLayerColorSafely(sprite, EmberAirlockLayer.Fill, visuals.FillColor);
        SetLayerColorSafely(sprite, EmberAirlockLayer.Stripe, visuals.StripeColor ?? Color.White);
        SetLayerColorSafely(sprite, EmberAirlockLayer.StripeFill, visuals.StripeColor ?? Color.White);

        SetLayerVisibleSafely(sprite, EmberAirlockLayer.Color, visuals.DoorColor != null);
        SetLayerVisibleSafely(sprite, EmberAirlockLayer.Fill, true);
        SetLayerVisibleSafely(sprite, EmberAirlockLayer.Stripe, visuals.StripeColor != null);
        SetLayerVisibleSafely(sprite, EmberAirlockLayer.StripeFill, visuals.ShowStripeFill);
        SetLayerVisibleSafely(sprite, DoorVisualLayers.BaseUnlit, false);
        SetLayerVisibleSafely(sprite, DoorVisualLayers.BaseBolted, false);
        SetLayerVisibleSafely(sprite, DoorVisualLayers.BaseEmergencyAccess, false);

        var greenVisible = powered && (state == DoorState.Opening || state == DoorState.Closing);
        var greenState = state == DoorState.Closing ? "closing" : "opening";
        SetSpriteState(sprite, EmberAirlockLayer.GreenLights, component.GreenLightsSprite, greenState, greenVisible);
        SetLayerVisibleSafely(sprite, EmberAirlockLayer.GreenLights, greenVisible);

        SetLayerVisibleSafely(sprite, EmberAirlockLayer.DenyLights, powered && state == DoorState.Denying);
        SetLayerVisibleSafely(sprite, EmberAirlockLayer.BoltLights,
            powered &&
            boltLights &&
            (state == DoorState.Closed || state == DoorState.Welded));
        SetLayerVisibleSafely(sprite, EmberAirlockLayer.Emag, powered && state == DoorState.Emagging);
    }

    private DepartmentPrototype? ResolveDepartment(ProtoId<DepartmentPrototype>? id)
    {
        if (id is not { } departmentId)
            return null;

        return _prototype.TryIndex(departmentId, out DepartmentPrototype? department)
            ? department
            : null;
    }

    private static void SetLayerColorSafely(SpriteComponent sprite, object layer, Color color)
    {
        if (sprite.LayerMapTryGet(layer, out _))
            sprite.LayerSetColor(layer, color);
    }

    private static void SetLayerVisibleSafely(SpriteComponent sprite, object layer, bool visible)
    {
        if (sprite.LayerMapTryGet(layer, out _))
            sprite.LayerSetVisible(layer, visible);
    }

    private static void SetupMappedLayer(
        SpriteComponent sprite,
        object layer,
        ResPath rsi,
        string state,
        bool visible)
    {
        var index = sprite.LayerMapReserveBlank(layer);
        sprite.LayerSetSprite(index, new SpriteSpecifier.Rsi(rsi, state));
        sprite.LayerSetVisible(index, visible);
    }

    private static void SetupLayer(SpriteComponent sprite, EmberAirlockLayer layer, ResPath rsi, string state)
    {
        if (sprite.LayerMapTryGet(layer, out _))
            sprite.LayerMapRemove(layer);

        var index = sprite.AddLayer(new SpriteSpecifier.Rsi(rsi, state));
        sprite.LayerMapSet(layer, index);
        sprite.LayerSetVisible(layer, false);

        if (IsLightLayer(layer))
            sprite.LayerSetShader(layer, "unshaded");
    }

    private static void SetSpriteState(
        SpriteComponent sprite,
        object layer,
        ResPath rsi,
        string state,
        bool autoAnimated = false)
    {
        if (!sprite.LayerMapTryGet(layer, out _))
            return;

        sprite.LayerSetSprite(layer, new SpriteSpecifier.Rsi(rsi, state));
        sprite.LayerSetAutoAnimated(layer, autoAnimated);
    }

    /// <summary>
    /// Keeps every stacked layer drawn upright and unrotated. Which RSI direction each layer picks is left to
    /// <see cref="EmberProceduralDoorFacingSystem"/>, which sets the sprite's direction override from the
    /// surrounding walls.
    /// </summary>
    private static void ApplyLayerRendering(SpriteComponent sprite)
    {
        sprite.NoRotation = true;
        sprite.GranularLayersRendering = true;

        foreach (var layer in DirectionalViewLayers)
        {
            if (!sprite.LayerMapTryGet(layer, out _))
                continue;

            sprite.LayerSetRenderingStrategy(layer, LayerRenderingStrategy.NoRotation);
            sprite.LayerSetRotation(layer, Angle.Zero);
        }
    }

    private static ResPath FillSprite(EmberProceduralAirlockComponent component, EmberAirlockFill fill)
    {
        return fill switch
        {
            EmberAirlockFill.Color => component.ColorFillSprite,
            EmberAirlockFill.Glass => component.GlassFillSprite,
            _ => component.SteelFillSprite,
        };
    }

    private static bool IsLightLayer(EmberAirlockLayer layer)
    {
        return layer is EmberAirlockLayer.GreenLights
            or EmberAirlockLayer.DenyLights
            or EmberAirlockLayer.BoltLights
            or EmberAirlockLayer.Emag;
    }

    private enum EmberAirlockLayer : byte
    {
        Color,
        Fill,
        Stripe,
        StripeFill,
        GreenLights,
        DenyLights,
        BoltLights,
        Emag,
    }
}
