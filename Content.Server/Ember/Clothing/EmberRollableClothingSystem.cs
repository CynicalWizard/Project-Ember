using Content.Shared.Ember.Clothing;
using Content.Shared.Popups;

namespace Content.Server.Ember.Clothing;

public sealed class EmberRollableClothingSystem : SharedEmberRollableClothingSystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    protected override void Popup(Entity<EmberRollableClothingComponent> ent, EntityUid user, LocId message)
    {
        _popup.PopupEntity(Loc.GetString(message, ("item", ent.Owner)), user, user);
    }
}
