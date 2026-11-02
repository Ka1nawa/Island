# Release Checklist — Island 0.1.0

## Репозиторий
- [x] Git-репозиторий инициализирован в island/ (посторонний winops не включён)
- [x] .gitignore исключает bin/obj/publish/_backup/*.zip/settings.json/логи
- [x] Нет секретов, токенов, приватных URL и абсолютных путей пользователя в исходниках
- [x] Скриншоты обрезаны до окна приложения (фон рабочего стола и браузер не видны)
- [ ] LICENSE — отсутствует, требуется решение владельца (рекомендуется MIT)
- [ ] Начальный коммит и тег v0.1.0 — тег не создаётся без разрешения

## Сборка
- [x] dotnet build src/Island/Island.csproj — 0 ошибок, 0 предупреждений
- [x] dotnet run --project tests/ParserTests — 44/44 PASS
- [x] dotnet list package --vulnerable --include-transitive — уязвимых пакетов нет
- [x] Версия 0.1.0 в csproj
- [x] Release-сборка: framework-dependent и self-contained, SHA-256 в TEST_REPORT
- [x] Целевые тесты обновлятора (traversal, подозрительные имена, пустое имя) — 49/49 PASS

## Приложение
- [x] Холодный запуск, раскрытие/сворачивание
- [x] Сохранение настроек после перезапуска (атомарная запись)
- [x] Панель задач/автоскрытие, второй монитор
- [x] Живое стекло и fallback
- [x] Напоминания, таймер, тосты
- [x] Календарь ICS, погода, заметки, музыка, темы
- [ ] Windows-тосты в Action Center — не проверены вне песочницы
- [ ] DPI 125/150% — нет монитора с другим масштабом

## Документация
- [x] README соответствует функциям
- [x] CHANGELOG.md
- [x] SECURITY.md, CONTRIBUTING.md
- [x] RELEASE_NOTES_DRAFT.md
- [x] SECURITY_REVIEW.md, RELEASE_STATE.md
- [x] CI workflow (.github/workflows/ci.yml)
- [x] docs/: README, USER_GUIDE, INSTALLATION, TROUBLESHOOTING, DEVELOPMENT, ARCHITECTURE, FAQ, PRIVACY, README.ru
- [x] Ссылки на изображения проверены (0 битых), секретов и личных путей нет
- [x] README на английском, русское краткое описание сохранено (docs/README.ru.md)

## Публикация
- [ ] Push в GitHub — требуется разрешение
- [ ] GitHub Release — требуется разрешение
- [ ] Подпись артефактов — не выполнялась
