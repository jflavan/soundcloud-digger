.PHONY: dev setup test

dev:
	./start.sh

setup:
	cd backend && dotnet restore
	cd frontend && bun install

test:
	cd backend && dotnet test
	cd frontend && bun run check
	cd frontend && bun run test
