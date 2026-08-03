using System.Numerics;
using Content.Shared.Ember.Materials;

namespace Content.Shared.Ember.Structures;

/// <summary>
/// The four sets of corner sprites a table is drawn from, bottom to top. Bay adds them as separate overlays so
/// the frame shows through glass plating and the reinforcement shows over anything.
/// </summary>
public enum EmberTableLayerKind : byte
{
    Frame,
    Plating,
    Reinforcement,
    Carpet,
}

public static class EmberProceduralTableVisuals
{
    /// <summary>Felt is drawn as it was painted, so it is the one layer that takes no material colour.</summary>
    public const string CarpetStateBase = "carpet";

    /// <summary>The bare frame has no prefix at all: its corners are just <c>0</c> through <c>7</c>.</summary>
    public static string CornerState(string? stateBase, int corner)
    {
        return stateBase == null ? corner.ToString() : $"{stateBase}_{corner}";
    }

    public static string FlippedState(string? stateBase, string run)
    {
        return stateBase == null ? $"flip{run}" : $"{stateBase}_flip{run}";
    }

    /// <summary>
    /// Bay draws a flipped table by how many of its neighbours are flipped the same way, so a row of them reads
    /// as one barricade: none, one on either side, or both.
    /// </summary>
    public static string FlippedRun(bool counterClockwise, bool clockwise)
    {
        return (counterClockwise, clockwise) switch
        {
            (true, true) => "2",
            (true, false) => "1-",
            (false, true) => "1+",
            _ => "0",
        };
    }

    /// <summary>
    /// Which set of corners a material plates a table with, and what its reinforcement is drawn as. Bay reads
    /// both off the material being plated rather than off the reinforcement, which is why a steel lattice looks
    /// different depending on what it is holding together.
    /// </summary>
    public static string PlatingStateBase(EmberMaterialPrototype material) => material.TableIconBase;

    public static string ReinforcementStateBase(EmberMaterialPrototype plating) => plating.TableIconReinforced;

    /// <summary>
    /// Which way a table goes over: away from whoever is tipping it, snapped to a compass point.
    /// </summary>
    /// <remarks>
    /// Bay uses <c>get_cardinal_dir</c>, and snapping matters more for us than for it — moving between tiles
    /// rather than on them means a player is almost never exactly square with the table, and an unsnapped
    /// direction comes out diagonal nearly every time. The angle has to be taken the way entities measure
    /// rotation, where zero is south, and not the way maths does, where zero is east; the two are ninety degrees
    /// apart and the table goes over the wrong way if they are mixed up.
    /// </remarks>
    public static Direction FlipDirection(Vector2 user, Vector2 table)
    {
        return (table - user).ToWorldAngle().GetCardinalDir();
    }

    /// <summary>
    /// Turns the lip a flipped table blocks with — written for a table lying against the south edge of its own
    /// tile — round to whichever edge it actually lies against.
    /// </summary>
    public static Box2 LipFor(Box2 southern, Direction facing)
    {
        var depth = southern.Height;
        var across = southern.Width / 2f;

        return facing switch
        {
            Direction.North => new Box2(-across, 0.5f - depth, across, 0.5f),
            Direction.East => new Box2(0.5f - depth, -across, 0.5f, across),
            Direction.West => new Box2(-0.5f, -across, -0.5f + depth, across),
            _ => southern,
        };
    }

    /// <summary>
    /// Two tables join when they are the same material and both the same way up. A bare frame joins onto
    /// nothing, which is what makes an unplated table read as a separate object.
    /// </summary>
    public static bool Joins(EmberProceduralTableComponent self, EmberProceduralTableComponent other)
    {
        return self.Material != null &&
               self.Material == other.Material &&
               self.Flipped == other.Flipped;
    }
}
