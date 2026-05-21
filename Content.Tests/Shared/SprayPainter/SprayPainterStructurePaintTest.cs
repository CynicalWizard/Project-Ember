using Content.Shared.Ember.Structures;
using Content.Shared.Ember.Walls;
using Content.Shared.SprayPainter;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.SprayPainter;

[TestFixture]
[TestOf(typeof(SprayPainterStructurePaint))]
public sealed class SprayPainterStructurePaintTest
{
    [Test]
    public void WallFrameCanUseWallPaintModes()
    {
        var frame = new EmberProceduralStructureComponent
        {
            Role = EmberProceduralStructureRole.WallFrame,
            Material = "EmberWallSteel",
            StateBase = "frame",
        };

        Assert.Multiple(() =>
        {
            Assert.That(SprayPainterStructurePaint.CanApply(frame, SprayPainterWallMode.PaintWall), Is.True);
            Assert.That(SprayPainterStructurePaint.CanApply(frame, SprayPainterWallMode.ClearWallPaint), Is.True);
            Assert.That(SprayPainterStructurePaint.CanApply(frame, SprayPainterWallMode.PaintStripe), Is.False);
            Assert.That(SprayPainterStructurePaint.CanApply(frame, SprayPainterWallMode.ClearStripe), Is.False);
        });
    }

    [Test]
    public void PaintWallFrameSetsColorOverride()
    {
        var frame = new EmberProceduralStructureComponent
        {
            Role = EmberProceduralStructureRole.WallFrame,
            Material = "EmberWallSteel",
            StateBase = "frame",
        };
        var color = Color.FromHex("#667788");

        SprayPainterStructurePaint.Apply(frame, SprayPainterWallMode.PaintWall, color);

        Assert.That(frame.Color, Is.EqualTo(color));
    }

    [Test]
    public void ClearWallFrameRestoresMaterialColor()
    {
        var frame = new EmberProceduralStructureComponent
        {
            Role = EmberProceduralStructureRole.WallFrame,
            Material = "EmberWallSteel",
            StateBase = "frame",
            Color = Color.Red,
        };

        SprayPainterStructurePaint.Apply(frame, SprayPainterWallMode.ClearWallPaint, null);

        Assert.That(frame.Color, Is.Null);
    }
}
