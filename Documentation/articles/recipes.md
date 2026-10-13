# Рецепты

Короткие инструкции для частых задач. Сцены, префабы и ассеты меняются в редакторе Unity (или через MCP),
текст `.unity`, `.prefab`, `.asset`, `.meta` руками не правим.

## Новая тестовая сцена в меню

1. Создайте сцену в `Assets/_Project/Scenes/Sandbox/<Область>.unity` (удобно скопировать готовую песочницу).
2. Добавьте её в **File → Build Settings**. Без этого кнопка в меню будет неактивной.
3. Откройте `Scenes/Menu.unity`, у объекта `MenuScreens` (<xref:TankBrawl.UI.MenuScreens>) добавьте запись в
   `debugScenes`: `label` — текст кнопки, `sceneName` — имя сцены без пути. Кнопка появится в **Настройки → Отладка**.

Кнопку «Назад» и экранное управление добавлять не нужно: <xref:TankBrawl.UI.SceneNavigator> и
<xref:TankBrawl.UI.TouchControlsBootstrap> создаются сами. Если в сцене нет `MatchManager`, это просто бой без очков.

## Новый тип снаряда

1. **Create → TankBrawl → Weapon Config** (или скопируйте `Fragment` / `ArmorPiercing`), заполните числа
   (поля описаны в [Параметрах](inspector.md#tankbrawlweapons)): `displayName` — надпись на кнопке «СНАРЯД»,
   `fireSfx` — звук выстрела, прибавки «за уровень».
2. Для взрывного типа задайте `explosionRadius > 0`, `explosionDamage`, `explosionBlockDamage`, `explosionEffect`
   (префаб `ExplosionEffect`) и `maxBounces = 0`, если от стали он должен взрываться.
3. Сделайте префаб снаряда — вариант `Projectile_ArmorPiercing` со своим спрайтом. Коллайдер не нужен: хитбоксы
   задаются радиусами в конфиге. Типы различаются **силуэтом**, не только цветом.
4. Назначьте префаб в `projectilePrefab` конфига, а конфиг — в `TankWeapon` нужного танка: основным (`weapon`) или
   в список `extraAmmo`, чтобы он листался кнопкой «СНАРЯД».

## Новый характер бота

1. **Create → TankBrawl → Bot Profile** (или скопируйте один из `ScriptableObjects/Bots`).
2. Покрутите числа: агрессивнее — больше `aggression` и `nitroRamDistance`, меньше `preferredDistance`,
   `retreatHealthFraction` и `reactionDelay`; осторожнее — наоборот. Точнее — меньше `aimSpreadDegrees`, больше
   `leadFactor`. Чаще уворачивается — больше `dodgeChance`. Охотнее держит точку — больше `pointDuty`.
3. Назначьте профиль в `BotTankInput` конкретного бота в сцене (или на варианте префаба `Bot`, чтобы он стал
   профилем по умолчанию).

## Новое усиление

Если хватает существующих видов (скорострельность, взрывные снаряды, лазер, щит, ремонт, заряд нитро) с другими
числами — просто **Create → TankBrawl → Power-Up Config** и добавьте ассет в `powerUps` спавнера (для ремонта и заряда
нитро ассет того же вида заменяет числа по умолчанию). Новый **вид** требует кода:

1. Добавьте значение в <xref:TankBrawl.PowerUps.PowerUpKind> (в конец) и, если нужно, поля его секции в
   <xref:TankBrawl.PowerUps.PowerUpConfig>.
2. Эффект сделайте рычагом в компоненте танка, которого он касается (как `TankWeapon.FireIntervalScale` или
   `TankController.SpeedMultiplier`), и сбрасывайте его в `OnEnable` / `OnDisable` этого компонента.
3. Включайте и выключайте рычаг в `TankPowerUps.Refresh()`. Мгновенный вид (как ремонт) — в `TankPowerUps.ApplyInstant`
   и в `PowerUpConfig.IsInstant`; подпись над пикапом — в `PowerUpConfig.MapLabel`; когда он нужен боту —
   в `BotTankInput.IsWanted`.
4. Создайте ассет, добавьте его в `powerUps` у <xref:TankBrawl.PowerUps.PowerUpSpawner> на арене и в песочнице.
5. Если бот должен вести себя с ним иначе — см. `BotTankInput.ActAttack` (так сделано для лазера).

## Новый режим матча

1. Добавьте значение в конец <xref:TankBrawl.Match.MatchModeKind> (в ассетах режимов `kind` хранится числом).
2. Напишите класс режима, унаследованный от <xref:TankBrawl.Match.MatchModeBase>: `Begin` (добавить ячейки счёта
   `AddEntry`, вызвать `match.RespawnAll()`), `Tick` (очки, часы `SetClock`, конец — `match.FinishMatch`), `OnDied`
   (только записать; возрождение — `match.ScheduleRespawn`). Тексты HUD меняйте через `SetTitle` / `SetBanner` /
   `SetHint` и `BumpScore()`, только когда они действительно изменились.
3. Добавьте `case` в `MatchManager.CreateMode` — единственное место, где режимы создаются.
4. **Create → TankBrawl → Match Mode Config** в `ScriptableObjects/Match`, выберите `kind`, заполните числа и тексты.
5. Добавьте ассет в массив `modes` у `MenuScreens` (порядок карточек = порядок массива) и, при желании, иконку
   в `UiTheme.modeIcons`.
6. Если режиму нужны свои объекты сцены (как зона точки), включайте их в `MatchManager.Start` по `config.kind`.

## Новая карта

Карты рисуются текстом и собираются генератором — пошагово в статье [Карты](maps.md#новая-карта).

## Новый звук

1. Добавьте значение **в самый конец** <xref:TankBrawl.Audio.Sfx>: в ассетах (`SfxLibrary`, `fireSfx` у снарядов)
   оно хранится числом, и вставка в середину сдвинет все звуки после неё.
2. В ассете `ScriptableObjects/Resources/SfxLibrary` добавьте запись с этим `id` и клипами. Можно сразу запустить
   **TankBrawl → Generate Placeholder Sounds** — он сделает заглушку.
3. Вызовите `AudioManager.Play(Sfx.X)` или `AudioManager.PlayAt(Sfx.X, позиция)` там, где происходит событие.

Заменить заглушку настоящим звуком: положите клип в нужную запись `SfxLibrary` вместо файла из `Audio/Placeholders`.

## Новый разрушаемый объект

Реализуйте <xref:TankBrawl.Core.IDamageable>:

- `Kind = Unit` — если в него должны попадать большим хитбоксом и он теряет HP (`DamageInfo.Amount`);
- `Kind = Terrain` — если это часть местности с прочностью (`DamageInfo.BlockDamage`).

Компонент должен стоять на объекте с коллайдером или выше по иерархии: снаряд ищет его через
`GetComponentInParent<IDamageable>()`. Таран по `Terrain` передаёт `Amount = 0`. Взрыв ломает по площади только
<xref:TankBrawl.Map.DestructibleTilemap>, остальные `Terrain`-объекты он пропускает.

## Новое static-поле

Domain reload выключен, поэтому любое static-поле нужно сбрасывать:

```csharp
static readonly List<Foo> Active = new List<Foo>();

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics()
{
    Active.Clear();
}
```

От static-событий всегда отписывайтесь в `OnDisable` / `OnDestroy`.
