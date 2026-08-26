# Ember: employer names and descriptions moved to ember/employers.ftl along with the
# prototypes themselves. Only the editor label and the requirement message live here.

humanoid-profile-editor-employer-label = Трудоустройство

character-employer-requirement = Вы{$inverted ->
    [true]{" "}не можете
    *[other]{" "}должны
} быть работником одной из данных компаний: {$employers}
