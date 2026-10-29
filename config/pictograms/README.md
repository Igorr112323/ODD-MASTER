# Пиктограммы

Формат в клетках масштабной сетки прил. Б ГОСТ Р 52290-2024 (ADR-0023). Один файл описывает
одну или несколько пиктограмм; обязательные поля — `id`, `sign`, `status`, `source`, `grid`, `layers`.

- `grid.cellsAcross` — сколько клеток сетки покрывает размер носителя `grid.spans` (A/B/D/H).
- `origin: center`, `yAxis: up` — начало координат в центре знака, ось Y вверх.
- `layers[].shapes[]` — `polygon`, `polyline`, `circle`, `text`. Цвет — семантическое имя из каталога.
- `status`: `Placeholder` (заглушка), `StudentTranscribed` (после `odd-tools pictogram import`),
  `StudentVerified` (после ручной сверки), `Unverified`.

**Пиктограммы ГОСТ агент не оцифровывал**: он не видит стандарт. Здесь лежат только заглушки
и результат работы студента по `docs/howto/PICTOGRAMS.md`. Список «что оцифровать» —
`docs/VERIFICATION_QUEUE.md` (GOST-019).
