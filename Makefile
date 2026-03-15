.PHONY: dev setup test

dev:
	./start.sh

setup:
	cd backend && dotnet restore
	cd frontend && npm install

test:
	cd backend && dotnet test
	cd frontend && npx vitest run
