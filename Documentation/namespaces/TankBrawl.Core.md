---
uid: TankBrawl.Core
summary: *content
---
Общие контракты, от которых зависят все остальные области: источник команд танка (`ITankInput`), получение урона (`IDamageable`, `DamageInfo`), команды и их цвета (`TeamMember`, `Team`, `TeamColors`). Плюс общие сервисы: камера (`CameraFollow`), тряска экрана (`ScreenShake`), частицы и вибрация (`Fx`), глобальные настройки. Интерфейсы меняет только ответственный. См. [Архитектура](../articles/architecture.md).
