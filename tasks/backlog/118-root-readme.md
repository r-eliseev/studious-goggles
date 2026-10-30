---
id: 118
status: done
assignee:
claimed:
created: 2026-10-29
---

# Добавить корневой README без обещаний roadmap-функций

## Описание
У GitHub repo нет landing README. Нужен краткий user/developer start, основанный только на merged code/release после provider docs/privacy/built-in registration. Релизные известные ограничения показываются явно и не задерживают базовый README.

Зависит от: 115, 116, 136.
Среда: Linux или Windows.

## Критерии готовности
- «Current» и roadmap разделены; Web/Docker/gallery/spending не названы готовыми.
- Built-in providers после 136: OpenRouter, fal и Together AI; additional OpenAI-compatible config manual. Together live paid behavior помечен как не проверенный, если smoke не выполнен.
- Fal Core support не выдаётся за тот же first-run UI setup, пока экран/flow не подтверждён.
- Download table различает выпускаемые workflow пакеты и проверенную поддержку на устройствах. Ссылка на Releases ведёт к реальным artifacts; отсутствие проверки arm64 (если 100 ещё не завершена) названо ограничением, без обещания работающей архитектуры. Android debug-signed sideload, no store/MSIX/update claims.
- Privacy: local app data с учётом отключённого backup 115; обычные Debug logs и debug trace могут содержать user/provider content, их нельзя публиковать.
- Dev .NET10/Avalonia/four test commands, platform details ссылкой.
- Все relative links проходят check-docs; no badges/unverified support/app id.

## Анализ
README нужен как вход для пользователя/разработчика и не зависит от будущего transport/recovery/concurrency refactor. Нужны 115 (privacy claim),116 (provider wording),136 (builtin Together и завершённый UI ключей); 117 не влияет на quickstart.119/120 сняты,100 не блокирует README: при нерешённой проверке arm64 описать limitation вместо обещания. Технические детали бюджета/recovery/concurrency оставить ссылками на документацию, которую обновляют соответствующие кодовые задачи. `scripts/check-docs.sh` проверяет markdown links во всём repo, включая root README. ApplicationId target в status не совпадает current csproj до task030, поэтому не упоминать.

## План
Для Haiku/Luna: сначала evidence table в отчёте, затем README. Никакого internet copy.

1. Evidence table claim→source: builtin config; provider setup page models после 135/136; release workflow + фактически доступные Releases; platform matrix и исход 100; logging handlers/spec; secret stores; status roadmap. Если факт не подтверждён — убрать claim или назвать ограничение, не додумывать. Не переписывать задачи-зависимости или workflow ради README.
2. Создать sections: purpose; Current; Download; First run/keys; Local data/privacy; Development; Documentation; Roadmap; License.
3. Current перечисляет только code evidence. Together wording строго как criteria.
4. Download: ссылка `https://github.com/r-eliseev/studious-goggles/releases`, таблица семейства оболочки/ОС/архитектуры/формата по актуальному release.yml; отдельно указать платформы, на которых runtime ещё не проверен. Не выводить архитектуру бинарника из одного имени архива. Если 100 ещё открыта, MAUI Windows arm64 отметить непроверенным. Не давать прямую ссылку на несуществующий asset. No MAUI iOS/MacCatalyst, store Android, installer/auto-update.
5. Privacy: prompts/images DB/files local and Android automatic backup disabled after115; обычные Debug logs **и** debug folder могут содержать пользовательский контент, не прикладывать их публично без очистки. Не обещать шифрование всей локальной БД/изображений; secrets — platform stores с описанным fallback. При описании Information указать design policy без неподтверждённой гарантии отсутствия exception content во всех текущих ветках до 127.
6. Development commands copied exactly from AGENTS/build workflow; no local test counts.
7. Validate relative links with check.sh; внешние ссылки проверить отдельно, так как check-docs их пропускает. Все commands — для отдельных csproj на Linux, не всего MAUI solution. Указать точную основную команду build Avalonia и четыре существующие команды test из AGENTS, без локальных counts.
8. После проверки заполнить «Отчёт» со ссылками на сверенные code/docs и оставшимися неизвестными; выполнить `scripts/release-task.sh 118` и `bash scripts/check.sh`, обновить PR по `docs/tasks.md`. Код приложения не менять.

### Проверка
```
bash scripts/check.sh
```

Файлы: `README.md`

## Отчёт

Создан корневой README на русском: отдельно описаны текущая реализация исходников и roadmap, загрузки v0.1.4, первый запуск, локальные данные/приватность, команды разработки, документация и лицензия. Код приложения, scripts и workflow не менялись.

Evidence table (утверждение → источник):

| Утверждение в README | Проверенный источник |
|---|---|
| OpenRouter, fal.ai, Together AI встроены; вместе с тем Together остаётся image-only, живой ответ не проверен | `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, `docs/knowledge/together-ai.md`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderConfigLoaderTests.cs` |
| OpenRouter требуется для первого запуска ассистента; fal/Together необязательны, дополнительные провайдеры задаются конфигом | `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `SettingsPageModel.cs`, `docs/architecture.md` |
| Набор моделей Together и неизвестная фактическая стоимость | built-in JSON, `docs/knowledge/together-ai.md`, `GenerationServiceTests.StartRunAsync_BuiltInTogether_UsesConfiguredImageContractAndKeepsCostUnknown` |
| Матрица пакетов и форматы | `.github/workflows/release.yml`; реальные 9 asset names/URLs проверены `gh release view v0.1.4 --json assets` |
| MAUI Windows arm64 asset v0.1.4 фактически содержит x64 PE | отчёт `tasks/backlog/100-release-maui-windows-arm64.md`, `docs/knowledge/maui.md`; прямой asset присутствует в Release |
| Runtime/ручные проверки не приравнены к CI build | `docs/knowledge/dev-environment.md`, статусы задач 016/046/140; отдельная проверка Windows PE описана задачей 100 |
| Данные локальны, Android backup отключён, OEM transfer не проверен | `docs/architecture.md`, `docs/knowledge/maui.md` (задача 115) |
| Debug log и trace могут включать пользовательский/provider content | `docs/architecture.md`, `docs/specs/debug-trace.md` |
| Лицензия MIT | `LICENSE` |

Проверки: `bash scripts/check.sh` прошёл, включая проверку относительных Markdown-ссылок. Внешняя страница Releases и все прямые asset URL сверены с GitHub Release v0.1.4; несуществующих артефактов в таблице нет. `git diff --check` прошёл.

Отдельно указано, что source code из PR #51 новее опубликованного v0.1.4; Android APK debug-signed; интерактивный запуск многих платформ и OEM device-transfer не проверены. README не обещает работу arm64-артефакта MAUI v0.1.4 и не называет Web, Docker, галерею, сравнение или учёт расходов готовыми.

## Комментарии
