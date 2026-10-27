#!/usr/bin/env bash
# Проверка документации. Код возврата 1, если есть ошибки.
cd "$(dirname "$0")/.."
fail=0

# 1. Обязательные файлы
for f in AGENTS.md docs/index.md docs/status.md docs/architecture.md docs/conventions.md docs/tasks.md; do
  [ -f "$f" ] || { echo "ОШИБКА: нет файла $f"; fail=1; }
done

# 2. Каждый файл docs/ упомянут в index.md: файлы верхнего уровня по имени, вложенные по имени каталога.
#    Не проверяются: index.md, записи журнала log/ и шаблоны _*.md.
if [ -f docs/index.md ]; then
  while IFS= read -r f; do
    rel="${f#docs/}"; base="$(basename "$rel")"
    case "$rel" in index.md|log/*) continue ;; esac
    case "$base" in _*) continue ;; esac
    if [ "$rel" = "$base" ]; then key="$base"; else key="${rel%%/*}/"; fi
    grep -qF "$key" docs/index.md || { echo "ОШИБКА: $rel не упомянут в docs/index.md"; fail=1; }
  done < <(find docs -type f -name '*.md' | sort)
fi

# 3. У каждого ADR есть строка «Статус:» с допустимым значением
for f in docs/decisions/*.md; do
  [ -e "$f" ] || continue
  case "$(basename "$f")" in _*) continue ;; esac
  grep -qE '^Статус: (предложено|принято|заменено|отклонено)' "$f" \
    || { echo "ОШИБКА: у $f нет строки 'Статус:' с допустимым значением"; fail=1; }
done

# 4. Битые относительные ссылки на .md-файлы
while IFS= read -r f; do
  dir="$(dirname "$f")"
  while IFS= read -r link; do
    [ -n "$link" ] || continue
    case "$link" in http*|mailto:*) continue ;; esac
    [ -e "$dir/$link" ] || { echo "ОШИБКА: битая ссылка в $f -> $link"; fail=1; }
  done < <(grep -oE '\]\([^)#[:space:]]+\.md(#[^)]*)?\)' "$f" | sed -E 's/^\]\(([^)#]+).*/\1/')
done < <(find . -name '*.md' -not -path './.git/*' -not -path './node_modules/*' | sort)

# 5. architecture.md описывает устройство, а не ход работ: без номеров задач, путей tasks/ и раздела о состоянии
if [ -f docs/architecture.md ]; then
  grep -nE '[Зз]адач[а-я]* +[0-9]{3}|tasks/|^#+ +Состояние' docs/architecture.md | while IFS= read -r line; do
    echo "ОШИБКА: docs/architecture.md:${line%%:*}: ход работ (номер задачи, tasks/ или раздел «Состояние») — ему место в «Отчёте» задачи"
  done | grep . && fail=1
fi

# 6. Предупреждение: изменён код, а docs/ и tasks/ нет (для CI на pull request)
base="${BASE_REF:-origin/main}"
if git rev-parse --verify -q "$base" >/dev/null 2>&1; then
  changed="$(git diff --name-only "$base"...HEAD 2>/dev/null)"
  if echo "$changed" | grep -qE '^ImageGen/' && ! echo "$changed" | grep -qE '^(docs|tasks)/'; then
    echo "ПРЕДУПРЕЖДЕНИЕ: изменён код в ImageGen/, но docs/ и tasks/ не затронуты"
  fi
fi

exit $fail
