using Content.Shared.Item;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Ember.Clothing;

/// <summary>
/// The two verbs that move a uniform between its three worn states.
/// </summary>
/// <remarks>
/// Bay's transition rules, kept because they are what makes three states feel like two switches
/// rather than a cycle:
///
/// <list type="bullet">
/// <item>Pulling down while the sleeves are up only unrolls the sleeves. The garment has to pass
/// through its ordinary state, which is also the only sequence that makes sense on a body.</item>
/// <item>Rolling sleeves while pulled down is refused outright, because there are no sleeves on
/// the arms to roll.</item>
/// </list>
///
/// The state is on the item and not on the wearer, so a uniform taken off with its sleeves up
/// goes back on with its sleeves up. Bay does the same.
/// </remarks>
public abstract class SharedEmberRollableClothingSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberRollableClothingComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(
        Entity<EmberRollableClothingComponent> ent,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;

        if (ent.Comp.CanRollSleeves)
        {
            var rolled = ent.Comp.Roll == EmberClothingRoll.Sleeves;
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => TrySetRoll(ent, rolled ? EmberClothingRoll.None : EmberClothingRoll.Sleeves, user),
                Text = Loc.GetString(rolled
                    ? "ember-clothing-unroll-sleeves-verb"
                    : "ember-clothing-roll-sleeves-verb"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png")),
                Priority = 2,
            });
        }

        if (!ent.Comp.CanRollDown)
            return;

        var down = ent.Comp.Roll == EmberClothingRoll.Down;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TrySetRoll(ent, down ? EmberClothingRoll.None : EmberClothingRoll.Down, user),
            Text = Loc.GetString(down
                ? "ember-clothing-pull-up-verb"
                : "ember-clothing-pull-down-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png")),
            Priority = 1,
        });
    }

    /// <summary>
    /// Where a garment ends up when this state is asked for, or null if the request is refused.
    /// </summary>
    /// <remarks>
    /// Pure and static so the rules can be tested without an entity. They are small and they are
    /// the whole of the feel, which is exactly the combination that gets quietly broken.
    /// </remarks>
    public static EmberClothingRoll? Resolve(
        EmberClothingRoll current,
        EmberClothingRoll requested,
        bool canRollSleeves,
        bool canRollDown)
    {
        if (current == requested)
            return null;

        return requested switch
        {
            EmberClothingRoll.Sleeves when !canRollSleeves => null,
            EmberClothingRoll.Down when !canRollDown => null,

            // Pulled down means the sleeves are already off the arms. Refused outright, and Bay
            // says so out loud — a visible verb that silently does nothing reads as a bug.
            EmberClothingRoll.Sleeves when current == EmberClothingRoll.Down => null,

            // The other direction is not an error, just a step: unrolling the sleeves leaves the
            // garment ordinary, and a second press pulls it down.
            EmberClothingRoll.Down when current == EmberClothingRoll.Sleeves => EmberClothingRoll.None,

            _ => requested,
        };
    }

    /// <summary>
    /// Moves the garment to <paramref name="roll"/>, applying Bay's refusals.
    /// </summary>
    public bool TrySetRoll(Entity<EmberRollableClothingComponent> ent, EmberClothingRoll roll, EntityUid? user = null)
    {
        if (Resolve(ent.Comp.Roll, roll, ent.Comp.CanRollSleeves, ent.Comp.CanRollDown) is not { } resolved)
        {
            if (user != null && roll == EmberClothingRoll.Sleeves && ent.Comp.Roll == EmberClothingRoll.Down)
                Popup(ent, user.Value, "ember-clothing-roll-sleeves-blocked");

            return false;
        }

        ent.Comp.Roll = resolved;
        Dirty(ent);

        // Same nudge SetEquippedPrefix uses. The sprite name is built from this component, so
        // nothing else would notice the change on its own.
        _item.VisualsChanged(ent);
        return true;
    }

    protected virtual void Popup(Entity<EmberRollableClothingComponent> ent, EntityUid user, LocId message)
    {
    }
}
