---
id: 032
status: todo
assignee:
claimed:
created: 2026-10-27
---

# Vue-фронт

## Описание
Vue 3 + Vite + TypeScript для Web API: вход, сессии, чат, выбор моделей и ввод ключа. Продуктовые сценарии соответствуют общей логике MAUI/Avalonia; браузер не вызывает provider API напрямую и не хранит provider keys локально. Сборка публикуется в wwwroot Web-хоста.

Зависит от: 031.
Среда: Linux или Windows; Node/npm и браузер для UI-проверок.

016 не блокирует планирование. Статус todo: сначала контракт API 031 и небольшие планы; реализация после одобрения и готовности зависимостей.

## Критерии готовности
- Вход/выход, сессии и чат работают через cookie-auth API 031; 401 переводит на вход без потери данных на сервере.
- Выбор моделей и сохранение ключа дают то же продуктовое поведение, что общие Core-сервисы; цена/неизвестная цена/ошибки отображаются честно.
- Live updates не создают дубли карточек; после reconnect состояние восстанавливается с сервера; уход со страницы освобождает подписку.
- Provider key после отправки очищается из формы и не сохраняется в localStorage, IndexedDB или browser URL; frontend bundle не содержит секретов.
- Сборка воспроизводима через lockfile; published wwwroot работает без Vite dev-server, в том числе при прямом открытии deep link.
- UI доступен с клавиатуры, адаптируется к узкому экрану и проверен end-to-end без реальных provider calls.

## Анализ
Кода Web/ClientApp и API DTO ещё нет. Не пытаться переписать Presentation на TypeScript целиком: серверные Core-сервисы остаются источником правил, клиент рендерит DTO и отправляет команды. «Поведение совпадает с MAUI» не означает копирование каждого native menu/gesture; окончательный перечень экранов/действий фиксируется на момент плана по имеющимся общим сценариям.

Для Haiku/Luna нужны отдельные части: (1) scaffold/build/API client/error/auth state; (2) список/CRUD сессий; (3) чат/отправка/история; (4) модели/ключи; (5) live generation state/image rendering; (6) интеграционные E2E/публикация assets. Все части опираются на опубликованный контракт 031, не выдумывают собственные статусы и provider URLs.

## План
Этап подготовки плана:
1. По 031 перечислить endpoint/DTO/error/event contract для каждого экрана. Развести server-state, transient form state и router state; выбрать один простой подход и записать новые зависимости/ADR при необходимости.
2. Определить структуру `ImageGen.Web/ClientApp` (Vue3/Vite/TS), lockfile, npm scripts и публикацию wwwroot из ASP.NET publish. Dev proxy/test URLs не должны попадать в production bundle.
3. Зафиксировать routes login/sessions/chat/models/key/settings в фактическом объёме 031 и точки navigation, empty/loading/error/unauthorized states; не добавлять несуществующие gallery/spending endpoints.
4. Для network client описать credentials same-origin, CSRF из 031, отсутствие автоматического повторения платных POST, отмену устаревших GET, clear key form после submit и безопасный рендер текста ассистента без raw HTML.
5. Для event stream описать один consumer на активную страницу, merge по GenerationId/version либо refetch, reconnect/backoff и cleanup; тест «старый ответ другого session не заменяет новый экран».
6. Для каждой выделенной части назвать файлы, unit/component tests и E2E: auth/401, submit, provider error, reload/deep link, reconnect, две сессии, keyboard navigation, narrow viewport. Реальные ключи/платные запросы в тесты не добавлять.
7. После готового API и одобрения декомпозиции — planned и ожидание одобрения реализации. Будущие проверки: npm ci, typecheck, lint, test и build (точные имена scripts задать в плане), Web.Tests и browser smoke опубликованного хоста, `bash scripts/check.sh`; отчёт/завершение по docs/tasks.md.

Файлы для анализа: `tasks/backlog/031-web-host-and-api.md`, `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `docs/specs/chat-sessions.md`. Новые ClientApp-файлы/команды назвать в окончательных планах.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Устаревший блокер ожиданием016 снят: код скелета существует. Задача допускается к планированию, но не получила автоматического одобрения реализации. Исторические комментарии сохранены.
