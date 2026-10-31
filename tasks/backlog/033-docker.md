---
id: 033
status: planned
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


Уточнение 2026-10-31 (claude-audit): выход публикации задают 031 (`dotnet publish ImageGen.Web`) и 032 (`npm run build` → `wwwroot`). Папка данных Web — `IMAGEGEN_DATA_DIR` (план 031).

## План
Реализация — после 031 и 032. Сначала проверка, затем код.

1. `Dockerfile` в корне, три стадии: `node:22-bookworm-slim` (`npm ci`, `npm run build`) → `mcr.microsoft.com/dotnet/sdk:10.0` (`dotnet publish ImageGen/ImageGen.Web -c Release -o /app` с собранным `wwwroot`) → `mcr.microsoft.com/dotnet/aspnet:10.0` (Debian, не Alpine — SQLite проверен на glibc). Теги — с конкретной минорной версией на момент реализации. `USER $APP_UID` (встроенный пользователь образа .NET), `ENV IMAGEGEN_DATA_DIR=/data ASPNETCORE_HTTP_PORTS=8080`, `VOLUME /data`, `EXPOSE 8080`, `ENTRYPOINT ["dotnet","ImageGen.Web.dll"]`, `HEALTHCHECK` на `/api/health`.
2. `.dockerignore`: `.git`, `tmp`, `**/bin`, `**/obj`, `**/node_modules`, `poc`, `docs`, `tasks` — не исключать `ImageGen/`, `Directory.*.props`, `global.json`.
3. `docker-compose.yml`: сервис `imagegen`, `ports: ["127.0.0.1:8080:8080"]`, `volumes: [imagegen-data:/data]`, `environment: IMAGEGEN_WEB_PASSWORD: ${IMAGEGEN_WEB_PASSWORD:?set the password}`, `stop_grace_period: 30s`, именованный том. Пароль только через окружение, не через ARG/слой.
4. Права: свежий именованный том создаётся с владельцем `$APP_UID` (каталог `/data` создаётся в образе с этим владельцем); bind mount — инструкция `chown` в документации, без `chmod 777`.
5. Smoke `docker/smoke.sh` (не в `scripts/`): `docker compose up -d` с тестовым паролем → health → вход → сохранить ключ поддельного провайдера (через `providers.json` в томе с localhost-заглушкой) → прогон → `docker compose down` (без `-v`) → `up` → вход, ключ читается, картинка отдаётся, повторного submit нет → `down -v` в конце. Без внешних провайдеров.
6. CI — отдельным предложением с diff workflow и только после разрешения владельца; публикация в registry — отдельная задача.

Проверки: `docker build`, `docker compose config`, `docker/smoke.sh`, `scripts/check.sh`; в отчёте — версия Docker и архитектура. Документы: `docs/knowledge/docker.md` (новый: запуск, том, бэкап `/data`, смена пароля), `docs/index.md`, `README.md`.

Файлы: `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `docker/smoke.sh`, `docs/knowledge/docker.md`, `docs/index.md`, `README.md`.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Устаревший блокер ожиданием016 снят: код скелета существует. Задача допускается к планированию, но не получила автоматического одобрения реализации. Исторические комментарии сохранены.

### 2026-10-31, claude-audit
План готов, статус planned; занимать после 031 и 032. Допущения: Debian-образы .NET, порт 8080 только на 127.0.0.1, именованный том; smoke-скрипт в новой папке `docker/`, а не в `scripts/`.
