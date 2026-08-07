using Content.Shared.Ember.Walls;
using NUnit.Framework;

namespace Content.Tests.Shared.Ember.Walls;

[TestFixture]
[TestOf(typeof(EmberProceduralWallStates))]
public sealed class EmberProceduralWallStatesTest
{
    [Test]
    public void HiddenOptionalLayersUseBlankState()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EmberProceduralWallStates.Paint("stone", 0, false), Is.EqualTo("blank"));
            Assert.That(EmberProceduralWallStates.Stripe(0, false), Is.EqualTo("blank"));
            Assert.That(EmberProceduralWallStates.Reinforcement(null, 0), Is.EqualTo("blank"));
        });
    }

    [Test]
    public void StripeLayerUsesGenericStripeState()
    {
        Assert.That(EmberProceduralWallStates.Stripe(0, true), Is.EqualTo("stripe0"));
    }
}
