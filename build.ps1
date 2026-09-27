# Сборка WoW-3.3.5a-DLSS5.exe: один файл, рантайм .NET и все файлы DLSS-стека внутри.
#   1. dotnet publish — single-file exe;
#   2. vendor\ упаковывается в zip;
#   3. zip дописывается в конец exe, за ним 16 байт: смещение zip (Int64) + "WDLSS5P1" (см. Payload.cs).

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $build, $dist | Out-Null

Write-Host '[1/3] dotnet publish' -ForegroundColor Cyan
# Каждый раз в новую папку: промежуточный exe могут держать открытым, и перезапись падает.
$publish = Join-Path $build ('publish-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
& dotnet publish (Join-Path $root 'src\WowDlss5\WowDlss5.csproj') -c Release -o $publish --nologo -v:minimal
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Write-Host '[2/3] payload.zip из vendor\' -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $build 'payload.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'vendor'), $zip, [IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host '[3/3] WoW-3.3.5a-DLSS5.exe = exe + payload' -ForegroundColor Cyan
$exe = Join-Path $publish 'WoW-3.3.5a-DLSS5.exe'
$out = Join-Path $dist 'WoW-3.3.5a-DLSS5.exe'
$fs = [IO.File]::Create($out)
try {
    foreach ($part in $exe, $zip) {
        $in = [IO.File]::OpenRead($part)
        if ($part -eq $zip) { $offset = $fs.Position }
        try { $in.CopyTo($fs) } finally { $in.Dispose() }
    }
    $fs.Write([BitConverter]::GetBytes([long]$offset), 0, 8)
    $magic = [Text.Encoding]::ASCII.GetBytes('WDLSS5P1')
    $fs.Write($magic, 0, 8)
}
finally { $fs.Dispose() }

Get-ChildItem $build -Directory -Filter 'publish*' | Where-Object { $_.FullName -ne $publish } |
    ForEach-Object { try { Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop } catch { } }

$mb = (Get-Item $out).Length / 1MB
Write-Host ('Готово: {0} ({1:N0} МБ)' -f $out, $mb) -ForegroundColor Green
