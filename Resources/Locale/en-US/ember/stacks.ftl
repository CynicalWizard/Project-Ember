# Stack names shown by the construction guide when a recipe asks for rods.

ember-stack-rod-steel = steel rod
ember-stack-rod-iron = iron rod
ember-stack-rod-aluminium = aluminium rod
ember-stack-rod-plasteel = plasteel rod
ember-stack-rod-titanium = titanium rod
ember-stack-rod-osmiumcarbideplasteel = osmium-carbide plasteel rod
ember-stack-rod-osmium = osmium rod
ember-stack-rod-gold = gold rod
ember-stack-rod-silver = silver rod
ember-stack-rod-electrum = electrum rod
ember-stack-rod-copper = copper rod
ember-stack-rod-bronze = bronze rod
ember-stack-rod-platinum = platinum rod
ember-stack-rod-uranium = uranium rod

# Rod names follow how many are in the pile, so one of them is not called "rods".

ember-rod-steel = { $count ->
        [one] steel rod
       *[other] steel rods
    }
ember-rod-iron = { $count ->
        [one] iron rod
       *[other] iron rods
    }
ember-rod-aluminium = { $count ->
        [one] aluminium rod
       *[other] aluminium rods
    }
ember-rod-plasteel = { $count ->
        [one] plasteel rod
       *[other] plasteel rods
    }
ember-rod-titanium = { $count ->
        [one] titanium rod
       *[other] titanium rods
    }
ember-rod-osmiumcarbideplasteel = { $count ->
        [one] osmium-carbide plasteel rod
       *[other] osmium-carbide plasteel rods
    }
ember-rod-osmium = { $count ->
        [one] osmium rod
       *[other] osmium rods
    }
ember-rod-gold = { $count ->
        [one] gold rod
       *[other] gold rods
    }
ember-rod-silver = { $count ->
        [one] silver rod
       *[other] silver rods
    }
ember-rod-electrum = { $count ->
        [one] electrum rod
       *[other] electrum rods
    }
ember-rod-copper = { $count ->
        [one] copper rod
       *[other] copper rods
    }
ember-rod-bronze = { $count ->
        [one] bronze rod
       *[other] bronze rods
    }
ember-rod-platinum = { $count ->
        [one] platinum rod
       *[other] platinum rods
    }
ember-rod-uranium = { $count ->
        [one] uranium rod
       *[other] uranium rods
    }

ember-rod-desc = { $count ->
        [one] A rod. Can be used for building, or something.
       *[other] Some rods. Can be used for building, or something.
    }
