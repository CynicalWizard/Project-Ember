using Content.Shared.SprayPainter;
using Content.Shared.Ember.Doors;
using Content.Shared.Ember.Storage;
using System.Text;
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

    /// <summary>The container appearances, in the same order the shared system caches their ids.</summary>
    public List<SprayPainterClosetEntry> ClosetEntries { get; private set; } = new();

    protected override void CacheStyles()
    {
        base.CacheStyles();

        CacheClosetEntries();

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

    private void CacheClosetEntries()
    {
        ClosetEntries.Clear();

        foreach (var id in ClosetStyles)
        {
            if (!Proto.TryIndex(id, out var style))
                continue;

            var shape = style.Shape == EmberClosetShape.LargeCrate
                ? "large_crate"
                : style.Shape.ToString().ToLowerInvariant();

            var rsi = _resourceCache
                .GetResource<RSIResource>(new ResPath($"/Textures/Ember/Structures/Storage/bases/{shape}.rsi"))
                .RSI;

            var icon = rsi.TryGetState("base", out var state) ? state.Frame0 : null;
            ClosetEntries.Add(new SprayPainterClosetEntry(Readable(id.Id), icon, style.Color));
        }
    }

    /// <summary>EmberClosetSecureClosetEngineeringCe -> "Secure Closet Engineering Ce".</summary>
    private static string Readable(string id)
    {
        const string prefix = "EmberCloset";
        var name = id.StartsWith(prefix) ? id[prefix.Length..] : id;

        var text = new StringBuilder(name.Length + 8);
        foreach (var c in name)
        {
            if (char.IsUpper(c) && text.Length > 0)
                text.Append(' ');

            text.Append(c);
        }

        return text.Length == 0 ? id : text.ToString();
    }
}

/// <summary>
/// One container appearance as the picker shows it: the shape's own outline, tinted the way the container
/// will be. Bay names its appearances by type path and nothing more, so the id is the label -- the same
/// answer the mapping tree gives for walls and tiles.
/// </summary>
public sealed class SprayPainterClosetEntry
{
    public string Name;
    public Texture? Icon;
    public Color Color;

    public SprayPainterClosetEntry(string name, Texture? icon, Color color)
    {
        Name = name;
        Icon = icon;
        Color = color;
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
