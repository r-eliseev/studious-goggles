---
id: 165
status: done
assignee:
claimed:
created: 2026-10-31
---

# Добавить секреты подписи Android в GitHub

## Описание
Ручная задача владельца (решение 2026-10-31). Агент её не выполняет: прокси облачной сессии запрещает запись секретов GitHub (`403` на `/repos/…/actions/secrets`).

Владелец получил в чате 2026-10-31 файлы `github-secrets.txt` и `imagegen-release.keystore` (ключ из задачи 163, отпечаток SHA-256 `ecde686bc9970c00bbc74a32c8db5b99a3d1ccccecbffc8992a7a8d7942eb8cd`). До выполнения этой задачи `release.yml` подписывает APK отладочным ключом, как раньше.

Зависит от: — (код `release.yml` из 163 уже в main; 163 ждёт эту задачу, а не наоборот).
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

Секреты добавил владелец 2026-11-01 в 18:53 UTC: `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD` (по `gh secret list` видны только имена).

Проверено claude-audit 2026-11-01:
- Ручной запуск release workflow `41339041274` (18:55 UTC, `main`) успешен. Шаг «Restore signing key» восстановил keystore, сборка APK получила `AndroidKeyStore=true`, предупреждения об отсутствии секретов нет.
- `apksigner verify --print-certs -v` на артефакте `ImageGen-Maui-dev-14-android.apk`: подпись верна (v1, v2, v3), один подписант, `CN=ImageGen, O=eliseev`, SHA-256 `6711f8badc39c087512e78a04ab4d7a8313d9d00b33081bf139867fc55b10277`.

Отличие от критериев: отпечаток не совпал с ключом `ecde686b…eb8cd` из описания. Владелец положил в секреты новый ключ и 2026-11-01 принял его (комментарий ниже). Новый отпечаток записан в `docs/knowledge/dev-environment.md` и `README.md`.

Не проверено агентом: сохранены ли keystore и пароль в менеджере паролей (это видит только владелец).

## Комментарии

### 2026-10-31, владелец (в чате, записал claude-audit)
Секреты владелец создаёт сам, отдельной задачей; до этого сборки должны работать без сертификата.

### 2026-10-31, claude-audit
Исправлена зависимость: было «Зависит от: 163», а 163 заблокирована до этой задачи — получался цикл. Код подписи уже в main (PR #58), поэтому задача не ждёт ничего.

### 2026-11-01, владелец (в чате, записал claude-audit)
Новый ключ (`CN=ImageGen, O=eliseev`, SHA-256 `6711f8ba…10277`) создан намеренно и принят вместо `ecde686b…eb8cd`.
