using Content.Shared.Ember.Materials;

namespace Content.Shared.Ember.Doors;

public readonly record struct EmberMaterialDoorStates(
    string Closed,
    string Open,
    string Opening,
    string Closing);

public static class EmberMaterialDoorVisuals
{
    /// <summary>
    /// Icon bases the ported sheet actually draws. Bay's own data references a "cult" base that its
    /// material_doors.dmi never had, so a base that is not here has to fall back rather than be trusted.
    /// </summary>
    public static readonly string[] KnownBases = { "metal", "stone", "wood", "plastic", "resin" };

    public const string FallbackBase = "metal";

    /// <summary>
    /// Bay builds every state by suffixing the icon base, which is what lets one sheet cover every material.
    /// </summary>
    public static EmberMaterialDoorStates StatesFor(string iconBase)
    {
        var resolved = Resolve(iconBase);

        return new EmberMaterialDoorStates(
            resolved,
            resolved + "open",
            resolved + "opening",
            resolved + "closing");
    }

    public static string Resolve(string iconBase)
    {
        return Array.IndexOf(KnownBases, iconBase) >= 0 ? iconBase : FallbackBase;
    }

    /// <summary>
    /// Bay treats a material as glass below half opacity: the door stops blocking sight and is drawn
    /// see-through.
    /// </summary>
    public static bool IsGlass(EmberMaterialPrototype material)
    {
        return material.Opacity < 0.5f;
    }

    /// <summary>
    /// Alpha Bay draws the door at. Solid materials are opaque; glass ones use its fixed 180/255.
    /// </summary>
    public static float AlphaFor(EmberMaterialPrototype material)
    {
        return IsGlass(material) ? 180f / 255f : 1f;
    }
}
