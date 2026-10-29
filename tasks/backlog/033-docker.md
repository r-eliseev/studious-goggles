---
id: 033
status: todo
assignee:
claimed:
created: 2026-10-27
---

# Docker-образ Web-оболочки

## Описание
Многоэтапный Dockerfile для Web/Vue-хоста, docker-compose с постоянным томом /data и паролем из окружения, воспроизводимая сборка/проверка образа в CI. Нужна для утверждённого сценария self-hosted Web.

Зависит от: 031, 032.
Среда: Linux или Windows с рабочим Docker engine; на Windows Linux containers.

016 не блокирует планирование. Статус todo: точный publish output определяется 031/032, реализация после плана/одобрения.

## Критерии готовности
- Итоговый контейнер запускает опубликованный Web и собранные Vue assets без SDK, Node, исходников и секретов внутри образа.
- БД, картинки, encrypted secrets и Data Protection key ring находятся под /data и переживают recreate с тем же томом; cookie/secret decryption проверяется после рестарта.
- Приложение работает непривилегированным пользователем с корректными правами на /data; password обязателен и не имеет значения по умолчанию в compose.
- HTTP endpoint доступен снаружи только по явному пробросу порта; localhost smoke проходит с auth и без реального обращения к провайдерам.
- graceful stop и повторный запуск не удаляют данные, не запускают повторный submit оплачиваемого задания.
- Проверка образа в CI согласована отдельно: этот план сам по себе не разрешает править scripts/ или .github/workflows/.

## Анализ
Образ не собрать до появления publishable Web/ClientApp. Persist только imagegen.db недостаточно: потеря key ring делает encrypted key store нечитаемым, потеря images оставляет мёртвые пути. Нельзя считать размер/права bind mount одинаковыми на Linux и Docker Desktop: тестировать именованный том или явно описанный supported deployment.

Для Haiku/Luna разделить: (1) Dockerfile/build context и compose/runtime docs, (2) restart/persistence smoke, (3) CI build после отдельного разрешения на workflow. Новые теги registry, публикация образа и deployment не подразумеваются просьбой собрать образ; registry/credentials не изобретать.

Нужные runtime library/base-image зависимости определяются по фактическому Web/Data проекту. Alpine/musl не выбирать только ради размера без SQLite smoke. Загрузки при сборке учитывают proxy/CA среды; TLS verification не отключать.

## План
Этап подготовки плана:
1. По publish contract 031/032 назвать SDK/runtime base tags и порядок stages Vue build → dotnet publish → runtime, единственный output и process entrypoint. Описать .dockerignore без исключения нужных файлов и без включения tmp/github-auth, .git или development secrets.
2. Согласовать DataDirectory=/data и расположение DB/images/secret store/key ring с Web host. Назвать UID/GID и поведение свежего тома, documented volume ownership; no recursive chmod777.
3. Описать compose env/ports/volume/shutdown grace period. Пароль передаётся runtime env/поддержанным secret механизмом 031, не ARG/build layer; пример конфигурации не содержит реального пароля.
4. Test cases: первый запуск/миграции, недостающий пароль → fail startup, authenticated health/application call, save fake generation+key, stop/recreate with same volume, прочитать прежнюю картинку и проверить decryption key, no resubmit восстановленного job, том не удаляется cleanup.
5. Назвать build/smoke команды и файлы тестового fixture; healthcheck не должен обходить auth остальных endpoints или зависеть от внешнего провайдера.
6. Подготовить отдельное предложение CI с точным workflow diff и явно получить разрешение на .github/workflows/ до изменений. Publication в registry — отдельная задача после выбора registry/tag policy.
7. После готового плана planned и ожидание одобрения. Будущие проверки docker build/compose config/smoke/recreate, `bash scripts/check.sh`; отчёт с версией Docker/архитектурой и limitations, release-task по docs/tasks.md.

Файлы для анализа: `tasks/backlog/031-web-host-and-api.md`, `tasks/backlog/032-vue-frontend.md`, `docs/architecture.md`, `ImageGen/ImageGen.Data/SqliteDatabase.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`. Будущие новые `Dockerfile`, `.dockerignore`, `docker-compose.yml`, Web deployment docs и smoke fixture назвать в окончательном плане; scripts/workflows не менять автоматически.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Устаревший блокер ожиданием016 снят: код скелета существует. Задача допускается к планированию, но не получила автоматического одобрения реализации. Исторические комментарии сохранены.
