#!/usr/bin/env python3
"""Страж CR-21: тестовые проекты решения обязаны реально запускаться.

Что проверяется (без .NET SDK, только по файлам и логу `dotnet test`):

1. каждый проект из `tests/` присутствует в `OddMaster.sln` **и** имеет строки
   `ProjectConfigurationPlatforms`. Проект без них молча выпадает из
   `dotnet build/test OddMaster.sln` — именно так 22 теста DXF не запускались в CI;
2. если передан лог `dotnet test`, каждая тестовая сборка обязана в нём отметиться,
   а число сборок в логе обязано совпасть с числом тестовых проектов решения.

Код возврата 1 — гвард красный, job падает.

Использование:
    python3 scripts/check_solution_tests.py
    python3 scripts/check_solution_tests.py --solution OddMaster.sln --log test.log
"""

import argparse
import re
import sys
from pathlib import Path

PROJECT_LINE = re.compile(
    r'Project\("\{(?P<type>[0-9A-Fa-f-]+)\}"\)\s*=\s*"(?P<name>[^"]+)",\s*"(?P<path>[^"]+)",\s*'
    r'"\{(?P<guid>[0-9A-Fa-f-]+)\}"')

# Лог `dotnet test` в русской и английской локали SDK.
RUN_LINE = re.compile(
    r'(?:Тестовый запуск для|Test run for)\s+(?P<dll>[^\s(]+\.dll)', re.IGNORECASE)


def read_solution(path):
    """Возвращает (проекты, множество GUID с конфигурациями)."""
    text = path.read_text(encoding="utf-8-sig")
    projects = [m.groupdict() for m in PROJECT_LINE.finditer(text)]
    start = text.find("GlobalSection(ProjectConfigurationPlatforms)")
    configured = set()
    if start >= 0:
        end = text.find("EndGlobalSection", start)
        block = text[start:end]
        configured = {m.group(1).upper() for m in re.finditer(r"\{([0-9A-Fa-f-]+)\}\.", block)}
    return projects, configured


def is_test_project(project):
    """Тестовый проект — лежит в tests/ (служебные папки решения сюда не попадают)."""
    return project["path"].replace("\\", "/").lower().startswith("tests/")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--solution", default="OddMaster.sln")
    parser.add_argument("--log", default=None, help="лог `dotnet test` для сверки сборок")
    args = parser.parse_args()

    root = Path(__file__).resolve().parent.parent
    solution = root / args.solution
    if not solution.is_file():
        print("❌ нет файла решения: " + str(solution))
        return 1

    projects, configured = read_solution(solution)
    test_projects = [p for p in projects if is_test_project(p)]
    problems = []

    if not test_projects:
        problems.append("в решении не найдено ни одного тестового проекта — проверь путь tests/")

    for project in test_projects:
        if project["guid"].upper() not in configured:
            problems.append(
                "проект %s (%s) без строк ProjectConfigurationPlatforms: "
                "dotnet build/test OddMaster.sln его пропускает" % (project["name"], project["guid"]))

    csproj_missing = [p["name"] for p in test_projects
                      if not (root / p["path"].replace("\\", "/")).is_file()]
    for name in csproj_missing:
        problems.append("в решении указан отсутствующий файл проекта: " + name)

    # Тестовые проекты на диске, которых нет в решении, — тоже дыра.
    in_solution = {p["path"].replace("\\", "/").lower() for p in projects}
    for csproj in sorted((root / "tests").glob("*/*.csproj")):
        relative = csproj.relative_to(root).as_posix().lower()
        if relative not in in_solution:
            problems.append("тестовый проект не добавлен в решение: " + relative)

    assemblies = []
    if args.log:
        log_path = root / args.log
        if not log_path.is_file():
            problems.append("нет лога dotnet test: " + str(log_path))
        else:
            log_text = log_path.read_text(encoding="utf-8", errors="replace")
            assemblies = sorted({Path(m.group("dll")).name for m in RUN_LINE.finditer(log_text)})
            expected = {p["name"] + ".dll" for p in test_projects}
            missing = sorted(expected - set(assemblies))
            if missing:
                problems.append("в логе dotnet test нет сборок: " + ", ".join(missing))
            if assemblies and len(assemblies) != len(test_projects):
                problems.append("сборок в логе %d, тестовых проектов в решении %d"
                                % (len(assemblies), len(test_projects)))

    print("Тестовых проектов в решении: %d" % len(test_projects))
    for project in test_projects:
        print("  · %s" % project["name"])
    if args.log:
        print("Тестовых сборок в логе: %d" % len(assemblies))
        for name in assemblies:
            print("  · %s" % name)

    if problems:
        print("\n❌ Страж CR-21: тесты решения неполные")
        for problem in problems:
            print("  - " + problem)
        return 1

    print("\n✅ Все тестовые проекты решения запускаются")
    return 0


if __name__ == "__main__":
    sys.exit(main())
