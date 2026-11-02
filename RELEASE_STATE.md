# Release State — Island 0.1.2

Обновлено: 2026-11-02

## Статус: READY (conditional)

Код, сборка, тесты, лицензия и документация готовы. Push, тег и GitHub Release **не выполнялись**.

## Версия и артефакты
- Версия 0.1.2 (csproj, FileVersion 0.1.2.0).
- Island-framework-0.1.2.exe — 990a912292ac44fc1c722a9cc2f014d753f6ae22c814e69c2d9257c7f41197c0
- Island-selfcontained-0.1.2.exe — b166fe2879216223d3a867343a6306485948e48af8d4657db174aca506aaae16
- Архивов нет: публикуются два exe.

## GitHub
- Репозиторий: https://github.com/Ka1nawa/Island (public, MIT).
- Remote origin добавлен локально.
- На GitHub: ветка main на e96fd2f, тег и релиз v0.1.0 (старые zip).
- Локально впереди 5 коммитов (исправления 0.1.2) — push не выполнялся.

## Осталось сделать вручную
1. Проверить self-contained на чистой машине без .NET.
2. Проверить framework-dependent с .NET 10 Desktop Runtime.
3. Проверить тосты в Action Center, DPI 125/150%.
4. При желании — подписать exe.

## Команды публикации

~~~powershell
cd C:\Users\paran\Documents\deepseek-harness\default-workspace\island
git remote add origin https://github.com/Ka1nawa/Island.git   # уже добавлен локально
git push origin main

git tag -a v0.1.2 -m "Island 0.1.2"
git push origin v0.1.2

gh release create v0.1.2 --title "Island 0.1.2" --notes-file RELEASE_NOTES_DRAFT.md `
  "release/Island-framework-0.1.2.exe#Framework-dependent (requires .NET 10 Desktop Runtime)" `
  "release/Island-selfcontained-0.1.2.exe#Self-contained (no runtime required)"
~~~
