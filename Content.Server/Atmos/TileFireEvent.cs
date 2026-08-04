namespace Content.Server.Atmos
{
    /// <summary>
    ///     Event raised directed to an entity when it is standing on a tile that's on fire.
    /// </summary>
    [ByRefEvent]
    public readonly struct TileFireEvent
    {
        public readonly float Temperature;
        public readonly float Volume;

        public TileFireEvent(float temperature, float volume)
        {
            Temperature = temperature;
            Volume = volume;
        }
    }

    /// <summary>
    ///     Event raised directed to an entity on a tile beside one that's on fire.
    /// </summary>
    /// <remarks>
    ///     Anything that seals its own tile — a wall, a window, a shut airlock — can never have a fire on it,
    ///     because a hotspot needs air and there is none. Bay reaches those through <c>adjacent_fire_act</c>,
    ///     and this is the same door: the things that burn from where they stand hear
    ///     <see cref="TileFireEvent"/>, and the things a fire can only lick at hear this.
    /// </remarks>
    [ByRefEvent]
    public readonly struct AdjacentTileFireEvent
    {
        public readonly float Temperature;
        public readonly float Volume;

        public AdjacentTileFireEvent(float temperature, float volume)
        {
            Temperature = temperature;
            Volume = volume;
        }
    }
}
