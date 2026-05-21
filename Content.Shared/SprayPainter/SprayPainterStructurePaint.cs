using Content.Shared.Ember.Structures;

namespace Content.Shared.SprayPainter;

public static class SprayPainterStructurePaint
{
    public static bool CanApply(EmberProceduralStructureComponent structure, SprayPainterWallMode mode)
    {
        if (structure.Role != EmberProceduralStructureRole.WallFrame)
            return false;

        return mode is SprayPainterWallMode.PaintWall or SprayPainterWallMode.ClearWallPaint;
    }

    public static void Apply(EmberProceduralStructureComponent structure, SprayPainterWallMode mode, Color? color)
    {
        switch (mode)
        {
            case SprayPainterWallMode.PaintWall:
                structure.Color = color;
                break;
            case SprayPainterWallMode.ClearWallPaint:
                structure.Color = null;
                break;
        }
    }
}
