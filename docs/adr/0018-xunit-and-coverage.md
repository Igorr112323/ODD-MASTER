# ADR-0018: xUnit и coverlet.collector вместо самописного раннера

- **Статус:** принято (сессия 02, 28.10.2026).
- **Дата:** 2026-10-28.

## Контекст

CR-03: самописный OddFact/TestRunner, переопределение VSTest, нет покрытия, NuGet.Config с <clear/> без источников.

## Решение

Все 106 тестов перенесены 1:1: `[OddFact("имя")]` → `[Fact(DisplayName="имя")]`, статические классы/методы → экземплярные, `Assert` → тонкая обёртка `TestAssert` над `Xunit.Assert` (сохраняет русские сообщения и Close/NotContains). `Directory.Build.targets` удалён. Пакеты: Microsoft.NET.Test.Sdk 17.11.1, xunit 2.9.2, xunit.runner.visualstudio 2.8.2, coverlet.collector 6.0.2. Покрытие Cobertura — артефакт CI.

## Альтернативы

Оставить микро-раннер; вендорить nupkg в git.

## Последствия

Плюсы: стандартный `dotnet test`, покрытие, трассировка тестов. Минусы: зависимость от NuGet (в песочнице недоступен → CI как оракул). Цель — покрытие Core ≥80 %. Заменяет ADR-0014. Статус: принято.
