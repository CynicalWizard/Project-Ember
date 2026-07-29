using System.Numerics;
using Content.Shared.Construction.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Ember.Construction;

public sealed class EmberProceduralIconSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IResourceCache _resource = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly Dictionary<string, IRenderTexture> _cache = new();
    private ProceduralIconRenderControl _renderControl = default!;

    private static readonly HashSet<string> ProceduralComponents = new()
    {
        "EmberProceduralWall",
        "EmberProceduralStructure",
        "EmberMaterialTint",
        "EmberMaterialStack",
        "EmberProceduralAirlock",
    };

    private static readonly Dictionary<string, string> ConstructionIconPrototypes = new()
    {
        { "Wall", "WallSolid" },
        { "ReinforcedWall", "WallReinforced" },
    };

    public override void Initialize()
    {
        base.Initialize();

        _renderControl = new ProceduralIconRenderControl(EntityManager);
        _ui.RootControl.AddChild(_renderControl);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _ui.RootControl.RemoveChild(_renderControl);
        _renderControl.Clear();

        foreach (var texture in _cache.Values)
        {
            texture.Dispose();
        }

        _cache.Clear();
    }

    public bool TryGetConstructionIcon(ConstructionPrototype recipe, out Texture texture)
    {
        if (ConstructionIconPrototypes.TryGetValue(recipe.ID, out var mapped) &&
            TryGetPrototypeIcon(mapped, out texture))
        {
            return true;
        }

        return TryGetPrototypeIcon(recipe.ID, out texture);
    }

    public bool TryGetPrototypeIcon(string? prototypeId, out Texture texture)
    {
        texture = Texture.Transparent;

        if (string.IsNullOrEmpty(prototypeId) ||
            !_prototype.TryIndex<EntityPrototype>(prototypeId, out var prototype, logError: false) ||
            !IsProcedural(prototype))
        {
            return false;
        }

        if (_cache.TryGetValue(prototype.ID, out var cached))
        {
            texture = cached.Texture;
            return true;
        }

        var dummy = Spawn(prototype.ID, MapCoordinates.Nullspace);
        var sprite = EnsureComp<SpriteComponent>(dummy);
        _appearance.OnChangeData(dummy, sprite);

        var size = SpriteSize(sprite);
        if (size == Vector2i.Zero)
        {
            Del(dummy);
            texture = _resource.GetFallback<TextureResource>().Texture;
            return true;
        }

        var renderTarget = _clyde.CreateRenderTarget(
            size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
            name: $"ember-procedural-icon-{prototype.ID}");

        _cache[prototype.ID] = renderTarget;
        _renderControl.Enqueue(renderTarget, dummy);
        texture = renderTarget.Texture;
        return true;
    }

    private static bool IsProcedural(EntityPrototype prototype)
    {
        foreach (var component in ProceduralComponents)
        {
            if (prototype.Components.ContainsKey(component))
                return true;
        }

        return false;
    }

    private static Vector2i SpriteSize(SpriteComponent sprite)
    {
        var size = Vector2i.Zero;

        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible)
                continue;

            size = Vector2i.ComponentMax(size, layer.PixelSize);
        }

        return size;
    }

    private sealed class ProceduralIconRenderControl : Control
    {
        private readonly IEntityManager _entity;
        private readonly Queue<(IRenderTexture Texture, EntityUid Entity)> _queue = new();

        public ProceduralIconRenderControl(IEntityManager entity)
        {
            _entity = entity;
        }

        public void Enqueue(IRenderTexture texture, EntityUid entity)
        {
            _queue.Enqueue((texture, entity));
        }

        public void Clear()
        {
            while (_queue.TryDequeue(out var queued))
            {
                if (_entity.EntityExists(queued.Entity))
                    _entity.DeleteEntity(queued.Entity);
            }
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            while (_queue.TryDequeue(out var queued))
            {
                if (!_entity.EntityExists(queued.Entity))
                    continue;

                handle.RenderInRenderTarget(queued.Texture, () =>
                {
                    handle.DrawEntity(
                        queued.Entity,
                        queued.Texture.Size / 2,
                        Vector2.One,
                        Angle.Zero,
                        overrideDirection: Direction.South);
                }, Color.Transparent);

                _entity.DeleteEntity(queued.Entity);
            }
        }
    }
}
