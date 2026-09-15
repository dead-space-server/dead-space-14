-create-3rd-person =
    { $chance ->
        [1] Создаёт
        *[other] создают
    }

-cause-3rd-person =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    }

-satiate-3rd-person =
    { $chance ->
        [1] Насыщает
        *[other] насыщают
    }

entity-effect-guidebook-spawn-entity =
    { $chance ->
        [1] Создаёт
        *[other] создают
    } { $amount ->
        [1] {INDEFINITE($entname)}
        *[other] {$amount} {MAKEPLURAL($entname)}
    }

entity-effect-guidebook-destroy =
    { $chance ->
        [1] Уничтожает
        *[other] уничтожают
    } объект

entity-effect-guidebook-break =
    { $chance ->
        [1] Ломает
        *[other] ломают
    } объект

entity-effect-guidebook-explosion =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } взрыв

entity-effect-guidebook-emp =
    { $chance ->
        [1] Вызывает
        *[other] Вызывают
    } электромагнитный импульс

entity-effect-guidebook-flash =
    { $chance ->
        [1] Вызывает
        *[other] Вызывают
    } ослепляющую вспышку

entity-effect-guidebook-foam-area =
    { $chance ->
        [1] Создаёт
        *[other] создают
    } большое количество пены

entity-effect-guidebook-smoke-area =
    { $chance ->
        [1] Создаёт
        *[other] создают
    } густой дым

entity-effect-guidebook-satiate =
    { $chance ->
        [1] Насыщает
        *[other] насыщают
    } { $relative ->
        [1] потребность «{ $type }» со средней скоростью
        *[other] потребность «{ $type }» со скоростью { NATURALFIXED($relative, 3) } от средней
    }

entity-effect-guidebook-health-change =
    { $chance ->
        [1] { $healsordeals ->
                [heals] Лечит
                [deals] Наносит
                *[both] лечит
             }
        *[other] { $healsordeals ->
                [heals] Лечат
                [deals] Наносят
                *[both] лечит
             }
    } { $changes }

entity-effect-guidebook-even-health-change =
    { $chance ->
        [1] { $healsordeals ->
            [heals] Равномерно лечит
            [deals] Равномерно наносит
            *[both] равномерно лечит
        }
        *[other] { $healsordeals ->
            [heals] Равномерно лечат
            [deals] Равномерно наносят
            *[both] равномерно лечит
        }
    } { $changes }

entity-effect-guidebook-status-effect-old =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                     *[other] вызывают
                 } {LOC($key)} как минимум на {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд 
                      } эффект не накапливается
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } {LOC($key)} как минимум на {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд 
                      } эффект накапливается
        [set]  { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } {LOC($key)} на {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд 
                      } эффект не накапливается
        *[remove]{ $chance ->
                    [1] Убирает
                    *[other] Убирают
                } {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд 
                      } {LOC($key)}
    }

entity-effect-guidebook-status-effect =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызывают
                 } {LOC($key)} минимум на {NATURALFIXED($time, 3)} { $time ->
                [one] секунду
                [few] секунды
               *[other] секунд
            }, эффект не накапливается
        [add]
            { $chance ->
                [1] Вызывает
               *[other] вызывают
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { $time ->
                [one] секунду
                [few] секунды
               *[other] секунд
            }, эффект накапливается
        [set]
            { $chance ->
                [1] Вызывает
               *[other] вызывают
            } { LOC($key) } минимум на { NATURALFIXED($time, 3) } { $time ->
                [one] секунду
                [few] секунды
               *[other] секунд
            }, эффект не накапливается
        *[remove]
            { $chance ->
                [1] Удаляет
               *[other] удаляют
            } { NATURALFIXED($time, 3) } { $time ->
                [one] секунду
                [few] секунды
               *[other] секунд
            } от { LOC($key) }
    } { $delay ->
        [0] немедленно
        *[other] после { NATURALFIXED($delay, 3) } { $delay ->
            [one] секунду
            [few] секунды
            *[other] секунд
        } задержки
    }

entity-effect-guidebook-status-effect-indef =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызывает
                 } постоянный {LOC($key)}
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } постоянный{LOC($key)}
        [set]  { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } постоянный{LOC($key)}
        *[remove]{ $chance ->
                    [1] Убирает
                    *[other] убирают
                } {LOC($key)}
    } { $delay ->
        [0] мгновенно
        *[other] после { NATURALFIXED($delay, 3) } { $delay ->
            [one] секунду
            [few] секунды
            *[other] секунд
        } задержки
    }

