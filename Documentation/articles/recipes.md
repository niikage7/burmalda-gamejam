# Рецепты

Короткие инструкции для частых задач. Сцены, префабы и ассеты меняются в редакторе Unity (или через MCP),
текст `.unity`, `.prefab`, `.asset`, `.meta` руками не правим.

## Новая тестовая сцена в меню

1. Создайте сцену в `Assets/_Project/Scenes/Sandbox/<Область>.unity` (удобно скопировать готовую песочницу).
2. Добавьте её в **File → Build Settings**. Без этого кнопка в меню будет неактивной с пометкой «нет в Build Settings».
3. Откройте `Scenes/Menu.unity`, у объекта с <xref:TankBrawl.UI.MainMenu> добавьте запись в `entries`:
   `label` — текст кнопки, `sceneName` — имя сцены без пути.

Кнопку «Назад» добавлять не нужно: <xref:TankBrawl.UI.SceneNavigator> создаётся сам.

## Новый тип снаряда

1. **Create → TankBrawl → Weapon Config**, заполните числа из баланса (поля описаны в
   [Параметрах](inspector.md#tankbrawlweapons)).
2. Сделайте префаб снаряда с компонентом <xref:TankBrawl.Weapons.Projectile> и спрайтом. Коллайдер не нужен:
   хитбоксы задаются радиусами в конфиге. Типы различаются **силуэтом**, не только цветом.
3. Назначьте префаб в `projectilePrefab` конфига, конфиг — в `TankWeapon` нужного танка.

## Новый характер бота

1. **Create → TankBrawl → Bot Profile** (или скопируйте `ScriptableObjects/Bots/DefaultBotProfile`).
2. Покрутите числа: агрессивнее — больше `nitroRamDistance`, меньше `stopDistance` и `reactionDelay`;
   осторожнее — наоборот. Точнее — меньше `aimSpreadDegrees`, больше `leadFactor`.
3. Назначьте профиль в `BotTankInput` на варианте префаба `Bot`.

## Новый разрушаемый объект (например, база)

Реализуйте <xref:TankBrawl.Core.IDamageable>:

- `Kind = Unit` — если в него должны попадать большим хитбоксом и он теряет HP (`DamageInfo.Amount`);
- `Kind = Terrain` — если это часть местности с прочностью (`DamageInfo.BlockDamage`).

Компонент должен стоять на объекте с коллайдером или выше по иерархии: снаряд ищет его через
`GetComponentInParent<IDamageable>()`. Таран по `Terrain` передаёт `Amount = 0`, поэтому базу он не убьёт с одного удара.

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
