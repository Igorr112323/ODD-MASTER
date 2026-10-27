# Сборка адаптера nanoCAD (Windows, неделя 1)

**Статус: не проверено.** В среде агента не было SDK nanoCAD 26, поэтому `src/OddMaster.NanoCAD/` не компилировался и **не входит** в `OddMaster.sln` и CI. Это сделано намеренно: зелёная сборка на Linux не должна зависеть от проприетарных DLL.

## Что есть

- `OddMaster.NanoCAD.sln` — отдельное решение: `OddMaster.Core` + `OddMaster.NanoCAD`.
- `OddMaster.NanoCAD.csproj` — ссылки на сборки SDK через свойство `$(NanoCadSdkPath)`, по умолчанию `~/nanoCAD/sdk26/include-x64`. DLL в репозиторий **не кладём** (условие задания).
- `HelloCommand.cs` — команда `ODD_HELLO` и пример получения геометрии из ядра.
- `SignObject.cs` — кастомный объект; геометрия приходит из `OddMaster.Core.Ports.IDrawingPort`.
- `NanoCadDrawingPort.cs` — реализация порта примитивами САПР.

## Порядок действий студенту

1. Зарегистрироваться на сайте разработчика, скачать MultiCAD .NET SDK для nanoCAD 26 (Windows). Требуется вузовский доступ — это риск, записано в `QUESTIONS.md` (Q-003) и `NIGHT_REPORT.md`.
2. Установить .NET 8 SDK (проверить SDK-008: версию фреймворка, которую ждёт nanoCAD 26).
3. Проверить реестр `docs/SDK_ASSUMPTIONS.md`: восемь предположений SDK-001…SDK-008. Каждое имеет комментарий `// [ПРОВЕРИТЬ В SDK: SDK-NNN]` рядом с вызовом в коде.
4. Открыть `OddMaster.NanoCAD.sln`, передать путь к SDK:
   ```
   dotnet build OddMaster.NanoCAD.sln -p:NanoCadSdkPath=C:\Path\To\sdk26\include-x64
   ```
5. Сверить имена DLL (SDK-001), пространства имён (SDK-002), атрибут команды (SDK-003), базовый класс и метод отрисовки (SDK-004/SDK-005), примитивы (SDK-006). Исправить код и обновить реестр: строка «не проверено» → «проверено, как именно».
6. Загрузить DLL в nanoCAD (SDK-007: `NETLOAD` или меню приложений), выполнить `ODD_HELLO`.
7. Только после этого добавлять адаптер в общий CI или в основное решение — и то conditional-сборкой на Windows.

## Чего не делать

- Не копировать DLL SDK в git.
- Не убирать `NotImplementedException` «чтобы собралось»: адаптер честно говорит, что API не проверен.
- Не добавлять `#pragma warning disable` для заглушек; см. `CONTRIBUTING.md`.
