# Запускает «Островок»: при необходимости пересобирает проект и стартует виджет.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'src\Island\Island.csproj'
$exe = Join-Path $root 'src\Island\bin\Debug\net10.0-windows10.0.19041.0\Island.exe'

$running = Get-Process Island -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

$needsBuild = -not (Test-Path $exe)
if (-not $needsBuild) {
    $exeTime = (Get-Item $exe).LastWriteTimeUtc
    $newer = Get-ChildItem (Join-Path $root 'src\Island') -Recurse -File -Include *.cs, *.xaml |
        Where-Object { $_.LastWriteTimeUtc -gt $exeTime } |
        Select-Object -First 1
    $needsBuild = $null -ne $newer
}

if ($needsBuild) {
    Write-Host 'Собираю островок...'
    dotnet build $project -v q -nologo
    if ($LASTEXITCODE -ne 0) { throw 'Сборка завершилась с ошибкой.' }
}

Start-Process -FilePath $exe
Write-Host 'Островок запущен вверху по центру экрана.'
