# Аксессуары, крепящиеся к одежде. Порт системы с SierraBay12.
#
# Склонения в движке нет: {$accessory} и {$clothing} подставляются в именительном падеже,
# а род предмета заранее неизвестен (шарф, повязка, медаль). Поэтому название ставится только
# после двоеточия — в этой позиции именительный падеж корректен при любом роде, — а во всех
# остальных фразах вместо названия стоит указательное местоимение. По той же причине здесь нет
# причастий и прилагательных, согласуемых с предметом.

# Успешные действия. Игрок только что сам ткнул в нужную вещь, так что {$clothing} не повторяем.
ember-accessory-attached = Вы прикрепляете аксессуар: {$accessory}.
ember-accessory-detached = Вы снимаете аксессуар: {$accessory}.

# Отказы при прикреплении
ember-accessory-no-attachments = К этой одежде нельзя ничего прикрепить.
ember-accessory-wrong-slot = Этот аксессуар к такой одежде не крепится.
ember-accessory-slot-occupied =
    { $limit ->
        [one] Такой аксессуар тут уже есть.
       *[other] Больше {$limit} таких сюда не влезет.
    }
ember-accessory-too-many = На этой одежде больше нет места.
ember-accessory-already-attached = Этот аксессуар уже прикреплён сюда.
ember-accessory-self-attach = Нельзя прикрепить вещь саму к себе.
ember-accessory-attach-refused = Аксессуар не держится на этой одежде.

# Отказы при снятии
ember-accessory-not-removable = Этот аксессуар не снимается.
ember-accessory-detach-refused = Не получается снять этот аксессуар.
ember-accessory-out-of-reach = Вы не дотягиваетесь до этой одежды.

# Осмотр
ember-accessory-examine = [color=lightgray]Прикреплено: {$accessory}.[/color]
ember-accessory-examine-wearer = [color=lightgray]Знаки различия: {$accessory}.[/color]

# Вербы и меню
ember-accessory-remove-verb = Снять аксессуар
ember-accessory-radial-tooltip = Снять: {$accessory}
