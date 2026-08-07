# Названия стаков, которые показывает руководство по постройке в шагах рецепта.

ember-stack-rod-steel = стальные прутья
ember-stack-rod-iron = железные прутья
ember-stack-rod-aluminium = алюминиевые прутья
ember-stack-rod-plasteel = пласталевые прутья
ember-stack-rod-titanium = титановые прутья
ember-stack-rod-osmiumcarbideplasteel = осмиево-карбидные прутья
ember-stack-rod-osmium = осмиевые прутья
ember-stack-rod-gold = золотые прутья
ember-stack-rod-silver = серебряные прутья
ember-stack-rod-electrum = электрумовые прутья
ember-stack-rod-copper = медные прутья
ember-stack-rod-bronze = бронзовые прутья
ember-stack-rod-platinum = платиновые прутья
ember-stack-rod-uranium = урановые прутья

# Название прутьев следует их количеству, чтобы один прут не назывался прутьями.

ember-rod-steel = { $count ->
        [one] стальной прут
       *[other] стальные прутья
    }
ember-rod-iron = { $count ->
        [one] железный прут
       *[other] железные прутья
    }
ember-rod-aluminium = { $count ->
        [one] алюминиевый прут
       *[other] алюминиевые прутья
    }
ember-rod-plasteel = { $count ->
        [one] пласталевый прут
       *[other] пласталевые прутья
    }
ember-rod-titanium = { $count ->
        [one] титановый прут
       *[other] титановые прутья
    }
ember-rod-osmiumcarbideplasteel = { $count ->
        [one] осмиево-карбидный прут
       *[other] осмиево-карбидные прутья
    }
ember-rod-osmium = { $count ->
        [one] осмиевый прут
       *[other] осмиевые прутья
    }
ember-rod-gold = { $count ->
        [one] золотой прут
       *[other] золотые прутья
    }
ember-rod-silver = { $count ->
        [one] серебряный прут
       *[other] серебряные прутья
    }
ember-rod-electrum = { $count ->
        [one] электрумовый прут
       *[other] электрумовые прутья
    }
ember-rod-copper = { $count ->
        [one] медный прут
       *[other] медные прутья
    }
ember-rod-bronze = { $count ->
        [one] бронзовый прут
       *[other] бронзовые прутья
    }
ember-rod-platinum = { $count ->
        [one] платиновый прут
       *[other] платиновые прутья
    }
ember-rod-uranium = { $count ->
        [one] урановый прут
       *[other] урановые прутья
    }

ember-rod-desc = { $count ->
        [one] Прут. Годится для стройки и не только.
       *[other] Несколько прутьев. Годятся для стройки и не только.
    }