entity-effect-guidebook-knockdown =
    { $type ->
        [update]{ $chance ->
                    [1] Вызывает
                    *[other] вызывают
                    } {LOC($key)} минимум на { NATURALFIXED($time, 3) } { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд
            }, эффект не накапливается
        [add]   { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } падение как минимум на {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд
            }, эффект накапливается
        *[set]  { $chance ->
                    [1] Вызывает
                    *[other] вызывают
                } падение как минимум на {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд
            }, эффект не накапливается
        [remove]{ $chance ->
                    [1] Убирает
                    *[other] убирают
                } {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд
            }, оглушения
    }

entity-effect-guidebook-set-solution-temperature-effect =
    { $chance ->
        [1] Устанавливает
        *[other] устанавливают
    } температуру раствора в {NATURALFIXED($temperature, 2)}k

entity-effect-guidebook-adjust-solution-temperature-effect =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Убирает
            }
        *[other]
            { $deltasign ->
                [1] добавляют
                *[-1] убирают
            }
    } температуру к раствору пока она не достигнет { $deltasign ->
                [1] максимум {NATURALFIXED($maxtemp, 2)}k
                *[-1] минимум {NATURALFIXED($mintemp, 2)}k
            }

entity-effect-guidebook-adjust-reagent-reagent =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Убирает
            }
        *[other]
            { $deltasign ->
                [1] Добавляет
                *[-1] убирают
            }
    } {NATURALFIXED($amount, 2)}ед. {$reagent}'а { $deltasign ->
        [1] к
        *[-1] из
    } раствора

entity-effect-guidebook-adjust-reagent-group =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Убирает
            }
        *[other]
            { $deltasign ->
                [1] добавляют
                *[-1] убирают
            }
    } {NATURALFIXED($amount, 2)}ед. реагентов из группы {$group} { $deltasign ->
            [1] в
            *[-1] из
        } раствора

entity-effect-guidebook-adjust-temperature =
    { $chance ->
        [1] { $deltasign ->
                [1] Добавляет
                *[-1] Убирает
            }
        *[other]
            { $deltasign ->
                [1] добавляют
                *[-1] убирают
            }
    } {POWERJOULES($amount)} температуры { $deltasign ->
            [1] в
            *[-1] из
        } огранизм в котором метаболизируется

entity-effect-guidebook-chem-cause-disease =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } болезнь { $disease }

entity-effect-guidebook-chem-cause-random-disease =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } болезни { $diseases }

entity-effect-guidebook-jittering =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } тряску

entity-effect-guidebook-clean-bloodstream =
    { $chance ->
        [1] Очищает
        *[other] очищают
    } кровь от других реагентов

entity-effect-guidebook-cure-disease =
    { $chance ->
        [1] Лечит
        *[other] лечат
    } болезни

entity-effect-guidebook-eye-damage =
    { $chance ->
        [1] { $deltasign ->
                [1] Наносит
                *[-1] Лечит
            }
        *[other]
            { $deltasign ->
                [1] наносят
                *[-1] лечат
            }
    } слепоту

entity-effect-guidebook-vomit =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } рвоту

entity-effect-guidebook-create-gas =
    { $chance ->
        [1] Создаёт
        *[other] создают
    } { $moles } { $moles ->
        [1] моль
        *[other] моль
    } { $gas }'а

entity-effect-guidebook-drunk =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } опьянение

entity-effect-guidebook-electrocute =
    { $chance ->
        [1] поражает током
        *[other] поражают током
    } употребившего на {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд 
                      }

entity-effect-guidebook-emote =
    { $chance ->
        [1] Заставляет
        *[other] заставляют
    } употребившего [bold][color=white]{$emote}[/color][/bold]

entity-effect-guidebook-extinguish-reaction =
    { $chance ->
        [1] Тушит
        *[other] тушат
    } огонь

entity-effect-guidebook-flammable-reaction =
    { $chance ->
        [1] Увеличивает
        *[other] увеличивают
    } воспламеняемость

entity-effect-guidebook-ignite =
    { $chance ->
        [1] Поджигает
        *[other] поджигают
    } употребившего

entity-effect-guidebook-make-sentient =
    { $chance ->
        [1] Делает
        *[other] делают
    } употребившего тихим

entity-effect-guidebook-make-polymorph =
    { $chance ->
        [1] Превращает
        *[other] превращают
    } употребившего в { $entityname }

entity-effect-guidebook-modify-bleed-amount =
    { $chance ->
        [1] { $deltasign ->
                [1] Повышает
                *[-1] Снижает
            }
        *[other] { $deltasign ->
                    [1] повышают
                    *[-1] снижают
                 }
    } кровотечение

entity-effect-guidebook-modify-blood-level =
    { $chance ->
        [1] { $deltasign ->
                [1] Увеличивает
                *[-1] Уменьшает
            }
        *[other] { $deltasign ->
                    [1] увеличивают
                    *[-1] уменьшают
                 }
    } уровень крови

