# DS14-start
# ВорПРО — the thief's PDA program

thief-program-name = ВорПРО
thief-program-unlocked = Программа «ВорПРО» установлена на ваш КПК. Проверьте список программ!

## Header
thief-program-balance = Баланс: [color=#8fa063]{$balance}[/color] dCR
thief-program-goal = Цель раунда: [color=#c9a227]{$earned} / {$target}[/color] dCR при себе ({$percent}%)

## Tabs
thief-program-tab-requests = Запросы
thief-program-tab-uplink = Аплинк

## Requests tab
thief-program-section-active = Принятые запросы
thief-program-section-offers = Доступные запросы
thief-program-hint-no-beacon = [color=#c9a227]Установите и разверните воровской маяк, чтобы продавать товары.[/color]
thief-program-request-offer =
    {$name} ×{$count} — [color=#8fa063]{$price} dCR[/color] • срок: {$minutes} мин.
thief-program-request-active =
    {$name} ×{$count} — [color=#8fa063]{$price} dCR[/color] • осталось: {$minutes}:{$seconds} [color=#ff4d4d]Просрочено![/color] Цена снижена на 15%.
    Отнесите товар к воровскому маяку и нажмите «Продать».
thief-program-request-expired =
    {$name} ×{$count} — [color=#a86f32]~{$price} dCR[/color] • [color=#ff4d4d]просрочен[/color]
    Отнесите товар к воровскому маяку и нажмите «Продать».
thief-program-accept = Взять
thief-program-sell = Продать
thief-program-decline-tooltip = Отказаться от запроса

## Uplink tab
thief-program-uplink-cost = {$cost} dCR
thief-program-buy = Купить
thief-program-exchange-placeholder = Сумма для отмывания...
thief-program-exchange-button = Отмыть 1:1
thief-program-search-placeholder = Поиск по аплинку...

## Categories
thief-program-category-tools = Инструменты
thief-program-category-gear = Снаряжение
thief-program-category-implants = Импланты
thief-program-category-misc = Разное
thief-program-category-sets = Наборы

## Server popups
thief-program-requests-limit = Слишком много активных запросов. Сначала выполните или отклоните текущие.
thief-program-error-no-mind = Программа не может определить владельца.
thief-program-error-no-beacon = Не найден привязанный воровской маяк!
thief-program-error-too-far = Вы слишком далеко от своего маяка.
thief-program-error-not-enough = Рядом с маяком недостаточно подходящих товаров.
thief-program-uplink-error = Такой товар отсутствует в каталоге.
thief-program-uplink-no-money = Недостаточно грязных кредитов.
thief-program-exchange-invalid = Укажите положительную сумму.
thief-program-exchange-not-enough = Недостаточно грязных кредитов для обмена.
thief-program-sold-in-time = Сделка завершена вовремя! Получено: {$amount} dCR (бонус +15%).
thief-program-sold-late = Сделка завершена с опозданием. Получено: {$amount} dCR (−15%).
thief-program-exchanged = Обмен выполнен: {$amount} обычных кредитов зачислено.
thief-program-carry-hint = Важно: в цель раунда идут только dCR, которые вы несёте с собой!

## Uplink listings
# Listing names reuse the already-existing item locales (ent-*) referenced from thief_program_listings.yml.
# DS14-end
