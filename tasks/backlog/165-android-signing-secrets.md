---
id: 165
status: approved
assignee:
claimed:
created: 2026-10-31
---

# Добавить секреты подписи Android в GitHub

## Описание
Ручная задача владельца (решение 2026-10-31). Агент её не выполняет: прокси облачной сессии запрещает запись секретов GitHub (`403` на `/repos/…/actions/secrets`).

Владелец получил в чате 2026-10-31 файлы `github-secrets.txt` и `imagegen-release.keystore` (ключ из задачи 163, отпечаток SHA-256 `ecde686bc9970c00bbc74a32c8db5b99a3d1ccccecbffc8992a7a8d7942eb8cd`). До выполнения этой задачи `release.yml` подписывает APK отладочным ключом, как раньше.

Зависит от: 163 (код `release.yml` в main).
Среда: человек, веб-интерфейс GitHub.

## Критерии готовности
- В репозитории есть секреты Actions `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD` из `github-secrets.txt`.
- Keystore и пароль сохранены в менеджере паролей; файлы удалены из загрузок.
- Ручной запуск release workflow прошёл без предупреждения о секретах; агент сверил отпечаток APK с указанным выше и закрыл 163.

## Анализ

## План
1. GitHub → репозиторий → Settings → Secrets and variables → Actions → New repository secret: четыре секрета, имя — строка над значением в `github-secrets.txt`, значение — одна строка целиком.
2. Keystore и пароль — в менеджер паролей; удалить оба файла из загрузок.
3. Actions → release → Run workflow (ветка `main`).
4. Сообщить агенту номер запуска: он скачает APK из артефактов, проверит `apksigner verify --print-certs` и закроет 163.

## Отчёт

## Комментарии

### 2026-10-31, владелец (в чате, записал claude-audit)
Секреты владелец создаёт сам, отдельной задачей; до этого сборки должны работать без сертификата.
