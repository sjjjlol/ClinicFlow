# ClinicFlow

A runnable scheduling study project for a Java developer learning C#/.NET. Built with AI assistance, with deterministic failure exercises and a separate requirement for the learner to implement. All patients and resources are fictional. This is not clinical software, an Elekta product, or a certified FHIR implementation.

The Chinese UI uses an Apple-inspired visual style: quiet surfaces, clear typography, rounded panels and restrained blue accents.

## Run with Docker / 中文一键启动

Prerequisites: Docker with Compose, Bash, curl and OpenSSL. No local .NET or Node installation is required for this path.

```sh
git clone git@github.com:sjjjlol/ClinicFlow.git
cd ClinicFlow
./scripts/start.sh
```

Open **http://localhost:5080**. The first build downloads pinned images and can take several minutes. The script generates a local, ignored `.env`, starts MySQL and the mock receiver, applies EF migrations, seeds fictional catalog data, and serves the built React UI from ASP.NET Core.

Demo usernames: `scheduler`, `taskoperator`, `admin`. Read **DEMO_PASSWORD** from your local `.env` and use it for all three accounts. Passwords are hashed on first seed; editing `.env` does not rotate existing database password hashes. Demo roles have different permissions; Admin does not inherit scheduling/task privileges.

停止：`docker compose --profile full down`（保留数据）。重置：`docker compose --profile full down -v`（删除本项目数据库、外部去重和会话密钥卷；仅用于清除虚构演示数据）。不要在本地API占用5080时启动容器API。

Ports are loopback-only: UI/API 5080, MySQL 3308, receiver 5090. Override APP_PORT/DB_PORT/MOCK_PORT for an isolated second Compose project. Compose intentionally uses Development cookies on localhost HTTP; public hosting would require HTTPS, production configuration and operational work outside this demo.

## Local debugging

Pinned baseline: .NET SDK **10.0.401**, ASP.NET runtime/OpenAPI **10.0.12**, EF Core **9.0.20**, Pomelo **9.0.0**, MySQL **8.4.8**, Node **24.13.1**, React **19.2.0**, TypeScript **5.9.3**, Vite **7.3.6**. Lockfiles are committed. [Version rationale](docs/adr/001-platform.md).

```sh
./scripts/configure.sh
docker compose up -d --build --wait  # infrastructure only; app is in the full profile
./scripts/dotnet.sh tool restore
./scripts/api.sh
# another terminal
cd frontend && npm ci && npm run dev
```

Open http://127.0.0.1:5173. Vite proxies `/api` and `/fhir` to the local API. `scripts/dotnet.sh` uses a global SDK if installed, otherwise `~/.local/share/clinicflow-dotnet/dotnet`. Install the pinned SDK through Microsoft's .NET download if neither exists. Use one local API process; restart it after backend edits.

## Workflow and guarantees

Scheduler creates a Pending appointment and immediately reserves consecutive 15-minute slots. TaskOperator completes two prerequisites; Scheduler confirms, reschedules or cancels. Reschedule resets tasks. Slot uniqueness, stable resource lock ordering, version checks, idempotent commands, audit and Outbox are covered by real MySQL tests.

The background worker uses short claim/completion transactions and HTTP outside transactions. A persistent receiver deduplicates MessageId and rejects older snapshot versions. Admin can inspect attempts and retry failed messages. This is at-least-once delivery with receiver deduplication.

## Verification

```sh
./scripts/test.sh                              # isolated clinicflow_tests database
./scripts/http-tests.sh                        # running local API; loads .env itself
node --test mock-external/receiver.test.mjs     # isolated receiver restart/lost-response test
./scripts/upgrade-drill.sh                     # isolated upgrade + actual backup restoration
./scripts/labs.sh L1                           # explicit faulty lab
./scripts/labs.sh L2
./scripts/labs.sh L3 100000
```

Browser tests require Node plus a running UI/API. Export `.env` locally without printing secrets:

```sh
set -a; source .env; set +a
cd frontend
npx playwright install chromium
npm run test:e2e
# optional: PW_CHANNEL=chrome npm run test:e2e (installed Chrome)
# BASE_URL=http://127.0.0.1:5080 tests the bundled container UI
```

With `.env` exported, `node tests/http-integration.mjs` briefly stops/restarts the mock container and verifies real outage recovery. `docker compose stop mock` / `docker compose start mock` also demonstrate it manually. After five failures, Admin must retry. Fault-control HTTP endpoints are disabled in normal Compose.

CI restores locked dependencies, builds, runs MySQL/HTTP/receiver/browser tests, exercises labs and upgrades, then publishes a portable ASP.NET + React artifact. Dockerfile provides repeatable container packaging; there is no automatic public deployment. [Acceptance evidence](docs/acceptance.md) and [development history](docs/development-progress.md) distinguish actual execution from pending checks.

## Reading route / 学习入口

- [Java开发者七天阅读路线](docs/java-reading-guide.md)
- [架构、ER与事务时序](docs/architecture.md) · [API与错误码](docs/api.md)
- [ADR目录](docs/adr/) · [FHIR R4范围](docs/fhir-r4.md)
- [隔离故障实验](labs/README.md) · [RCA与英文缺陷更新](docs/rca.md)
- [升级与备份恢复记录](docs/upgrade-checklist.md)
- [演示脚本与面试材料](docs/demo-and-interview.md)
- [留给用户独立实现的Completed需求](docs/independent-task.md)
- [原始规格](SPEC.md)

After login, generated OpenAPI is available at `/api/openapi/v1.json`.

Limits: no real hospital integration, dosage calculation, treatment planning, device control, registration/SSO, message broker or full FHIR conformance. No automatic retention/cleanup for audit, idempotency or receipts. The mock's built-in Node SQLite API is experimental in the pinned runtime. Lab timings are reproducible laptop observations, not production gains. Database startup migrations and seeded accounts are for single-instance local demonstration.
