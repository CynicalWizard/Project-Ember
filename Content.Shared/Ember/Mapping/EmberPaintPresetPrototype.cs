using Robust.Shared.Prototypes;

namespace Content.Shared.Ember.Mapping;

[Prototype("emberPaintPreset")]
public sealed partial class EmberPaintPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    [DataField("paintColor")]
    public Color? PaintColor { get; set; }

    [DataField("stripeColor")]
    public Color? StripeColor { get; set; }
}
