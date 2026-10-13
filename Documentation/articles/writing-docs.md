# Как писать документацию

Сайт собирается из четырёх источников:

| Источник | Где лежит | Куда попадает |
| --- | --- | --- |
| XML-комментарии `///` | в `.cs` | раздел [API](../api/TankBrawl.Core.yml) |
| `[Tooltip]`, `[Header]`, `[Min]`, `[Range]` полей | в `.cs` | [Параметры в инспекторе](inspector.md) и раздел «Поля в инспекторе» на странице класса |
| Статьи | `Documentation/articles/*.md`, описания неймспейсов — `Documentation/namespaces/*.md` | раздел «Для разработчиков» и страницы неймспейсов в API |
| Страницы для нетехнических читателей | `Documentation/game/*.md` | раздел «Об игре» |

Скрипты редактора (`Assets/_Project/Editor`) в API не попадают: DocFX читает только `Assets/_Project/Scripts`.

## Что обновить вместе с кодом

- **Новая папка в `Scripts`** (= новый неймспейс): файл `Documentation/namespaces/TankBrawl.<Папка>.md` с описанием
  и строка в таблице неймспейсов в [Архитектуре](architecture.md).
- **Новая механика или заметное изменение поведения**: статья в `articles/` (новая — ещё и строка в `articles/toc.yml`
  и в таблице на главной), а если это видно игроку — страницы `game/rules.md`, `game/match.md`, `game/progress.md`.
- **Новый конфиг или поле, которое крутят геймдизайнеры**: таблицы в [Как настраивать игру](../game/tuning.md).
- **Новый частый сценарий** («как добавить…»): [Рецепты](recipes.md).
- Числа в статьях — значения из ассетов на момент написания. Поменяли баланс — поправьте таблицы в
  `game/rules.md`, `game/match.md` и статьях про оружие, усиления и режимы.

## Правила для комментариев в коде

Цель — чтобы блок кода был понятен с первого прочтения. Комментарий нужен, только если код сам этого не говорит.

- **Каждый класс** получает `/// <summary>`: что он делает и как связан с остальными. Это главный текст на его странице.
- **Публичные члены** — `<summary>`, если имя не объясняет всего: единицы измерения, когда срабатывает, что значит
  `null`, побочные эффекты. Очевидное (`Max`, `IsDead`) не комментируем.
- **Реализация интерфейса** — `/// <inheritdoc/>`, описание берётся из интерфейса.
- **Поля для инспектора** — только `[Tooltip("…")]`, без `///`: тултип виден и в Unity, и на сайте.
  Пишите единицы (`units/s`, `s`, `deg`) и значение из баланса (`Balance v0.2: 2.8`).
- **Внутри методов** — обычный `//`, и только на «почему», а не на «что»: обход бага, неочевидная математика,
  порядок, от которого зависит корректность.
- Язык комментариев в коде — английский (как и идентификаторы), статьи — на русском.
- В `<see cref="…"/>` ссылайтесь только на публичные типы и члены: ссылки на приватные поля на сайте не работают,
  их пишите как `<c>fieldName</c>`.

```csharp
/// <summary>
/// Nitro stock and state. The stock drains only while the button is held and the tank is actually driving.
/// </summary>
public class TankNitro : MonoBehaviour
{
    [Tooltip("Seconds of continuous boost in a full tank. Balance v0.2: 3")]
    [Min(0.1f)] public float capacity = 3f;

    /// <summary>Stock as 0..1, for a future bar.</summary>
    public float Normalized => Max > 0f ? stock / Max : 0f;

    // Runs before TankController, so the controller sees this step's state.
    ...
}
```

## Сборка

```sh
./Documentation/build.sh --serve     # Linux / macOS
Documentation\build.cmd --serve      # Windows
```

Что происходит внутри:

1. `dotnet tool restore` — ставит DocFX той версии, что записана в `dotnet-tools.json`.
2. `dotnet run Documentation/tools/InspectorParams.cs` — собирает таблицы параметров из атрибутов полей.
3. `dotnet docfx Documentation/docfx.json` — читает исходники из `Assets/_Project/Scripts` и собирает сайт
   в `Documentation/_site`.

DocFX разбирает исходники без Unity, поэтому типы `UnityEngine` для него неизвестны и в логе много
предупреждений — это нормально. Сгенерированные файлы (`_site`, `api`, `obj`, `articles/inspector.md`)
в git не коммитятся.

Диаграммы в статьях пишутся на [Mermaid](https://mermaid.js.org/) в блоке ` ```mermaid `, ссылки на классы —
`<xref:TankBrawl.Tank.TankController>`.

## Публикация

Workflow `.github/workflows/docs.yml` пересобирает сайт и выкладывает на GitHub Pages при каждом пуше в `dev`,
если менялись скрипты или `Documentation/`. Запустить вручную: GitHub → Actions → Docs → Run workflow.
