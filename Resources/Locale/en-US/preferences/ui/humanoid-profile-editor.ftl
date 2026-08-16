humanoid-profile-editor-randomize-everything-button = Randomize everything
humanoid-profile-editor-name-label = Name:
humanoid-profile-editor-name-random-button = Randomize
humanoid-profile-editor-identity-tab = Identity
humanoid-profile-editor-appearance-tab = Appearance
humanoid-profile-editor-background-tab = Background
humanoid-profile-editor-clothing = Preview job equipment:
humanoid-profile-editor-loadouts = Preview loadout items:
humanoid-profile-editor-clothing-show = Show
humanoid-profile-editor-sex-label = Sex:
humanoid-profile-editor-sex-male-text = Male
humanoid-profile-editor-sex-female-text = Female
humanoid-profile-editor-sex-unsexed-text = None
humanoid-profile-editor-age-label = Age:
humanoid-profile-editor-skin-color-label = Skin color:
humanoid-profile-editor-height-label = Height: {$height}cm
humanoid-profile-editor-width-label = Width: {$width}cm
humanoid-profile-editor-weight-label = Weight: {$weight}kg
humanoid-profile-editor-species-label = Species:
humanoid-profile-editor-pronouns-label = Pronouns:
humanoid-profile-editor-pronouns-male-text = He / Him
humanoid-profile-editor-pronouns-female-text = She / Her
humanoid-profile-editor-pronouns-epicene-text = They / Them
humanoid-profile-editor-pronouns-neuter-text = It / It
humanoid-profile-editor-import-button = Import Character
humanoid-profile-editor-export-button = Export Character
humanoid-profile-editor-export-image-button = Export image
humanoid-profile-editor-open-image-button = Open images
humanoid-profile-editor-save-button = Save Changes
humanoid-profile-editor-reset-button = Undo Changes
humanoid-profile-editor-spawn-priority-label = Spawn priority:
humanoid-profile-editor-eyes-label = Eye color:
humanoid-profile-editor-jobs-tab = Assignment
humanoid-profile-editor-preference-unavailable-stay-in-lobby-button = Stay in lobby if preference unavailable.
humanoid-profile-editor-preference-unavailable-spawn-as-overflow-button = Be {INDEFINITE($overflowJob)} {$overflowJob} if preference unavailable.
humanoid-profile-editor-preference-jumpsuit = Jumpsuit
humanoid-profile-editor-preference-jumpskirt = Jumpskirt
humanoid-profile-editor-preference-backpack = Backpack
humanoid-profile-editor-preference-satchel = Satchel
humanoid-profile-editor-preference-duffelbag = Duffelbag
humanoid-profile-editor-guidebook-button-tooltip = Click for more info

# Spawn priority
humanoid-profile-editor-preference-spawn-priority-none = None
humanoid-profile-editor-preference-spawn-priority-arrivals = Arrivals
humanoid-profile-editor-preference-spawn-priority-cryosleep = Cryosleep

humanoid-profile-editor-jobs-amount-in-department-tooltip = Jobs in the {$departmentName} department
humanoid-profile-editor-department-jobs-label = {$departmentName} jobs
humanoid-profile-editor-antags-tab = Antags
humanoid-profile-editor-antag-preference-yes-button = Yes
humanoid-profile-editor-antag-preference-no-button = No

humanoid-profile-editor-traits-tab = Traits
humanoid-profile-editor-traits-header = You have {$points ->
    [1] 1 point
    *[other] {$points} points
} and {$maxTraits ->
    [2147483648] {$traits ->
        [1] {$traits} trait
        *[other] {$traits} traits
    }
    *[other] {$traits}/{$maxTraits} traits
}
humanoid-profile-editor-traits-show-unusable-button = Show Unusable Traits
humanoid-profile-editor-traits-show-unusable-button-tooltip =
    When enabled, traits that your current character setup cannot use will be shown highlighted in red or orange if selected.
    You will still not be able to use the invalid traits unless your character setup changes to fit the requirements.
    This is most likely useful only if there's a bug hiding traits you actually can use or if you want to see other species' traits or something.
