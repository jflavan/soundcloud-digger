$ErrorActionPreference = "Stop"

# Check prerequisites
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "dotnet is not installed. Get it at https://dotnet.microsoft.com/download"
    exit 1
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Error "node is not installed. Get it at https://nodejs.org/"
    exit 1
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    Write-Error "npm is not installed. Get it at https://nodejs.org/"
    exit 1
}

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Installing dependencies..."
Push-Location "$Root/backend"
dotnet restore --verbosity quiet
Pop-Location

Push-Location "$Root/frontend"
npm install --silent
Pop-Location

Write-Host "Starting backend on port 5032..."
$backend = Start-Process -NoNewWindow -PassThru -FilePath dotnet `
    -ArgumentList "run","--project","$Root/backend/src/SoundCloudDigger.Api","--no-restore"

Write-Host "Starting frontend on port 5173..."
$frontend = Start-Process -NoNewWindow -PassThru -FilePath npm `
    -ArgumentList "run","dev","--","--open" `
    -WorkingDirectory "$Root/frontend"

Write-Host ""
Write-Host "Open http://localhost:5173 in your browser."
Write-Host "Press Ctrl+C to stop."

try {
    Wait-Process -Id $backend.Id, $frontend.Id
} finally {
    if (-not $backend.HasExited) { Stop-Process -Id $backend.Id -Force -ErrorAction SilentlyContinue }
    if (-not $frontend.HasExited) { Stop-Process -Id $frontend.Id -Force -ErrorAction SilentlyContinue }
}
