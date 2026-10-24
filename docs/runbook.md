# 运行与验证手册

在仓库根目录执行命令。业务操作见 [OIS 手册](ois-runbook.md)。已有数据库升级前先备份并停止旧版写入，数据搬移、历史删除范围及恢复说明见[数据库设计](database-design.md)。

## Docker

```sh
./scripts/start.sh
```

需要 Docker Compose、Bash、curl、OpenSSL。脚本保留已有 `.env` 或生成随机密码，启动 MySQL 和应用并自动迁移。访问 http://localhost:5080。

三个账号：`scheduler`、`clinician`、`physicist`；密码见 `.env` 的 `DEMO_PASSWORD`。初次创建账号要求 DEMO_PASSWORD 至少 8 个字符；configure.sh 保留已有 .env，不会替你修复短密码。修改环境变量不会重置已有账号密码。无患者注册入口。

停止且保留数据：`docker compose --profile full down`。日志：`docker compose logs --tail 100 app`。默认 DB_PORT=3308、APP_PORT=5080，可在 `.env` 配置。另起实例须隔离 Compose 项目名和端口。

## 本机开发

需要 global.json 指定的 .NET SDK 和 Node（CI 使用 24.13.1）。

```sh
./scripts/configure.sh
docker compose up -d --wait
./scripts/api.sh
```

需要 EF CLI 等本地工具时，在仓库根目录运行：

```sh
./scripts/dotnet.sh tool restore
./scripts/dotnet.sh tool list --local
```

工具版本由根目录 `dotnet-tools.json` 固定，SDK 由 `global.json` 固定。首次还原依赖后才能使用 `--no-restore`；`scripts/dotnet.sh` 选择已安装的 SDK，不负责安装它。

另一个终端：

```sh
npm --prefix frontend ci
npm --prefix frontend run dev
```

浏览器访问 http://localhost:5173。Vite 默认代理 5080；自定义 API 端口时也需调整 Vite 代理。不要同时启动占用相同端口的容器 app 和本机 API。

## 验证

先按目标选择环境，HTTP 与浏览器测试不会自动切换到隔离数据库。

| 验证 | 目标与副作用 |
| --- | --- |
| OisRulesTests、ModuleBoundariesTests | 内存规则与结构检查，无需数据库 |
| `scripts/test.sh` | 使用 clinicflow_tests，数据库测试会删除并重建该库；不要并行启动多个测试进程操作同一库 |
| `scripts/http-tests.sh` | 访问 API_URL 或 APP_PORT 指向的运行实例，写入该实例连接的数据库 |
| Playwright | 访问 BASE_URL 或默认 5173，经过前端连接后端并写入业务数据 |
| `scripts/upgrade-drill.sh` | 重建 clinicflow_upgrade_tests，生成并恢复 artifacts/upgrade-before.sql，不操作应用库 |

```sh
# 仅规则，无需数据库
./scripts/dotnet.sh test tests --filter FullyQualifiedName~OisRulesTests
# 模块边界检查，无需数据库
./scripts/dotnet.sh test tests --filter FullyQualifiedName~ModuleBoundariesTests
# 需要 db，使用隔离的 clinicflow_tests 库；包含验证记录迁移往返与跨模块事务测试
./scripts/test.sh
npm --prefix frontend run build
npm --prefix frontend run format:check
# 需要 API：认证、OIS 与 Controller 回归，会在目标应用数据库写入演示数据
./scripts/http-tests.sh
# 可选：另验证登录 429 / Retry-After；会耗尽当前 IP 登录配额，后续登录需等待窗口恢复
VERIFY_LOGIN_RATE_LIMIT=1 ./scripts/http-tests.sh
# 仅针对打包应用/容器，验证首页、资源与已移除路径
node tests/http-ui.mjs
# 隔离库的迁移与备份恢复演练
./scripts/upgrade-drill.sh
```

浏览器测试需 API 与 Vite，或用 BASE_URL 指向容器应用：

```sh
set -a
source .env
set +a
(cd frontend && npx playwright install chromium)
npm --prefix frontend run test:e2e
```

可设置 `PW_CHANNEL=chrome` 使用已安装 Chrome。HTTP 默认访问 APP_PORT，可由 API_URL 覆盖；Playwright 默认 5173，可由 BASE_URL 覆盖。测试请使用隔离演示环境。

CI 执行构建、OIS 规则/数据库/API/浏览器测试、迁移恢复及容器冒烟，并生成便携交付包。没有自动公网部署。

## 升级核对

已有实例先查看已应用迁移，再按[数据库设计](database-design.md)判断需要执行哪些步骤。启动会自动应用未执行的迁移；本次 SeparatePhysicalValidation 会搬移验证字段，更早版本还可能执行清理旧模块的迁移。升级后先确认 `/api/health` 响应中的 `status` 为 `ready`，再检查工作台的历史验证记录。当前健康接口在 `unavailable` 时也返回 HTTP 200，`curl -f` 单独不足以证明数据库就绪。

本次模块重构保持现有资源 API 与 JSON 契约。若从旧 `/api/ois/commands/*` 客户端升级，仍需更新客户端路径；不要将两次升级混为一谈。

## 排障

- 初次启动提示 Demo password requires 8+ characters：检查演示配置；若账号已存在但登录失败，确认使用的是原密码，修改 DEMO_PASSWORD 不会重置已有账号。
- 登录 429：遵守 Retry-After；CSRF 错误：刷新令牌或重新登录。
- 403：核对三个工作人员角色及操作责任角色。
- 治疗受阻：核对预约时段、身份、当前计划审核/验证、处方版本和机器状态。
- 升级失败：停止应用写入，检查迁移日志与备份，不要通过清空应用数据库绕过问题。

## 分层后的工程入口

启动/发布使用 `backend/ClinicFlow.Api`；EF CLI 使用 `backend/ClinicFlow.Infrastructure`。scripts/api.sh、Dockerfile、CI 和升级演练已使用这些工程路径。运行时需包含四层 DLL，使用 dotnet publish 的完整输出。

默认集成事件只写 Outbox，未配置消息传输或工作者；如需接入请按[事件文档](events.md)注册 publisher 并安排投递任务。待发量会随业务增长，不能把写入 Outbox 当成外部已确认。

CI 的两个任务固定使用 `ubuntu-24.04`。当前锁定的 Playwright 1.56.1 不支持 Ubuntu 26.04，不能直接随 `ubuntu-latest` 升级运行器；调整系统版本时应同步验证浏览器依赖安装及完整 E2E。
