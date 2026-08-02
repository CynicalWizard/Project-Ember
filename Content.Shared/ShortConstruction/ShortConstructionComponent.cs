using Content.Shared.RadialSelector;
using Robust.Shared.GameStates;

namespace Content.Shared.ShortConstruction;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShortConstructionComponent : Component
{
    // EMBER-TODO: these are written out by hand on every material stack, so a new recipe that consumes a
    // material is invisible from that material's radial menu until someone remembers to list it here, and a
    // material with fifty recipes would need fifty lines. It should be derived instead: gather the construction
    // prototypes whose first material step names this stack. The icons want the same treatment — they come from
    // the recipe prototype, which is the untinted sprite, so a copper low wall previews as a steel one.
    [DataField(required: true)]
    public List<RadialSelectorEntry> Entries = new();
}
