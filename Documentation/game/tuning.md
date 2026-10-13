# Как настраивать игру

Эта страница — для геймдизайнеров и всех, кто хочет поменять числа в игре без программирования.

## Где живут числа

Все настраиваемые числа игры — скорость, урон, перезарядка, поведение ботов, правила режимов, цены улучшений — лежат
не в коде, а в отдельных **файлах настроек** (в Unity они называются *ScriptableObject*-ассетами). Их открывают в Unity
и правят в окне **Inspector** (инспектор), как обычную форму с полями. Все они — в `Assets/_Project/ScriptableObjects/`.

| Файл настроек | Папка | Что настраивает |
| --- | --- | --- |
| `PlayerTankConfig` | `Tank/` | Танк игрока: скорость, разгон, разворот, здоровье; прибавки за улучшения корпуса и двигателя |
| `BotTankConfig` | `Tank/` | То же для ботов (сейчас медленнее игрока: 2,6 против 2,8); прибавки за ранг угрозы |
| `DummyTankConfig` | `Tank/` | Неподвижные мишени на тестовой арене (500 здоровья) |
| `NitroConfig` | `Tank/` | Нитро и таран — общий для всех танков; прибавка за улучшение нитро |
| `HealthBarStyle` | `Tank/` | Полоска здоровья под танками: размер, цвета, показывать ли у целых танков |
| `TankSkin_Blue` и др. | `Tank/` | Рисунки танка одного цвета (менять, только если пришёл новый арт) |
| `ArmorPiercing` | `Weapons/` | Бронебойный снаряд игрока: скорость, урон, перезарядка, размер; прибавки за улучшения |
| `Fragment` | `Weapons/` | Осколочный снаряд игрока, плюс радиус и урон взрыва |
| `ExplosiveShell` | `Weapons/` | Снаряд усиления «Взрывные снаряды» |
| `BotWeapon` | `Weapons/` | Снаряд ботов |
| `RapidFire`, `ExplosiveShells`, `Laser`, `Shield` | `PowerUps/` | Усиления: длительность, цвет пикапа и числа каждого |
| `DefaultBotProfile` | `Bots/` | Характер «уравновешенный»: реакция, меткость, дистанции, таран, отступление, увороты |
| `BotProfile_Aggressor` | `Bots/` | Характер «агрессор» |
| `BotProfile_Cautious` | `Bots/` | Характер «осторожный» |
| `TeamElimination`, `CapturePoint`, `Deathmatch` | `Match/` | Режимы: название и описание в меню, раунды, лимиты очков и времени, возрождение, доп. цель |
| `ProgressionConfig` | `Resources/` | Шестерёнки за убийство и победу, цены и описания улучшений |
| `SfxLibrary` | `Resources/` | Какой звуковой файл играет на каждое событие, громкость, музыка |
| `UiTheme` | `Resources/` | Картинки интерфейса (рамки, иконки, фон, логотип); пока пустые |

Полный список всех полей с описанием — на странице [Параметры в инспекторе](../articles/inspector.md).
Описания там на английском (они же видны всплывающей подсказкой в Unity, если навести мышь на поле).

## Как поменять число

1. В окне **Project** найдите файл настроек (пути — в таблице выше).
2. Кликните по нему — справа в окне **Inspector** появятся его поля.
3. Поменяйте число. Наведите мышь на название поля — появится подсказка, что оно значит.
4. Нажмите **Play** и проверьте, как ощущается.

> [!TIP]
> Числа в файлах настроек можно менять **прямо во время игры (в Play Mode)** — эффект виден сразу, и изменения
> **сохраняются** после остановки. Это удобно для подбора, но не забывайте вернуть значение, если эксперимент не удался.
> А вот изменения объектов на сцене во время Play Mode после остановки пропадают.

> [!WARNING]
> Файлы настроек общие для всей команды. Если поменяли число и хотите его оставить — скажите тому, кто отвечает за
> эту часть игры, и внесите итоговое значение в документ баланса `tank_balance_v0_2.md`.

> [!CAUTION]
> Пункт меню **TankBrawl → Setup Data Assets** возвращает к стартовым значениям числа режимов, награды, цены улучшений,
> прибавки «за уровень» и звуки выстрелов. Если вы их настраивали, не запускайте его.

