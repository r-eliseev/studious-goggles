---
id: 118
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Добавить корневой README без обещаний roadmap-функций

## Описание
У GitHub repo нет landing README. Нужен краткий user/developer start, основанный только на merged code/release после исправлений docs/privacy/arm64.

Зависит от: 100, 115, 116, 117, 119, 120.
Среда: Linux или Windows.

## Критерии готовности
- «Current» и roadmap разделены; Web/Docker/gallery/spending не названы готовыми.
- Built-in: OpenRouter/fal. Additional OpenAI-compatible config manual; Together selected, not built-in/live-verified.
- Fal Core support не выдаётся за тот же first-run UI setup, пока экран/flow не подтверждён.
- Download table только фактические release.yml assets после 100; Android debug-signed sideload, no store/MSIX/update claims.
- Privacy: local app data с учётом отключённого backup 115; обычные Debug logs и debug trace могут содержать user/provider content, их нельзя публиковать.
- Dev .NET10/Avalonia/four test commands, platform details ссылкой.
- Все relative links проходят check-docs; no badges/unverified support/app id.

## Анализ
README выполняется последним в doc chain. `scripts/check-docs.sh` проверяет markdown links во всём repo, включая root README. ApplicationId target в status не совпадает current csproj до task030, поэтому не упоминать.

## План
Для Haiku/Luna: сначала evidence table в отчёте, затем README. Никакого internet copy.

1. Evidence table claim→source: builtin config; provider setup page models; release workflow after100; platform matrix; logging handlers/spec; secret stores; status roadmap.
2. Создать sections: purpose; Current; Download; First run/keys; Local data/privacy; Development; Documentation; Roadmap; License.
3. Current перечисляет только code evidence. Together wording строго как criteria.
4. Download: Releases link; exact outputs workflow. No MAUI iOS/MacCatalyst, store Android, installer/auto-update.
5. Privacy: prompts/images DB/files local and Android automatic backup disabled after115; Debug logs **и** debug folder sensitive; Information no bodies; secrets platform stores/fallback caveat.
6. Development commands copied exactly from AGENTS/build workflow; no local test counts.
7. Validate links with check.sh and manually list external links.

### Проверка
```
bash scripts/check.sh
```

Файлы: `README.md`

## Отчёт

## Комментарии
