# 运行与验证手册

从[文档导读](README.md)选择任务；本页命令在仓库根目录执行。测试结果统一记录在[验收文档](acceptance.md)。

## Run with Docker / 中文一键启动

Prerequisites: Docker with Compose, Bash, curl and OpenSSL. No local .NET or Node installation is required for this path.

```sh
git clone git@github.com:sjjjlol/ClinicFlow.git
cd ClinicFlow
./scripts/start.sh
```

Open **http://localhost:5080**. The first build downloads pinned images and can take several minutes. The script generates a local, ignored `.env`, starts MySQL and the mock receiver, applies EF migrations, seeds fictional catalog data, and serves the built React UI from ASP.NET Core.

Use **注册预约账号** on the login page to create a personal account. Registered users see **我的预约**, can book only for themselves, and can reschedule/cancel before the appointment starts. Staff verify prerequisites and confirm appointments; after the scheduled end, Scheduler records **Completed**. See [account and lifecycle guide](accounts-and-lifecycle.md).

Demo usernames: `scheduler`, `taskoperator`, `admin`. Read **DEMO_PASSWORD** from your local `.env` and use it for all three accounts. Passwords are hashed on first seed; editing `.env` does not rotate existing database password hashes. Demo roles have different permissions; Admin does not inherit scheduling/task privileges.

停止：`docker compose --profile full down`（保留数据）。重置：`docker compose --profile full down -v`（删除本项目数据库、外部去重和会话密钥卷；仅用于清除虚构演示数据）。不要在本地API占用5080时启动容器API。

Ports are loopback-only: UI/API 5080, MySQL 3308, receiver 5090. Override APP_PORT/DB_PORT/MOCK_PORT for an isolated second Compose project. Compose intentionally uses Development cookies on localhost HTTP; public hosting would require HTTPS, production configuration and operational work outside this demo.

## Local debugging

Pinned baseline: .NET SDK **10.0.401**, ASP.NET runtime/OpenAPI **10.0.12**, EF Core **9.0.20**, Pomelo **9.0.0**, MySQL **8.4.8**, Node **24.13.1**, React **19.2.0**, TypeScript **5.9.3**, Vite **7.3.6**. Lockfiles are committed. [Version rationale](adr/001-platform.md).

```sh
./scripts/configure.sh
docker compose up -d --build --wait  # infrastructure only; app is in the full profile
npm --prefix agent-runtime ci
./scripts/dotnet.sh tool restore
./scripts/api.sh
# another terminal
cd frontend && npm ci && npm run dev
```

Open http://127.0.0.1:5173. Vite proxies `/api` and `/fhir` to the local API. `scripts/dotnet.sh` uses a global SDK if installed, otherwise `~/.local/share/clinicflow-dotnet/dotnet`. Install the pinned SDK through Microsoft's .NET download if neither exists. Use one local API process; restart it after backend edits.

## Verification

```sh
npm --prefix agent-runtime ci && npm --prefix agent-runtime test
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

CI independently starts the container from fresh volumes, restores locked dependencies, builds, runs MySQL/HTTP/receiver/browser tests, exercises labs and upgrades, then publishes a portable ASP.NET + React + Pi runtime artifact (requires Node 24.13.1 on the target host). Dockerfile provides repeatable container packaging; there is no automatic public deployment. [Acceptance evidence](acceptance.md) and [development history](acceptance.md#history) distinguish actual execution from pending checks.


## 可选扩展与排障

预约助手配置见[Agent 指南](appointment-agent.md)；影像启动、权限、查看器与停机排障见[影像手册](imaging/README.md#operations)。Docker 原理另查[Docker 教程](docker-crash-course.md)。

| 问题 | 处理入口 |
|---|---|
| 5080 被占用 | 选择本机 API 或容器 app，避免同时绑定 |
| 修改 .env 后密码未变化 | 演示密码只在首次种子时散列；修改变量不等于轮换数据库密码 |
| 登录 429 | 按 Retry-After 等待限流窗口；不要反复重试 |
| 401 / CSRF 错误 | 重新登录并刷新令牌；见 API 契约 |
| 新功能不显示 | 确认访问端口、重新构建 app、检查实际容器版本 |
| 外部同步失败 | Admin 查看 attempts；区分可重试故障与永久失败，达到上限后人工重试 |

HTTP 和浏览器测试会写入其目标 API 的数据库，优先使用隔离实例；后端 test.sh 使用 clinicflow_tests。停机和升级实验的副作用见对应脚本与影像手册。
