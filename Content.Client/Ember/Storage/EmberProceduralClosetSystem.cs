using System.Linq;
using Content.Shared.Ember.Storage;
using Content.Shared.Labels;
using Content.Shared.Lock;
using Content.Shared.Storage;
using Content.Shared.Tools.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Ember.Storage;

/// <summary>
/// Builds a closet or a crate out of a shape, a colour and a list of markings, the way Bay does.
/// </summary>
/// <remarks>
/// Bay composites its containers into a finished icon once, at round start, in
/// <c>/singleton/closet_appearance/New()</c>. Sprite layers do the same job without the intermediate icon, so
/// this is the same recipe read out as layers: the shape, the door if it is open, the lock if it has one, the
/// markings, the interior, the lock light and the weld.
///
/// One deliberate difference. Bay blends its colour onto the base with <c>BLEND_ADD</c>, which on art this
/// light saturates almost everything to white and leaves the colour showing only along the dark edges. A
/// sprite layer multiplies instead, which keeps the shading and gives a container that reads as its colour
/// rather than as a white box with a tinted outline. The colours are Bay's; only the operator is ours.
/// </remarks>
public sealed class EmberProceduralClosetSystem : EntitySystem
{
    private const string BasePath = "/Textures/Ember/Structures/Storage/bases";
    private const string DecalPath = "/Textures/Ember/Structures/Storage/decals";

    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _cache = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberProceduralClosetComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EmberProceduralClosetComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<EmberProceduralClosetComponent, AfterAutoHandleStateEvent>(OnStateHandled);
    }

    private void OnStartup(Entity<EmberProceduralClosetComponent> ent, ref ComponentStartup args)
    {
        Rebuild(ent);
    }

    private void OnStateHandled(Entity<EmberProceduralClosetComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        Rebuild(ent);
    }

    private void OnAppearanceChange(Entity<EmberProceduralClosetComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite != null)
            UpdateVisuals(ent, args.Sprite, args.Component);
    }

    /// <summary>
    /// Lays the layers out from scratch. A style is fixed for the lifetime of a container, so this only runs
    /// when one appears or when the server hands over a new colour.
    /// </summary>
    private void Rebuild(Entity<EmberProceduralClosetComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) ||
            !_prototype.TryIndex(ent.Comp.Style, out var style))
        {
            return;
        }

        // Only the layers this owns are cleared. The foreign ones stay: the blank frames the vanilla
        // visualisers insist on setting states on, and the paper label. The label has to stay on top of the
        // container and the container has to stay on top of the blanks, so this goes in just under the label.
        Clear(sprite);

        var shape = ShapeName(style.Shape);
        var basePath = new ResPath($"{BasePath}/{shape}.rsi");
        var decalPath = new ResPath($"{DecalPath}/{ShapeName(style.Markings)}.rsi");
        var at = sprite.LayerMapTryGet(PaperLabelVisuals.Layer, out var label) ? label : sprite.AllLayers.Count();

        AddLayer(sprite, ref at, EmberClosetLayer.Base, basePath, "base");
        AddLayer(sprite, ref at, EmberClosetLayer.Door, basePath, "open");
        AddLayer(sprite, ref at, EmberClosetLayer.Lock, basePath, "lock");

        // Every marking gets a layer whether or not the sheet has it, so that the layer keys line up with the
        // style's list on every later pass. One the sheet does not have is created hidden and stays hidden.
        var decals = _cache.GetResource<RSIResource>(decalPath).RSI;
        var placeholder = decals.First().StateId.Name!;
        var index = 0;
        foreach (var decal in style.AllDecals())
        {
            var state = Resolve(decals, decal.State, open: false) ?? placeholder;
            AddLayer(sprite, ref at, DecalKey(index++), decalPath, state);
        }

        AddLayer(sprite, ref at, EmberClosetLayer.Interior, basePath, "interior");
        AddLayer(sprite, ref at, EmberClosetLayer.Light, basePath, "light");
        AddLayer(sprite, ref at, EmberClosetLayer.Welded, basePath, "welded");

        sprite.LayerSetShader(EmberClosetLayer.Light, "unshaded");

        UpdateVisuals(ent, sprite);
    }

    private void UpdateVisuals(
        Entity<EmberProceduralClosetComponent> ent,
        SpriteComponent sprite,
        AppearanceComponent? appearance = null)
    {
        if (!_prototype.TryIndex(ent.Comp.Style, out var style))
            return;

        var open = _appearance.TryGetData<bool>(ent, StorageVisuals.Open, out var isOpen, appearance) && isOpen;
        var locked = _appearance.TryGetData<bool>(ent, LockVisuals.Locked, out var isLocked, appearance) && isLocked;
        var welded = _appearance.TryGetData<bool>(ent, WeldableVisuals.IsWelded, out var isWelded, appearance) && isWelded;

        var color = ent.Comp.Color ?? style.Color;
        var decals = _cache.GetResource<RSIResource>(new ResPath($"{DecalPath}/{ShapeName(style.Markings)}.rsi")).RSI;

        SetColor(sprite, EmberClosetLayer.Base, color);
        SetColor(sprite, EmberClosetLayer.Door, color);
        SetColor(sprite, EmberClosetLayer.Lock, color);

        SetVisible(sprite, EmberClosetLayer.Base, true);
        SetVisible(sprite, EmberClosetLayer.Door, open);
        SetVisible(sprite, EmberClosetLayer.Interior, open);

        // Bay draws the lock on a closed container, and on a crate whether it is open or not, because a crate
        // keeps its clasp on the front either way. Which of those it is is a question for the art.
        var lockState = open ? "lock_open" : "lock";
        var hasLock = style.CanLock && TryState(sprite, EmberClosetLayer.Lock, lockState);
        SetVisible(sprite, EmberClosetLayer.Lock, hasLock);

        var lightState = open ? "light_open" : "light";
        var hasLight = style.CanLock && TryState(sprite, EmberClosetLayer.Light, lightState);
        SetVisible(sprite, EmberClosetLayer.Light, hasLight);
        SetColor(sprite, EmberClosetLayer.Light, locked ? Color.Red : Color.Lime);

        // A welded crate has nothing left to weld shut once it is open.
        SetVisible(sprite, EmberClosetLayer.Welded, welded && !open);

        var index = 0;
        foreach (var decal in style.AllDecals())
        {
            var key = DecalKey(index++);

            // Bay looks for an open or closed version of a marking and falls back to a single state for the
            // ones that do not change with the door. A marking that has none of the three is simply not drawn.
            var state = Resolve(decals, decal.State, open);
            SetVisible(sprite, key, state != null);

            if (state == null)
                continue;

            sprite.LayerSetState(key, state);
            SetColor(sprite, key, decal.Color ?? color);
        }
    }

    private static string? Resolve(RSI rsi, string decal, bool open)
    {
        var suffixed = $"{decal}_{(open ? "open" : "closed")}";

        if (rsi.TryGetState(suffixed, out _))
            return suffixed;

        return rsi.TryGetState(decal, out _) ? decal : null;
    }

    private static bool TryState(SpriteComponent sprite, object layer, string state)
    {
        if (!sprite.LayerMapTryGet(layer, out var index) || sprite[index].Rsi is not { } rsi)
            return false;

        if (!rsi.TryGetState(state, out _))
            return false;

        sprite.LayerSetState(layer, state);
        return true;
    }

    /// <summary>Drops the layers from a previous pass, so a restyle does not stack a second set on the first.</summary>
    private static void Clear(SpriteComponent sprite)
    {
        foreach (var layer in Enum.GetValues<EmberClosetLayer>())
        {
            if (sprite.LayerMapTryGet(layer, out _))
                sprite.RemoveLayer(layer);
        }

        for (var i = 0; sprite.LayerMapTryGet(DecalKey(i), out _); i++)
        {
            sprite.RemoveLayer(DecalKey(i));
        }
    }

    private static void AddLayer(SpriteComponent sprite, ref int at, object key, ResPath rsi, string state)
    {
        var index = sprite.AddLayer(new SpriteSpecifier.Rsi(rsi, state), at++);
        sprite.LayerMapSet(key, index);
        sprite.LayerSetVisible(index, false);
    }

    private static void SetVisible(SpriteComponent sprite, object layer, bool visible)
    {
        if (sprite.LayerMapTryGet(layer, out _))
            sprite.LayerSetVisible(layer, visible);
    }

    private static void SetColor(SpriteComponent sprite, object layer, Color color)
    {
        if (sprite.LayerMapTryGet(layer, out _))
            sprite.LayerSetColor(layer, color);
    }

    private static string DecalKey(int index)
    {
        return $"ember-closet-decal-{index}";
    }

    private static string ShapeName(EmberClosetShape shape)
    {
        return shape switch
        {
            EmberClosetShape.Crate => "crate",
            EmberClosetShape.LargeCrate => "large_crate",
            EmberClosetShape.Cabinet => "cabinet",
            EmberClosetShape.Cart => "cart",
            EmberClosetShape.Wall => "wall",
            _ => "closet",
        };
    }

    private enum EmberClosetLayer : byte
    {
        Base,
        Door,
        Lock,
        Interior,
        Light,
        Welded,
    }
}
