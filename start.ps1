$ErrorActionPreference = "Stop"

# Check prerequisites
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet is not installed. Get it at https://dotnet.microsoft.com/download"
    exit 1
}
if (-not (Get-Command bun -ErrorAction SilentlyContinue)) {
    Write-Error "bun is not installed. Get it at https://bun.sh/"
    exit 1
}

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Installing dependencies..."
Push-Location "$Root/backend"
dotnet restore --verbosity quiet
Pop-Location

Push-Location "$Root/frontend"
bun install --silent
Pop-Location

Write-Host "Starting backend on port 5032..."
$backend = Start-Process -NoNewWindow -PassThru -FilePath dotnet `
    -ArgumentList "run","--project","$Root/backend/src/SoundCloudDigger.Api","--no-restore"

Write-Host "Starting frontend on port 5173..."
$frontend = Start-Process -NoNewWindow -PassThru -FilePath bun `
    -ArgumentList "run","dev","--open","http://scdigger.localhost:5173" `
    -WorkingDirectory "$Root/frontend"

Write-Host ""
Write-Host "Open http://scdigger.localhost:5173 in your browser."
Write-Host "Press Ctrl+C to stop."

try {
    Wait-Process -Id $backend.Id, $frontend.Id
} finally {
    if (-not $backend.HasExited) { Stop-Process -Id $backend.Id -Force -ErrorAction SilentlyContinue }
    if (-not $frontend.HasExited) { Stop-Process -Id $frontend.Id -Force -ErrorAction SilentlyContinue }
}
