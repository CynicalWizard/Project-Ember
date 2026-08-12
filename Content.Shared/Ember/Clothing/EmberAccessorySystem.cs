using System.Diagnostics.CodeAnalysis;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Temperature;
using Content.Shared.Verbs;
using Robust.Shared.Containers;

namespace Content.Shared.Ember.Clothing;

/// <summary>
/// Attaches accessories to clothing and takes them back off again.
/// </summary>
/// <remarks>
/// Ported from SierraBay12's code/modules/clothing/clothing_accessories.dm. The accessory list is
/// a container here, so deletion, stripping and storage all come for free; what remains is the
/// category validation, the interaction entry points, and relaying wearer effects down into the
/// attached accessories the way Bay's update_accessory_slowdown / armour aggregation did.
/// </remarks>
public sealed class EmberAccessorySystem : EntitySystem
{
    /// <summary>
    /// Where <see cref="TryAttachToWearer"/> looks for a holder, in order.
    /// </summary>
    private static readonly string[] PreferredHolderSlots = ["jumpsuit", "outerClothing"];

    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EmberAccessoryHolderComponent, ComponentInit>(OnHolderInit);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, InteractUsingEvent>(OnHolderInteractUsing);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, ExaminedEvent>(OnHolderExamined);

        SubscribeAllEvent<EmberAccessoryDetachRequestEvent>(OnDetachRequest);

