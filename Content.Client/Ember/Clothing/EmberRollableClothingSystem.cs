using Content.Shared.Ember.Clothing;

namespace Content.Client.Ember.Clothing;

/// <summary>
/// Client half of the roll verbs. The sprite itself is chosen in
/// <see cref="Content.Client.Clothing.ClientClothingSystem"/>, which reads
/// <see cref="EmberRollableClothingComponent.Roll"/> when it builds the worn state name.
/// </summary>
public sealed class EmberRollableClothingSystem : SharedEmberRollableClothingSystem
{
}
