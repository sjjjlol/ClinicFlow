# ClinicFlow

A learning project for transactional scheduling with ASP.NET Core, React/TypeScript and MySQL. All patients and resources are fictional. This is not clinical software or an Elekta product.

## Local development / 中文快速开始

Prerequisites: .NET SDK 10.0.401, Node 24.13.1, Docker Compose.

```sh
./scripts/configure.sh
docker compose up -d --build --wait
./scripts/api.sh
# another terminal
cd frontend && npm ci && npm run dev
```

Open http://localhost:5173. Database migrations run on API startup. Stop infrastructure with `docker compose down`; `docker compose down -v` deletes this project's database (only use to reset fictional data).

Implementation progress: [development log](docs/development-progress.md). Requirements: [SPEC](SPEC.md).
