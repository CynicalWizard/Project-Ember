using Content.Shared.SprayPainter;
using Content.Shared.SprayPainter.Components;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.SprayPainter;

[TestFixture]
[TestOf(typeof(SprayPainterColorSelection))]
public sealed class SprayPainterColorSelectionTest
{
    [Test]
    public void CustomColorWinsOverPaletteSelection()
    {
        var customColor = Color.FromHex("#224466");
        var painter = new SprayPainterComponent
        {
            PickedCustomColor = true,
            CustomColor = customColor,
            PickedColor = "red",
            ColorPalette = { ["red"] = Color.Red },
        };

        Assert.That(SprayPainterColorSelection.TryGetPickedColor(painter, out var color), Is.True);
        Assert.That(color, Is.EqualTo(customColor.WithAlpha(1f)));
    }

    [Test]
    public void PaletteColorIsUsedWhenCustomColorIsNotPicked()
    {
        var paletteColor = Color.FromHex("#52B4E9");
        var painter = new SprayPainterComponent
        {
            PickedColor = "engineering",
            ColorPalette = { ["engineering"] = paletteColor },
        };

        Assert.That(SprayPainterColorSelection.TryGetPickedColor(painter, out var color), Is.True);
        Assert.That(color, Is.EqualTo(paletteColor));
    }

    [Test]
    public void MissingPaletteSelectionReturnsFalse()
    {
        var painter = new SprayPainterComponent
        {
            PickedColor = "missing",
        };

        Assert.That(SprayPainterColorSelection.TryGetPickedColor(painter, out _), Is.False);
    }
}
