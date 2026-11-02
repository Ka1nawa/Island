# Contributing

## Требования

- Windows 10 2004+ / Windows 11
- .NET SDK 10.0

## Сборка и тесты

~~~powershell
dotnet build src/Island/Island.csproj
dotnet run --project tests/ParserTests/ParserTests.csproj
~~~

## Правила

- Не добавляйте секреты, локальные пути и пользовательские данные.
- Не меняйте зависимости без необходимости; фиксируйте версии.
- Для исправлений поведения добавляйте тест в tests/ParserTests, если логика чистая.
- Скриншоты для README — только с нейтральным фоном (без личных окон и вкладок).