humanoid-profile-editor-traits-remove-unusable-button = Remove {$count} Unusable Traits
humanoid-profile-editor-traits-remove-unusable-button-tooltip =
    If you click this button, all traits that your current character setup cannot use will be removed.
    You will be asked for confirmation before the traits are removed.
humanoid-profile-editor-traits-no-traits = No traits found

humanoid-profile-editor-job-priority-high-button = High
humanoid-profile-editor-job-priority-medium-button = Medium
humanoid-profile-editor-job-priority-low-button = Low
humanoid-profile-editor-job-priority-never-button = Never

humanoid-profile-editor-naming-rules-warning = Warning: Offensive or LRP IC names and descriptions will lead to admin intervention on this server. Read our \[Rules\] for more.

humanoid-profile-editor-loadouts-tab = Loadout
humanoid-profile-editor-loadouts-points-label = You have {$points}/{$max} points
humanoid-profile-editor-loadouts-show-unusable-button = Show Unusable Loadouts
humanoid-profile-editor-loadouts-show-unusable-button-tooltip =
    When enabled, loadouts that your current character setup cannot use will be highlighted in red.
    Loadouts that your character cannot wear (if clothing) will be highlighted in yellow.
    You will still not be able to use the invalid loadouts unless your character setup changes to fit the requirements.
    This may be useful if you like switching between multiple jobs and don't want to have to reselect your loadout every time you switch.
humanoid-profile-editor-loadouts-remove-unusable-button = Remove {$count ->
    [1] {$count} Unusable Loadout
    *[other] {$count} Unusable Loadouts
}
humanoid-profile-editor-loadouts-remove-unusable-button-tooltip =
    If you click this button, all loadouts that your current character setup cannot use will be removed.
    You will be asked for confirmation before the loadouts are removed.
humanoid-profile-editor-loadouts-no-loadouts = No loadouts found.
humanoid-profile-editor-loadouts-customize = Customize
humanoid-profile-editor-loadouts-customize-name = Name
humanoid-profile-editor-loadouts-customize-description = Description
humanoid-profile-editor-loadouts-customize-color = Color tint
humanoid-profile-editor-loadouts-customize-save = Save
humanoid-profile-editor-loadouts-guidebook-button-tooltip = Click for more info
humanoid-profile-editor-loadouts-heirloom = Heirloom
humanoid-profile-editor-loadouts-heirloom-tooltip = Whichever loadouts you choose to be your potential heirloom will be randomly picked from on spawn.

humanoid-profile-editor-markings-tab = Markings
humanoid-profile-editor-flavortext-tab = Description

# Ember: the standing summary of the character, kept in view while the editor changes under it.
ember-dossier-heading = Personnel record
ember-dossier-empty = [color=#888888]No character in this slot.[/color]
ember-dossier-unset = not set
ember-dossier-name = Name
ember-dossier-species = Person
ember-dossier-species-line = {$species}, {$sex}, {$age}
ember-dossier-homeworld = Born
ember-dossier-culture = Raised
ember-dossier-faction = Citizenship
ember-dossier-religion = Belief
ember-dossier-branch = Service
ember-dossier-employer = Employer
ember-dossier-rank = Rank
ember-dossier-post = Post
ember-dossier-post-more = {$post} +{$count}
ember-dossier-ready = [color=#7A9A5B]Ready for the shift.[/color]
ember-dossier-not-ready = [color=#B4553F]Not ready:[/color] {$missing}
ember-dossier-missing-rank = no rank
ember-dossier-missing-post = no post

# Referenced from HumanoidProfileEditor.xaml and previously undefined in any locale, so these
# rendered as raw fluent ids in the editor.
humanoid-profile-editor-station-ai-name-label = Station AI name:
humanoid-profile-editor-cyborg-name-label = Cyborg name:

# Ember: service is one question - who put this character aboard - because a state posts people
# and a company hires them, and the two are alternatives rather than layers.
humanoid-profile-editor-service-tab = Service
humanoid-profile-editor-service-question = Who sent you here?
humanoid-profile-editor-service-note-posted = The state assigns its own people. There is no employer to name.
humanoid-profile-editor-service-note-hired = A civilian is paid rather than posted, so an employer is required.
humanoid-profile-editor-legacy-jobs-button = Show inherited posts