entity-effect-guidebook-paralyze =
    { $chance ->
        [1] Парализует
        *[other] парализуют
    } употребившего как минимум на {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд 
                      }

entity-effect-guidebook-movespeed-modifier =
    { $chance ->
        [1] Модифицирует
        *[other] модифицируют
    } скорость передвижения на {NATURALFIXED($sprintspeed, 3)}x как минимум на {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд 
                      }

entity-effect-guidebook-reset-narcolepsy =
    { $chance ->
        [1] Временно сбрасывает
        *[other] временно сбрасывают
    } приступ нарколепсии

entity-effect-guidebook-wash-cream-pie-reaction =
    { $chance ->
        [1] Смывает
        *[other] смывают
    } пирог с лиц

entity-effect-guidebook-cure-zombie-infection =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } исцеление от зомби инфекции

entity-effect-guidebook-cause-zombie-infection =
    { $chance ->
        [1] Даёт
        *[other] дают
    } зомби инфекцию

entity-effect-guidebook-innoculate-zombie-infection =
    { $chance ->
        [1] Лечит
       *[other] лечат
    } зомби-вирус и обеспечивает иммунитет к нему в будущем

entity-effect-guidebook-reduce-rotting =
    { $chance ->
        [1] Восстанавливает
        *[other] восстанавливают
    } {NATURALFIXED($time, 3)} { $time ->
                       [one] секунду
                       [few] секунды
                      *[other] секунд 
                      } гниения

entity-effect-guidebook-area-reaction =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } дым или пенну реакцию на {NATURALFIXED($duration, 3)} {MANY("second", $duration)}

entity-effect-guidebook-add-to-solution-reaction =
    { $chance ->
        [1] Вызывает
        *[other] вызывают
    } {$reagent} который будет добавлен в жидкостное хранилище

entity-effect-guidebook-artifact-unlock =
    { $chance ->
        [1] Помогает
       *[other] помогают
    } разблокировать инопланетный артефакт.

entity-effect-guidebook-artifact-durability-restore =
    Восстанавливает {$restored} прочности активным узлам артефакта.

entity-effect-guidebook-plant-attribute =
    { $chance ->
        [1] Изменяет
       *[other] изменяют
    } { $attribute } на { $positive ->
        [true] [color=red]{ $amount }[/color]
       *[false] [color=green]{ $amount }[/color]
    }

entity-effect-guidebook-plant-cryoxadone =
    { $chance ->
        [1] Омолаживает
       *[other] омолаживают
    } растение, в зависимости от возраста растения и времени его роста

entity-effect-guidebook-plant-phalanximine =
    { $chance ->
        [1] Восстанавливает
       *[other] восстанавливают
    } жизнеспособность растения, ставшего нежизнеспособным в результате мутации

entity-effect-guidebook-plant-diethylamine =
    { $chance ->
        [1] Повышает
       *[other] повышают
    } продолжительность жизни растения и/или его базовое здоровье с шансом 10% на единицу

entity-effect-guidebook-plant-robust-harvest =
    { $chance ->
        [1] Повышает
       *[other] повышают
    } потенцию растения путём { $increase } до максимума в { $limit }. Приводит к тому, что растение теряет свои семена, когда потенция достигает { $seedlesstreshold }. Попытка повысить потенцию свыше { $limit } может вызвать снижение урожайности с вероятностью 10%

entity-effect-guidebook-plant-seeds-add =
    { $chance ->
        [1] Восстанавливает
       *[other] восстанавливают
    } семена растения

entity-effect-guidebook-plant-seeds-remove =
    { $chance ->
        [1] Убирает
       *[other] убирают
    } семена из растения

entity-effect-guidebook-plant-mutate-exude-gasses =
    { $chance ->
        [1] Заставляет мутировать
       *[other] заставляют мутировать
    } растение так, чтобы оно выделяло от { $minValue } до { $maxValue } молей газов

entity-effect-guidebook-plant-mutate-consume-gasses =
    { $chance ->
        [1] Заставляет мутировать
       *[other] заставляют мутировать
    } растение так, чтобы оно поглощало от { $minValue } до { $maxValue } молей газов

entity-effect-guidebook-plant-mutate-chemicals =
    { $chance ->
        [1] Мутирует
       *[other] мутируют
    } растение, чтобы то производило { $name }

entity-effect-guidebook-add-reagent-to-bloodstream =
    { $chance ->
        [1] Вводит
        *[other] вводят
    } {$quantity} ед. реагента «{$reagent}» прямо в кровь

entity-effect-disarm =
    { $chance ->
        [1] Обезоруживает
        *[other] обезоруживают
    } цель
