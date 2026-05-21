namespace Content.Shared.Ember.Structures;

public readonly record struct EmberProceduralStructureLayerCorners<T>(T SE, T NE, T NW, T SW);

public static class EmberProceduralStructureCorners
{
    public static EmberProceduralStructureLayerCorners<T> MapToLayers<T>(
        Direction facing,
        T se,
        T ne,
        T nw,
        T sw)
    {
        return facing switch
        {
            Direction.North => new(nw, sw, se, ne),
            Direction.West => new(sw, se, ne, nw),
            Direction.South => new(se, ne, nw, sw),
            _ => new(ne, nw, sw, se),
        };
    }
}
