# Release State — Island

Обновлено: 2026-11-02

## Статус: READY (conditional)

Код, сборка, артефакты, лицензия и документация готовы. Публикация возможна после ручных проверок
из списка ниже. Push, тег и GitHub Release **не выполнялись**.

## Готово
- Версия 0.1.0; сборка 0 ошибок/0 предупреждений; тесты 49/49.
- Уязвимых зависимостей нет (NAudio 2.2.1, MIT).
- Артефакты собраны, SHA-256 пересчитаны и совпадают с TEST_REPORT.
- LICENSE: MIT, Copyright (c) 2026 Kain Raine.
- README (EN) ссылается на LICENSE; docs/INSTALLATION объясняет framework-dependent vs self-contained.
- docs/: USER_GUIDE, INSTALLATION, TROUBLESHOOTING, DEVELOPMENT, ARCHITECTURE, FAQ, PRIVACY, README.ru.
- Git: ветка main, 2 коммита, рабочее дерево чистое, remote/tags отсутствуют.

## Требует подтверждения владельца
1. **Правообладатель в LICENSE.** Указан псевдоним проекта «Kain Raine» и год 2026. Если юридическое имя
   или организация иные — заменить строку Copyright до публикации.
2. **Подпись артефактов** не выполнялась. Решение: подписывать или публиковать неподписанными
   (SmartScreen будет предупреждать).

## Ручные проверки перед публикацией
- [ ] Подтвердить владельца в LICENSE.
- [ ] Запустить self-contained сборку на чистой машине без .NET.
- [ ] Запустить framework-dependent сборку на машине с .NET 10 Desktop Runtime.
- [ ] Проверить системные тосты в Action Center (вне песочницы).
- [ ] Проверить DPI 125/150% и второй монитор.
- [ ] При желании — подписать артефакты и обновить SHA-256.

## Команды публикации (выполнять вручную, после проверок)

Заменить OWNER/REPO на реальные. Remote ещё не добавлен.

~~~powershell
cd island

# 1. Первый push (ветка уже main)
git remote add origin https://github.com/OWNER/REPO.git
git push -u origin main

# 2. Тег версии
git tag -a v0.1.0 -m "Island 0.1.0"
git push origin v0.1.0

# 3. GitHub Release (нужен авторизованный gh)
gh release create v0.1.0 --title "Island 0.1.0" --notes-file RELEASE_NOTES_DRAFT.md `

  "release/Island-0.1.0-win-x64-framework.zip#Framework-dependent (requires .NET 10 Desktop Runtime)" `

  "release/Island-0.1.0-win-x64-selfcontained.zip#Self-contained (no runtime required)"
~~~

Если gh недоступен — создать релиз вручную на GitHub, приложив оба zip и вставив RELEASE_NOTES_DRAFT.md.

## Не выполнялось
- git push, git tag, gh release create — требуют отдельного разрешения.
- Изменение истории коммитов — не выполнялось.
