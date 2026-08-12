# Accessories attached to clothing, ported from SierraBay12.

# Successful actions
ember-accessory-attached = You attach the {$accessory} to the {$clothing}.
ember-accessory-detached = You remove the {$accessory} from the {$clothing}.

# Refusals while attaching
ember-accessory-no-attachments = The {$clothing} can't take any attachments.
ember-accessory-wrong-slot = The {$accessory} can't attach to the {$clothing}.
ember-accessory-slot-occupied =
    { $limit ->
        [one] The {$clothing} already has one of those attached.
       *[other] The {$clothing} won't hold more than {$limit} of those.
    }
ember-accessory-too-many = There's no room left on the {$clothing}.
ember-accessory-already-attached = The {$accessory} is already attached to the {$clothing}.
ember-accessory-self-attach = You can't attach the {$clothing} to itself.
ember-accessory-attach-refused = The {$accessory} won't go on the {$clothing}.

# Refusals while removing
ember-accessory-not-removable = The {$accessory} won't come off.
ember-accessory-detach-refused = You can't get the {$accessory} off the {$clothing}.
ember-accessory-out-of-reach = You can't reach the {$clothing}.

# Examine
ember-accessory-examine = [color=lightgray]{CAPITALIZE($accessory)} is attached to it.[/color]

# Verbs and menus
ember-accessory-remove-verb = Remove accessory
ember-accessory-radial-tooltip = Remove {$accessory}
