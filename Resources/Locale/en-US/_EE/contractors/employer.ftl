# Ember: employer names and descriptions moved to ember/employers.ftl along with the
# prototypes themselves. Only the editor label and the requirement message live here.

humanoid-profile-editor-employer-label = Employer

character-employer-requirement = You must{$inverted ->
    [true]{" "}not
    *[other]{""}
} be employed by one of these: {$employers}
