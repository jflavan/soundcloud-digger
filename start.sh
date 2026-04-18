#!/usr/bin/env bash
set -euo pipefail

# Check prerequisites
command -v dotnet >/dev/null 2>&1 || { echo "Error: dotnet is not installed. Get it at https://dotnet.microsoft.com/download"; exit 1; }
command -v node >/dev/null 2>&1 || { echo "Error: node is not installed. Get it at https://nodejs.org/"; exit 1; }
command -v npm >/dev/null 2>&1 || { echo "Error: npm is not installed. Get it at https://nodejs.org/"; exit 1; }

ROOT="$(cd "$(dirname "$0")" && pwd)"

echo "Installing dependencies..."
(cd "$ROOT/backend" && dotnet restore --verbosity quiet)
(cd "$ROOT/frontend" && npm install --silent)

trap 'kill 0' EXIT

echo "Starting backend on port 5032..."
(cd "$ROOT/backend" && dotnet run --project src/SoundCloudDigger.Api --no-restore) &

echo "Starting frontend on port 5173..."
(cd "$ROOT/frontend" && npm run dev -- --open http://scdigger.localhost:5173) &

echo ""
echo "Open http://scdigger.localhost:5173 in your browser."
echo "Press Ctrl+C to stop."

wait
