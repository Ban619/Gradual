$projectDir = $PSScriptRoot
$exePath = Join-Path $projectDir "bin\Debug\net8.0-windows\FronTimePOS.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found: $exePath"
    exit 1
}

Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath) -WindowStyle Normal
