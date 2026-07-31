using NUnit.Framework;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using Content.Shared.Ember.Walls;
using System.Linq;

namespace Content.Tests.Ember;

[TestFixture]
public sealed class EmberWallMaterialTest : ContentUnitTest
{
    [Test]
    public void TestMaterialColor()
    {
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var steel = protoManager.Index<EmberWallMaterialPrototype>("EmberWallSteel");
        Assert.That(steel.Color, Is.Not.EqualTo(Robust.Shared.Maths.Color.White), "Color should not be white!");
        System.Console.WriteLine($"EmberWallSteel color is {steel.Color}");
    }
}