## «Хочу, чтобы…» — что крутить

### Танк, снаряды, нитро

| Хочу, чтобы… | Файл | Поле |
| --- | --- | --- |
| танк ездил быстрее | `PlayerTankConfig` | `moveSpeed` |
| танк резче разгонялся / тормозил | `PlayerTankConfig` | `acceleration` / `deceleration` |
| танк быстрее разворачивался | `PlayerTankConfig` | `rotationSpeed` (градусов в секунду) |
| лёгкое касание стика не трогало танк с места | `PlayerTankConfig` | `driveThreshold` (больше — дольше только поворот) |
| танк был живучее | `PlayerTankConfig` | `maxHealth` |
| неуязвимость после появления длилась дольше | `TeamElimination` / `CapturePoint` / `Deathmatch` | `spawnProtection` |
| игрок стрелял чаще | `ArmorPiercing`, `Fragment` | `fireInterval` (меньше — чаще) |
| снаряды летели быстрее / медленнее | `ArmorPiercing`, `Fragment`, `BotWeapon` | `speed` |
| снаряды били сильнее | `ArmorPiercing`, `Fragment`, `BotWeapon` | `damage` |
| осколочный взрывался шире / сильнее | `Fragment` | `explosionRadius` / `explosionDamage` |
| снаряд легче попадал по танкам | `ArmorPiercing`, `Fragment`, `BotWeapon` | `tankHitRadius` |
| снаряд не цеплялся за углы | `ArmorPiercing`, `Fragment`, `BotWeapon` | `wallHitRadius` (меньше — реже цепляет) |
| снаряд отскакивал от стали больше раз | `ArmorPiercing`, `BotWeapon` | `maxBounces` |
| нитро хватало надольше | `NitroConfig` | `capacity` (секунды) |
| нитро быстрее восстанавливалось | `NitroConfig` | `regenPerSecond` |
| таран был слабее | `NitroConfig` | `ramDamage` или `ramCooldown` (больше — реже) |
| таран отбрасывал дальше | `NitroConfig` | `knockbackSpeed`, `knockbackDuration` |
| полоска здоровья была видна только у раненых | `HealthBarStyle` | включить `hideWhenFull` |

### Усиления

| Хочу, чтобы… | Файл | Поле |
| --- | --- | --- |
| усиление действовало дольше | `RapidFire` / `ExplosiveShells` / `Laser` / `Shield` | `duration` (секунды) |
| скорострельность была сильнее | `RapidFire` | `fireIntervalScale` (0,5 — вдвое чаще; меньше — ещё чаще) |
| взрывные снаряды были слабее | `ExplosiveShell` (в `Weapons/`) | `damage`, `explosionDamage`, `explosionRadius` |
| лазер бил реже / слабее | `Laser` | `laserInterval` / `laserDamage` |
| лазер меньше замедлял танк | `Laser` | `laserSpeedMultiplier` (1 — не замедляет) |
| пикап был другого цвета | любое усиление | `pickupColor` |

Как часто появляются усиления и сколько их может лежать на карте, настраивается не в файле, а на объекте
`PowerUpSpawner` в сцене (компонент *Power Up Spawner*): `firstDelay`, `baseInterval`, `minInterval`, `maxInterval`,
`maxOnMap`, а список возможных усилений — `powerUps`.

### Боты

Характеров три, и у каждого свой файл. Чтобы поменять поведение всех ботов, правьте все три (или тот, что назначен
нужному боту: на арене `Bot_A` и `Ally_1` — агрессоры, `Bot_B` и `Ally_2` — осторожные, `Bot_C` — уравновешенный).

| Хочу, чтобы боты… | Поле в профиле |
| --- | --- |
| стреляли точнее | `aimSpreadDegrees` (меньше — точнее), `leadFactor` (больше — лучше упреждение) |
| медленнее реагировали | `reactionDelayMin`, `reactionDelayMax` |
| стреляли с большего расстояния | `fireRange` |
| держались дальше / ближе в бою | `preferredDistance` |
| чаще таранили | `aggression` (шанс тарана), `nitroRamDistance` (0 — никогда) |
| реже уворачивались от снарядов | `dodgeChance` (0 — никогда) |
| отступали раньше / позже | `retreatHealthFraction` (доля здоровья, при которой бежит; 0 — никогда) |
| чаще ездили за усилениями | `powerUpGreed` (0 — игнорирует, 1 — едет за любым поблизости) |
| чаще держали точку, а не гонялись за врагами | `pointDuty` (шанс стать «держателем» при каждом появлении) |
| реже переключались между целями | `targetHoldTime` (больше), `retargetDistanceRatio` (меньше) |

