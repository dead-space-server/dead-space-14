status-effect-examine-adrenaline = [color=red]{ CAPITALIZE(POSS-ADJ($target)) } тело напряжено, а взгляд насторожен.[/color]
status-effect-examine-drunk = [color=brown]{ CAPITALIZE(SUBJECT($target)) } выглядит { GENDER($target) ->
        [male] пьяным
        [female] пьяной
        [epicene] пьяными
       *[neuter] пьяным
    }...[/color]
status-effect-examine-seeing-rainbow = [color=lightgreen]{ CAPITALIZE(SUBJECT($target)) } смотрит на то, чего на самом деле нет.[/color]
status-effect-examine-stunned = [color=yellow]{ CAPITALIZE(POSS-ADJ($target)) } тело выглядит изнурённым и неспособным двигаться.[/color]
status-effect-examine-temporary-blindness = [color=lightblue]{ CAPITALIZE(POSS-ADJ($target)) } глаза расфокусированы. Похоже, { SUBJECT($target) } плохо видит.[/color]
