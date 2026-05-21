using Content.Shared.SprayPainter;
using Content.Shared.Ember.Doors;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Client.SprayPainter;

public sealed class SprayPainterSystem : SharedSprayPainterSystem
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public List<SprayPainterEntry> Entries { get; private set; } = new();

    protected override void CacheStyles()
    {
        base.CacheStyles();

        Entries.Clear();
        foreach (var style in Styles)
        {
            var name = style.Name;
            if (EmberAirlockPaintStyle.TryGetPreviewPrototype(name, out var previewPrototype))
            {
                Entries.Add(new SprayPainterEntry(name, null, previewPrototype));
                continue;
            }

            string? iconPath = Groups
              .FindAll(x => x.StylePaths.ContainsKey(name))?
              .MaxBy(x => x.IconPriority)?.StylePaths[name];
            if (iconPath == null)
            {
                Entries.Add(new SprayPainterEntry(name, null));
                continue;
            }

            RSIResource doorRsi = _resourceCache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / new ResPath(iconPath));
            if (!doorRsi.RSI.TryGetState("closed", out var icon))
            {
                Entries.Add(new SprayPainterEntry(name, null));
                continue;
            }

            Entries.Add(new SprayPainterEntry(name, icon.Frame0));
        }
    }
}

public sealed class SprayPainterEntry
{
    public string Name;
    public Texture? Icon;
    public string? PreviewPrototype;

    public SprayPainterEntry(string name, Texture? icon, string? previewPrototype = null)
    {
        Name = name;
        Icon = icon;
        PreviewPrototype = previewPrototype;
    }
}