Чтобы сделать бота с новым характером, скопируйте профиль (`Ctrl+D`), поменяйте числа и назначьте новый профиль
нужному боту. Подробнее — в [Рецептах](../articles/recipes.md#новый-характер-бота).

Сила ботов от прокачки игрока — прибавки «за уровень» в `BotTankConfig` (`healthPerLevel`, `speedPerLevel`) и
`BotWeapon` (`damagePerLevel`, `speedPerLevel`, `fireRatePerLevel`).

### Режимы

| Хочу, чтобы… | Файл | Поле |
| --- | --- | --- |
| в «Команде на команду» играли до 5 побед | `TeamElimination` | `roundsToWin` |
| паузы между раундами были короче | `TeamElimination` | `roundIntroSeconds`, `roundEndSeconds` |
| матч был короче | `CapturePoint`, `Deathmatch` | `timeLimit` (секунды) и/или `scoreLimit` (очки) |
| точка давала очки быстрее | `CapturePoint` | `pointsPerSecond` |
| возрождение было быстрее | `CapturePoint`, `Deathmatch` | `respawnDelay` (секунды) |
| в «Все против всех» больше ценились убийства | `Deathmatch` | `killPoints`; очки за выживание — `survivalStartSeconds`, `survivalIntervalSeconds`, `survivalPoints` |
| возрождались подальше от врагов | `Deathmatch` | `safeSpawnDistance` |
| доп. цель была проще / давала больше | любой режим | `extraGoalHoldSeconds` (точка), `extraGoalKills` (все против всех), `extraGoalGears` |
| поменять текст карточки в меню | любой режим | `displayName`, `description` |

### Награды и улучшения

| Хочу, чтобы… | Файл | Поле |
| --- | --- | --- |
| за убийство / победу давали больше шестерёнок | `ProgressionConfig` | `gearsPerKill` / `gearsPerWin` |
| улучшения дорожали медленнее | `ProgressionConfig` | `costGrowth` (1,35 — +35 % за уровень) |
| первое улучшение ветки стоило дешевле | `ProgressionConfig` | `branches` → `baseCost` нужной ветки |
| улучшение корпуса давало больше здоровья | `PlayerTankConfig` | `healthPerLevel` (и поправьте `description` ветки в `ProgressionConfig`) |
| улучшение двигателя давало больше скорости | `PlayerTankConfig` | `speedPerLevel` |
| улучшение орудия давало больше урона | `ArmorPiercing`, `Fragment` | `damagePerLevel`, `explosionDamagePerLevel` |
| улучшение скорострельности было заметнее | `ArmorPiercing`, `Fragment` | `fireRatePerLevel` |
| улучшение нитро давало больше | `NitroConfig` | `capacityPerLevel` |

### Звук

| Хочу… | Файл | Что сделать |
| --- | --- | --- |
| заменить звук | `SfxLibrary` | найти запись с нужным `id` и положить новый файл в `clips` вместо заглушки |
| чтобы звук был тише | `SfxLibrary` | `volume` записи |
| чтобы повторы звучали разнообразнее | `SfxLibrary` | положить в `clips` несколько вариантов, расширить `pitch` |
| добавить музыку | `SfxLibrary` | `menuMusic`, `battleMusic`, громкость — `musicBaseVolume` |

## Настройки карты

Время восстановления кирпича, его прочность и мигание настраиваются не в файле, а на объекте `Bricks` в сцене
(компонент *Destructible Tilemap*): поля `restoreDelay`, `brickDurability`, `warningDuration`. Тени стен — на объекте
`WallShadows` (`offset`, `blurRadius`, `opacity`). Радиус зоны точки — на объекте `CapturePoint` (`radius`).