        InitializeRelay();
    }

    #region Setup

    private void OnHolderInit(Entity<EmberAccessoryHolderComponent> holder, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(holder, holder.Comp.ContainerId);
    }

    #endregion

    #region Queries

    /// <summary>
    /// The container holding this clothing's accessories, looked up fresh every time.
    /// </summary>
    /// <remarks>
    /// Deliberately not cached on the component. On the client the container's contents arrive as
    /// entity state, and that can land before this component's ComponentInit has run - a cached
    /// reference is still null at exactly the moment the insert fires, so every redraw triggered by
    /// it quietly did nothing and the accessory never appeared. The container itself is always
    /// present by then, so asking the container manager for it by id is both correct and cheap.
    /// </remarks>
    public bool TryGetContainer(
        Entity<EmberAccessoryHolderComponent?> holder,
        [NotNullWhen(true)] out BaseContainer? container)
    {
        container = null;

        return Resolve(holder, ref holder.Comp, false)
            && _container.TryGetContainer(holder, holder.Comp.ContainerId, out container);
    }

    /// <summary>
    /// Everything currently attached to this holder. Empty if it holds nothing.
    /// </summary>
    public IReadOnlyList<EntityUid> GetAccessories(Entity<EmberAccessoryHolderComponent?> holder)
    {
        return TryGetContainer(holder, out var container)
            ? container.ContainedEntities
            : Array.Empty<EntityUid>();
    }

    /// <summary>
    /// Finds the clothing an accessory is currently attached to, if it is attached at all.
    /// Bay equivalent: the accessory's "parent" var.
    /// </summary>
    public bool TryGetHolder(EntityUid accessory, out Entity<EmberAccessoryHolderComponent> holder)
    {
        holder = default;

        if (!_container.TryGetContainingContainer((accessory, null, null), out var container))
            return false;

        if (!TryComp<EmberAccessoryHolderComponent>(container.Owner, out var holderComp))
            return false;

        if (container.ID != holderComp.ContainerId)
            return false;

        holder = (container.Owner, holderComp);
        return true;
    }

    /// <summary>
    /// Whether this accessory may be attached to this holder right now.
    /// </summary>
    /// <remarks>
    /// Bay equivalent: can_attach_accessory(). The bulk check Bay performs (CLOTHING_BULKY against
    /// other worn clothing) has no SS14 analogue and is left to
    /// <see cref="EmberAccessoryAttachAttemptEvent"/> subscribers instead.
    /// </remarks>
    public bool CanAttach(
        Entity<EmberAccessoryHolderComponent> holder,
        Entity<EmberAccessoryComponent> accessory,
        EntityUid? user,
        out string? reason)
    {
        reason = null;

        if (!TryGetContainer(holder.Owner, out var container))
        {
            reason = Loc.GetString("ember-accessory-no-attachments", ("clothing", holder.Owner));
            return false;
        }

        if (holder.Owner == accessory.Owner)
        {
            reason = Loc.GetString("ember-accessory-self-attach", ("clothing", holder.Owner));
            return false;
        }

        if (container.Contains(accessory))
        {
            reason = Loc.GetString("ember-accessory-already-attached",
                ("accessory", accessory.Owner),
                ("clothing", holder.Owner));
            return false;
        }

        if (holder.Comp.ValidSlots.Count == 0)
        {
            reason = Loc.GetString("ember-accessory-no-attachments", ("clothing", holder.Owner));
            return false;
        }

        if (!holder.Comp.ValidSlots.Contains(accessory.Comp.Slot))
        {
            reason = Loc.GetString("ember-accessory-wrong-slot",
                ("accessory", accessory.Owner),
                ("clothing", holder.Owner));
            return false;
        }

        if (container.Count >= holder.Comp.MaxAccessories)
        {
            reason = Loc.GetString("ember-accessory-too-many", ("clothing", holder.Owner));
            return false;
        }

        var limit = GetSlotLimit(holder.Comp, accessory.Comp.Slot);
        if (CountInSlot(container, accessory.Comp.Slot) >= limit)
        {
            reason = Loc.GetString("ember-accessory-slot-occupied",
                ("clothing", holder.Owner),
                ("limit", limit));
            return false;
        }

        var attempt = new EmberAccessoryAttachAttemptEvent(holder, accessory, user);
        RaiseLocalEvent(holder, attempt);
        if (!attempt.Cancelled)
            RaiseLocalEvent(accessory, attempt);

        if (attempt.Cancelled)
        {
            reason = attempt.Reason ?? Loc.GetString("ember-accessory-attach-refused",
                ("accessory", accessory.Owner),
                ("clothing", holder.Owner));
            return false;
        }

        return true;
    }

    /// <summary>
    /// How many accessories of this category the holder accepts at once.
    /// </summary>
    public int GetSlotLimit(EmberAccessoryHolderComponent holder, EmberAccessorySlot slot)
    {
        return holder.SlotLimits.TryGetValue(slot, out var limit) ? limit : holder.DefaultSlotLimit;
    }

    private int CountInSlot(BaseContainer container, EmberAccessorySlot slot)
    {
        var count = 0;
        foreach (var attached in container.ContainedEntities)
        {
            if (TryComp<EmberAccessoryComponent>(attached, out var comp) && comp.Slot == slot)
                count++;
        }

        return count;
    }

    #endregion

    #region Attaching

    /// <summary>
    /// Attaches an accessory to a holder, if it is allowed. Tells the user why when it is not.
    /// </summary>
    public bool TryAttach(
        Entity<EmberAccessoryHolderComponent> holder,
        Entity<EmberAccessoryComponent> accessory,
        EntityUid? user)
    {
        if (!CanAttach(holder, accessory, user, out var reason))
        {
            if (user != null && reason != null)
                _popup.PopupClient(reason, holder, user.Value);

            return false;
        }

        if (!TryGetContainer(holder.Owner, out var container) || !_container.Insert(accessory.Owner, container))
        {
            if (user != null)
            {
                _popup.PopupClient(
                    Loc.GetString("ember-accessory-attach-refused",
                        ("accessory", accessory.Owner),
                        ("clothing", holder.Owner)),
                    holder,
                    user.Value);
            }

            return false;
        }

        var ev = new EmberAccessoryAttachedEvent(holder, accessory, user);
        RaiseLocalEvent(accessory, ev);
        RaiseLocalEvent(holder, ev);

        // Redraws the wearer so the accessory shows up on top of the clothing it was attached to.
        _item.VisualsChanged(holder);

        if (user != null)
        {
            _popup.PopupClient(
                Loc.GetString("ember-accessory-attached",
                    ("accessory", accessory.Owner),
                    ("clothing", holder.Owner)),
                holder,
                user.Value);
        }

        return true;
    }

    /// <summary>
    /// Tries the holder itself, then every accessory already attached to it, then their accessories.
    /// </summary>
    /// <remarks>
    /// Bay equivalent: attempt_attach_accessory(), which recurses so that pouches can be hung off a
    /// webbing rig that is itself attached to a uniform. Only the outermost failure is reported, so
    /// the player is not spammed with a refusal from every level of nesting.
    /// </remarks>
    public bool TryAttachRecursive(
        Entity<EmberAccessoryHolderComponent> holder,
        Entity<EmberAccessoryComponent> accessory,
        EntityUid? user)
    {
        if (TryAttachNested(holder, accessory))
            return true;

        // Nothing in the tree took it - run the top level again so the user hears why.
        return TryAttach(holder, accessory, user);
    }

    /// <summary>
    /// Depth-first attach with no user, so a refusal deep in the tree stays silent.
    /// </summary>
    private bool TryAttachNested(
        Entity<EmberAccessoryHolderComponent> holder,
        Entity<EmberAccessoryComponent> accessory)
    {
        if (TryAttach(holder, accessory, user: null))
            return true;

        if (!TryGetContainer(holder.Owner, out var container))
            return false;

        foreach (var attached in container.ContainedEntities)
        {
            if (!TryComp<EmberAccessoryHolderComponent>(attached, out var nested))
                continue;

            if (TryAttachNested((attached, nested), accessory))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attaches an accessory to something the wearer already has on: the uniform first, then the
    /// outer clothing, then anything else that will take it.
    /// </summary>
    /// <remarks>
    /// Bay does the same while dressing a character - outfit.dm hands accessories to w_uniform and
    /// the character preview falls back to the suit. This is what makes an accessory picked in a
    /// loadout end up on the uniform instead of in the backpack.
    /// </remarks>
    public bool TryAttachToWearer(EntityUid wearer, EntityUid accessory)
    {
        if (!TryComp<EmberAccessoryComponent>(accessory, out var accessoryComp))
            return false;

        foreach (var slot in PreferredHolderSlots)
        {
            if (!_inventory.TryGetSlotEntity(wearer, slot, out var clothing))
                continue;

            if (!TryComp<EmberAccessoryHolderComponent>(clothing, out var holder))
                continue;

            if (TryAttachNested((clothing.Value, holder), (accessory, accessoryComp)))
                return true;
        }

        var enumerator = _inventory.GetSlotEnumerator(wearer);
        while (enumerator.NextItem(out var item))
        {
            if (!TryComp<EmberAccessoryHolderComponent>(item, out var holder))
                continue;

            if (TryAttachNested((item, holder), (accessory, accessoryComp)))
                return true;
        }

        return false;
    }

    private void OnHolderInteractUsing(Entity<EmberAccessoryHolderComponent> holder, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<EmberAccessoryComponent>(args.Used, out var accessory))
            return;

        args.Handled = TryAttachRecursive(holder, (args.Used, accessory), args.User);
    }

    #endregion

    #region Detaching

    /// <summary>
    /// Takes an accessory off whatever it is attached to and puts it in the user's hands.
    /// </summary>
    /// <remarks>
    /// Bay equivalent: remove_accessory() plus on_removed(), which put the accessory in the user's
    /// hands or dropped it when there was no user.
    /// </remarks>
    public bool TryDetach(EntityUid accessory, EntityUid? user)
    {
        if (!TryComp<EmberAccessoryComponent>(accessory, out var accessoryComp))
            return false;

        if (!TryGetHolder(accessory, out var holder))
            return false;

        if (!CanDetach(holder, (accessory, accessoryComp), user, out var reason))
        {
            if (user != null && reason != null)
                _popup.PopupClient(reason, holder, user.Value);

            return false;
        }

        if (!TryGetContainer(holder.Owner, out var container) || !_container.Remove(accessory, container))
        {
            if (user != null)
            {
                _popup.PopupClient(
                    Loc.GetString("ember-accessory-detach-refused",
                        ("accessory", accessory),
                        ("clothing", holder.Owner)),
                    holder,
                    user.Value);
            }

            return false;
        }

        var ev = new EmberAccessoryDetachedEvent(holder, accessory, user);
        RaiseLocalEvent(accessory, ev);
        RaiseLocalEvent(holder, ev);

        _item.VisualsChanged(holder);

        if (user != null)
        {
            _hands.PickupOrDrop(user.Value, accessory);

            _popup.PopupClient(
                Loc.GetString("ember-accessory-detached",
                    ("accessory", accessory),
                    ("clothing", holder.Owner)),
                holder,
                user.Value);
        }

        return true;
    }

    /// <summary>
    /// Whether the user is allowed to pull this accessory off right now.
    /// </summary>
    public bool CanDetach(
        Entity<EmberAccessoryHolderComponent> holder,
        Entity<EmberAccessoryComponent> accessory,
        EntityUid? user,
        out string? reason)
    {
        reason = null;

        if ((accessory.Comp.Flags & EmberAccessoryFlags.Removable) == 0)
        {
            reason = Loc.GetString("ember-accessory-not-removable", ("accessory", accessory.Owner));
            return false;
        }

        if (user == null)
            return true;

        // ActionBlocker pops its own message for whatever is stopping them, so this one stays quiet.
        if (!_actionBlocker.CanInteract(user.Value, holder))
            return false;

        if (!_interaction.InRangeUnobstructed(user.Value, holder.Owner))
        {
            reason = Loc.GetString("ember-accessory-out-of-reach", ("clothing", holder.Owner));
            return false;
        }

        return true;
    }

    private void OnDetachRequest(EmberAccessoryDetachRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        if (!TryGetEntity(msg.Accessory, out var accessory))
            return;

        TryDetach(accessory.Value, user);
    }

    #endregion

    #region Examine

    private void OnHolderExamined(Entity<EmberAccessoryHolderComponent> holder, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!TryGetContainer(holder.Owner, out var container) || container.Count == 0)
            return;

        var visible = new List<EntityUid>();
        foreach (var accessory in container.ContainedEntities)
        {
            if (!TryComp<EmberAccessoryComponent>(accessory, out var comp))
                continue;

            if ((comp.Flags & EmberAccessoryFlags.Hidden) != 0)
                continue;

            visible.Add(accessory);
        }

        if (visible.Count == 0)
            return;

        using (args.PushGroup(nameof(EmberAccessoryHolderComponent)))
        {
            foreach (var accessory in visible)
            {
                args.PushMarkup(Loc.GetString("ember-accessory-examine",
                    ("accessory", Identity.Entity(accessory, EntityManager))));
            }
        }
    }

    #endregion

    #region Relay

    /// <summary>
    /// Accessories are inside a container on worn clothing, so the inventory relay stops at the
    /// clothing and never reaches them. This forwards the relayed event one level further down,
    /// which is what lets an armour plate or a scarf attached to a uniform still affect the wearer.
    /// </summary>
    /// <remarks>
    /// Bay did this by summing values on the holder (update_accessory_slowdown, armour aggregation).
    /// Re-raising the same <see cref="InventoryRelayedEvent{TEvent}"/> instance means existing
    /// components - ArmorComponent, ClothingSpeedModifierComponent, TemperatureProtectionComponent -
    /// work on an accessory with no extra code. Add a line here when an accessory needs to take part
    /// in an effect that is not listed yet.
    /// </remarks>
    private void InitializeRelay()
    {
        SubscribeLocalEvent<EmberAccessoryHolderComponent, InventoryRelayedEvent<DamageModifyEvent>>(RelayToAccessories);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent>>(RelayToAccessories);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, InventoryRelayedEvent<ModifyChangedTemperatureEvent>>(RelayToAccessories);

        // Verbs need the relay too, but they also feed the client's accessory menu, so they get
        // their own handlers rather than the plain relay.
        SubscribeLocalEvent<EmberAccessoryHolderComponent, GetVerbsEvent<EquipmentVerb>>(OnHolderGetVerbs);
        SubscribeLocalEvent<EmberAccessoryHolderComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(
            OnHolderGetRelayedVerbs);
    }

    private void OnHolderGetVerbs(Entity<EmberAccessoryHolderComponent> holder, ref GetVerbsEvent<EquipmentVerb> args)
    {
        RaiseLocalEvent(holder, new EmberAccessoryGetVerbsEvent(args));
    }

    private void OnHolderGetRelayedVerbs(
        EntityUid uid,
        EmberAccessoryHolderComponent component,
        InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        RelayToAccessories(uid, component, args);
        RaiseLocalEvent(uid, new EmberAccessoryGetVerbsEvent(args.Args));
    }

    private void RelayToAccessories<TEvent>(
        EntityUid uid,
        EmberAccessoryHolderComponent component,
        InventoryRelayedEvent<TEvent> args)
    {
        // Nearly all worn clothing carries nothing, and some of these events fire per damage
        // instance or per atmos tick, so the empty case gets out before touching the container.
        if (!TryGetContainer((uid, component), out var container) || container.Count == 0)
            return;

        // Relay cost is O(attached) per relayed event, which is what MaxAccessories bounds.
        // Nested holders re-raise in turn, so pouches on a rig on a uniform are all reached.
        foreach (var accessory in container.ContainedEntities)
        {
            RaiseLocalEvent(accessory, args);
        }
    }

    #endregion
}
