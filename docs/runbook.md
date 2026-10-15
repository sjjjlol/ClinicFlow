# 运行与验证手册

在仓库根目录执行命令。业务操作见 [OIS 手册](ois-runbook.md)。已有数据库升级前先备份，删除范围及恢复说明见[数据库设计](database-design.md)。

## Docker

```sh
./scripts/start.sh
```

需要 Docker Compose、Bash、curl、OpenSSL。脚本保留已有 `.env` 或生成随机密码，启动 MySQL 和应用并自动迁移。访问 http://localhost:5080。

三个账号：`scheduler`、`clinician`、`physicist`；密码见 `.env` 的 `DEMO_PASSWORD`。修改环境变量不会重置已有账号密码。无患者注册入口。

停止且保留数据：`docker compose --profile full down`。日志：`docker compose logs --tail 100 app`。默认 DB_PORT=3308、APP_PORT=5080，可在 `.env` 配置。另起实例须隔离 Compose 项目名和端口。

## 本机开发

需要 global.json 指定的 .NET SDK 和 Node（CI 使用 24.13.1）。

```sh
./scripts/configure.sh
docker compose up -d --wait
./scripts/api.sh
```

另一个终端：

```sh
npm --prefix frontend ci
npm --prefix frontend run dev
```

浏览器访问 http://localhost:5173。Vite 默认代理 5080；自定义 API 端口时也需调整 Vite 代理。不要同时启动占用相同端口的容器 app 和本机 API。

## 验证

```sh
# 仅规则，无需数据库
./scripts/dotnet.sh test tests --filter FullyQualifiedName~OisRulesTests
# 需要 db，使用隔离的 clinicflow_tests 库
./scripts/test.sh
npm --prefix frontend run build
npm --prefix frontend run format:check
# 需要 API，会在目标应用数据库写入演示数据
./scripts/http-tests.sh
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

## 排障

- 登录 429：遵守 Retry-After；CSRF 错误：刷新令牌或重新登录。
- 403：核对三个工作人员角色及操作责任角色。
- 治疗受阻：核对预约时段、身份、当前计划审核/验证、处方版本和机器状态。
- 升级失败：停止应用写入，检查迁移日志与备份，不要通过清空应用数据库绕过问题。
