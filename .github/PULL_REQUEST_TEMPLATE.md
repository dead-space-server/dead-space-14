<!-- ЭТО ШАБЛОН ВАШЕГО PULL REQUEST. ТЕКСТ МЕЖДУ СТРЕЛКАМИ - ЭТО КОММЕНТАРИИ, ОНИ НЕ БУДУТ ВИДНЫ В ОПИСАНИИ PR. -->

## Ссылка на задачу
<!--Укажите ссылки на соответствующие обсуждения, предложение, задачу. -->

## Медиа
<!-- Прикрепите медиафайлы, если PR вносит изменения в игру (одежда, предметы, механики и т.д.).
Небольшие исправления/рефакторинг освобождаются от этого требования. -->

## Требования
<!-- Подтвердите следующее, поставив X в скобках [X]: -->
- [ ] PR полностью завершён и мне не нужна помощь чтобы его закончить.
- [ ] Я внимательно просмотрел все свои изменения и багов в них не нашёл.
- [ ] Я запускал локальный сервер со своими изменениями и всё протестировал.
- [ ] Я добавил скриншот/видео демонстрации PR в игре, **или** этот PR этого не требует.

<<<<<<< ours
## Критические изменения
<!-- Перечислите все критические изменения, включая изменения пространств имен, публичных классов/методов/полей, переименования прототипов; и предоставьте инструкции по их исправлению. -->
||||||| base
## Media
<!-- Attach media if the PR makes in-game changes (clothing, items, features, etc).
Small fixes/refactors are exempt. Media may be used in SS14 progress reports with credit. -->
=======
## Test plan
<!--
Describe how you tested the pull request, and how someone reviewing this PR can test it themselves.
-->

## Media
<!-- Attach media if the PR makes in-game changes (clothing, items, features, etc).
Small fixes/refactors are exempt. Media may be used in SS14 progress reports with credit. -->
>>>>>>> theirs

<<<<<<< ours
**Список изменений**
<!-- Добавьте запись в Changelog, чтобы игроки знали о новых функциях или изменениях, которые могут повлиять на игровой процесс.
В журнал изменений следует помещать только то, что действительно важно игрокам. Если вы добавили музыку для ивента - это не важно. 
Если вы понёрфили пули христова - это важно. Убедитесь, что вы вынесли этот шаблон Changelog из блока комментариев, чтобы он отображался.
Changelog должен иметь символ :cl:, чтобы бот распознал изменения и добавил их в список изменений игры. -->
||||||| base
## Requirements
<!-- Confirm the following by placing an X in the brackets without spaces inside (for example: [X] ): -->
- [ ] I have read and am following the [Pull Request and Changelog Guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).
- [ ] I have added media to this PR or it does not require an in-game showcase.
<!-- You should understand that not following the above may get your PR closed at maintainer’s discretion -->

## Breaking changes
<!-- List any breaking changes, including namespaces, public class/method/field changes, prototype renames; and provide instructions for fixing them.
This will be posted in #codebase-changes. -->

**Changelog**
<!-- Add a Changelog entry to make players aware of new features or changes that could affect gameplay.
Make sure to read the guidelines and take this Changelog template out of the comment block in order for it to show up.
Changelog must have a :cl: symbol, so the bot recognizes the changes and adds them to the game's changelog. -->
=======
## Requirements
<!-- Confirm the following by placing an X in the brackets without spaces inside (for example: [X] ): -->
- [ ] I have read and am following the [Pull Request and Changelog Guidelines](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html).
- [ ] I have tested this pull request and written instructions on how to test it.
- [ ] I have added media to this PR or it does not require an in-game showcase.
<!-- You should understand that not following the above may get your PR closed at maintainer’s discretion -->

## Breaking changes
<!-- List any breaking changes, including namespaces, public class/method/field changes, prototype renames; and provide instructions for fixing them.
This will be posted in #codebase-changes. -->

## Changelog
<!-- Add a Changelog entry to make players aware of new features or changes that could affect gameplay.
Make sure to read the guidelines and take this Changelog template out of the comment block in order for it to show up.
Changelog must have a :cl: symbol, so the bot recognizes the changes and adds them to the game's changelog.
Maps, admin and rule changes should include a category header below the :cl: as per the guidelines.-->
>>>>>>> theirs
<!--
:cl:
<<<<<<< ours
- Добавлено: 
- Удалено: 
- Изменено: 
- Исправлено: 
||||||| base
- add: Added fun!
- remove: Removed fun!
- tweak: Changed fun!
- fix: Fixed fun!
=======
- add: Crowbars now randomly spawn in maintenance lockers.
- remove: Crowbars no longer spawn in maintenance crates.
- tweak: Crowbar spawn rates have been increased for tool lockers.
- fix: Crowbars no longer accidentally spawn in microwaves.
>>>>>>> theirs
-->
