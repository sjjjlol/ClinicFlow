# Java 开发者 Docker 速成手册（含 ClinicFlow 项目解析）

> 面向有 Java 后端经验的开发者。
> 第 1–4 章讲 Docker 核心概念（用 Java 世界类比）；第 5 章逐行解析本项目的 Dockerfile 与 compose.yaml；第 6 章给出日常开发的 Docker 编写模板与最佳实践。
> 配套阅读：`docs/csharp-dotnet-crash-course.md`、`docs/adr/001-platform.md`。

---

## 目录

1. [为什么需要 Docker：一句话心智模型](#1-为什么需要-docker)
2. [核心概念对照 Java 世界](#2-核心概念)
3. [Dockerfile：镜像的构建配方](#3-dockerfile-详解)
4. [Docker Compose：多服务编排](#4-docker-compose-详解)
5. [ClinicFlow 项目 Docker 全解析](#5-clinicflow-项目-docker-全解析)
6. [日常开发 Docker 编写模板与最佳实践](#6-编写模板与最佳实践)
7. [命令速查表](#7-命令速查表)

---

## 1. 为什么需要 Docker

Java 开发者熟悉的痛点：

- "我机器上能跑" —— JDK 版本、系统依赖、环境变量不一致。
- 装 MySQL / Redis 要配半天，换台机器重来。
- 生产环境是 Linux，开发机是 macOS，行为有差异。

Docker 的解法：**把应用 + 运行时 + 系统依赖打成一个不可变的镜像（Image），任何地方用同一份镜像启动进程（容器 Container），行为完全一致**。

用 Java 类比：你本来只交付一个 `app.jar`，要求目标机装好"正确版本"的 JRE；Docker 相当于把 `app.jar` + JRE + 操作系统用户态一起冻结成一个快照交付——目标机只需要 Docker 引擎。

---

## 2. 核心概念

| Docker 概念 | 一句话 | Java 世界类比 |
|---|---|---|
| **镜像 Image** | 只读的应用快照（代码 + 运行时 + 系统层） | 一个"冻结的" fat-jar + JRE + OS 环境 |
| **容器 Container** | 镜像跑起来的进程实例，相互隔离 | 用同一份 classpath 启动的多个 JVM 进程 |
| **镜像层 Layer** | Dockerfile 每条指令产生一层，可缓存复用 | 类似 Maven 的增量编译缓存 |
| **仓库 Registry** | 镜像的中央存储（Docker Hub / MCR） | Maven Central / 公司 Nexus |
| **镜像标签 Tag** | `mysql:8.4.8` 冒号后就是 tag | 构件版本号 |
| **Dockerfile** | 构建镜像的配方文件 | 可执行的"pom.xml + 装机脚本" |
| **Volume 卷** | 容器外的持久化目录，容器删了数据还在 | 外挂的数据盘（数据库文件不能随容器死） |
| **网络 Network** | 同一 Compose 项目内服务用**服务名**互访 | K8s Service 名的简化版 |
| **端口映射** | `宿主端口:容器端口`，如 `5080:8080` | 端口转发 |
| **.dockerignore** | 构建时不拷入镜像的文件清单 | ≈ `.gitignore` 的构建版 |
| **多阶段构建** | 一个 Dockerfile 里分"编译环境"和"运行环境"两段 | 用 Maven 镜像编译，但只把 jar 拷进 JRE 镜像 |

**关键心智转变**：

- 容器是**进程**，不是虚拟机——启动毫秒级，共享宿主内核。
- 镜像是**不可变**的——改配置 = 构建新镜像，不是登进去改文件。
- 容器文件系统是**临时的**——容器删除即丢失，重要数据必须放 Volume。

---

## 3. Dockerfile 详解

### 3.1 最小结构

```dockerfile
FROM eclipse-temurin:21-jre        # 基础镜像（≈ extends 一个"父镜像"）
WORKDIR /app                       # 工作目录（≈ cd，后续指令的相对路径基准）
COPY target/app.jar app.jar        # 把构建上下文里的文件拷进镜像
EXPOSE 8080                        # 声明端口（仅文档作用，真正生效靠 -p 映射）
ENTRYPOINT ["java", "-jar", "app.jar"]  # 容器启动命令（exec 形式，推荐）
```

### 3.2 层缓存：构建速度的关键

Docker 逐条执行指令，每条生成一个**层**；某层输入没变就**直接复用缓存**。所以指令顺序要从"最不常变"排到"最常变"：

```dockerfile
# 反模式：先 COPY 全部代码 → 改一行代码，依赖全部重下
COPY . .
RUN npm ci

# 正确：先只拷依赖清单 → 依赖层缓存；再拷源码 → 只有源码层重建
COPY package*.json ./
RUN npm ci
COPY . .
```

对应 Java 项目同理：先 `COPY pom.xml` + 下载依赖，再 `COPY src/`。

### 3.3 多阶段构建：编译环境与运行环境分离

编译需要 JDK/SDK/Node（几百 MB），运行只需要 JRE/runtime。多阶段构建让每个阶段用不同基础镜像，**最终镜像只保留最后一个阶段的内容**：

```dockerfile
FROM maven:3.9-eclipse-temurin-21 AS build   # 阶段1：取名 build
WORKDIR /src
COPY pom.xml .
RUN mvn dependency:go-offline
COPY src/ src/
RUN mvn package -DskipTests

FROM eclipse-temurin:21-jre-alpine           # 阶段2：运行时镜像（最终镜像）
WORKDIR /app
COPY --from=build /src/target/app.jar .      # --from=build：从前一阶段拷产物
ENTRYPOINT ["java", "-jar", "app.jar"]
```

最终镜像里没有 Maven、没有源码、没有依赖缓存——**更小、更安全（攻击面小）**。

### 3.4 其他高频指令

| 指令 | 作用 | 注意 |
|---|---|---|
| `RUN` | 构建时执行命令（装包、编译） | 每层是叠加的，删文件要在同一层删 |
| `COPY` / `ADD` | 拷文件进镜像 | 用 COPY；ADD 的自动解压/URL 特性易踩坑 |
| `ENV` | 设环境变量（构建+运行都生效） | 密钥不要写这里（会进镜像层历史） |
| `ARG` | 仅构建期的变量 | 与 ENV 区别：运行时不存在 |
| `USER` | 切换运行用户 | **生产必须非 root** |
| `EXPOSE` | 声明监听端口 | 纯文档性质，不映射端口 |
| `ENTRYPOINT` vs `CMD` | 入口命令 vs 默认参数 | exec 数组形式能收到 SIGTERM，优雅停机 |
| `HEALTHCHECK` | 容器健康检查 | Compose 的 `depends_on: service_healthy` 依赖它 |

### 3.5 基础镜像命名规律（以 .NET 为例，Java 同理）

| 镜像 | 用途 | 类比 |
|---|---|---|
| `mcr.microsoft.com/dotnet/sdk:10.0.401` | 编译用（含完整 SDK） | `maven:3.9-eclipse-temurin-21`（含 JDK+Maven） |
| `mcr.microsoft.com/dotnet/aspnet:10.0.12` | 运行 ASP.NET 用（仅运行时） | `eclipse-temurin:21-jre` |
| `node:24.13.1-alpine` | Node 构建/运行，alpine 变体 | alpine = 精简 Linux（~5MB 基底），镜像最小但用 musl libc，偶有兼容性坑 |

**版本要钉死**（`mysql:8.4.8` 而不是 `mysql:latest`）——可复现构建，升级是显式决策。本项目三个镜像全部钉了精确版本。

---

## 4. Docker Compose 详解

Compose = 用一个 YAML 描述"一组互相依赖的服务怎么起"，一条命令拉起整套环境（≈ 本地版 K8s，但简单得多）。

### 4.1 核心字段

```yaml
name: myapp                    # 项目名（网络/卷/容器名的前缀）
services:                      # 每个 service ≈ 一种容器
  db:
    image: mysql:8.4.8         # 方式一：直接用现成镜像
    build: .                   # 方式二：用当前目录 Dockerfile 现场构建（二选一）
    environment:               # 容器内环境变量（≈ application.yml 的环境变量覆盖）
      MYSQL_DATABASE: app
    ports: ["127.0.0.1:3308:3306"]   # 宿主:容器；绑 127.0.0.1 = 只允许本机访问（安全细节）
    volumes: ["db-data:/var/lib/mysql"]  # 命名卷:容器内路径
    healthcheck:               # 健康检查（决定 depends_on: service_healthy）
      test: ["CMD-SHELL", "mysql -e 'SELECT 1'"]
      interval: 3s
      retries: 40
    profiles: [full]           # 档案：只有 --profile full 才启动（≈ Spring profiles 思想）
    depends_on:                # 启动顺序依赖
      db:
        condition: service_healthy   # 等 db 健康再启动（不是只等进程起来！）
volumes:
  db-data:                     # 声明命名卷（由 Docker 管理，容器删了数据还在）
```

### 4.2 环境变量与 .env

- Compose 自动读同目录 `.env` 文件，`${VAR}` 在 YAML 里展开。
- `${VAR:?错误提示}`：变量未设置则**报错中止**（防裸奔）。
- `${VAR:-默认值}`：未设置时用默认值。
- `.env` 存密钥 → **必须 gitignore**（本项目 `.gitignore` 和 `.dockerignore` 都排了它）。

### 4.3 网络

同一 Compose 项目的服务自动进同一网络，**用服务名当主机名互访**：应用连数据库写 `Server=db`（不是 localhost！localhost 是容器自己）。端口映射只影响"宿主机→容器"方向。

---

## 5. ClinicFlow 项目 Docker 全解析

### 5.1 根 `Dockerfile`：三阶段构建（前端 → 后端 → 运行时）

```dockerfile
# ---- 阶段1：前端构建 ----
FROM node:24.13.1-alpine AS frontend      # 钉死版本的 Node 构建环境，阶段取名 frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./            # 先只拷依赖清单 → package-lock 不变则下一层命中缓存
RUN npm ci                                # 严格按 lock 文件安装（≈ 可复现构建，不用 npm install）
COPY frontend/ ./                         # 再拷源码（改源码只重建此层之后的层）
RUN npm run build                         # 产出静态文件到 dist/

# ---- 阶段2：后端发布 ----
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS backend   # 含完整 .NET SDK 的编译镜像
WORKDIR /src
COPY global.json Directory.Build.props ./ # 构建配置文件先行（钉 SDK 版本、开 lock 还原）
COPY backend/ backend/
RUN dotnet restore backend --locked-mode  # --locked-mode：严格按 packages.lock.json 还原（≈ npm ci）
RUN dotnet publish backend -c Release --no-restore -o /out  # 发布为生产 DLL（≈ mvn package）
COPY --from=frontend /src/frontend/dist/ /out/wwwroot/  # 前端产物塞进 ASP.NET 静态目录

# ---- 阶段3：最终运行时镜像（真正部署的只有这个）----
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12  # 仅 ASP.NET 运行时，无 SDK → 镜像小、攻击面小
WORKDIR /app
# Data Protection（ASP.NET 的 Cookie 加密体系）需要可写的密钥目录；
# chown 给内置非 root 用户 app（官方运行时镜像自带）。
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
COPY --from=backend /out/ ./              # 只拿发布产物，源码/SDK/Node 全部不进最终镜像
USER app                                  # 非 root 运行（生产安全基线）
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClinicFlow.dll"]   # exec 数组形式：dotnet 进程是 PID 1，能收到停机信号
```

**为什么是这个结构**：

- 镜像里**没有**源码、Node、SDK——最终镜像只含 ASP.NET 运行时 + 编译产物（几百 MB 的 SDK 镜像不进交付物）。
- `USER app` + 密钥目录 chown：以非 root 跑，且 ASP.NET Data Protection 有地方落密钥文件。
- 前端 build 产物拷进 `wwwroot`：`Program.cs` 的 `UseStaticFiles()`/`MapFallbackToFile("index.html")` 直接托管它——**一个容器同时服务 API 和前端**。

### 5.2 `compose.yaml`：三个服务 + 一个隐藏档案

```yaml
name: clinicflow
services:
  db:                              # MySQL 8.4.8（钉版本）
    image: mysql:8.4.8
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD:?run scripts/configure.sh}  # :? 未配置即报错，引导跑配置脚本
      MYSQL_DATABASE: clinicflow   # 首次启动自动建库建用户
      MYSQL_USER: clinicflow
      MYSQL_PASSWORD: ${DB_PASSWORD:?run scripts/configure.sh}
    ports: ["127.0.0.1:${DB_PORT:-3308}:3306"]  # 只绑回环地址：本机可连，局域网不可达（MySQL 不暴露给邻居）
    volumes: ["mysql-data:/var/lib/mysql"]      # 数据持久化：容器删了数据还在
    healthcheck:                  # 真·就绪探测：用应用账号真连一次 TCP
      test: ["CMD-SHELL", "MYSQL_PWD=$$MYSQL_PASSWORD mysql --protocol=TCP -h127.0.0.1 -uclinicflow -Dclinicflow -e 'SELECT 1'"]
      interval: 3s
      retries: 40                 # 3s × 40 ≈ 2 分钟容忍 MySQL 首次初始化
      # $$ 转义：Compose 里 $ 是变量展开，$$ 才是给 shell 的单个 $
  mock:                            # 模拟外部集成方（Node 小服务，Dockerfile 在 mock-external/）
    build: ./mock-external
    ports: ["127.0.0.1:${MOCK_PORT:-5090}:5090"]
    environment:
      INTEGRATION_TOKEN: ${INTEGRATION_TOKEN:?run scripts/configure.sh}
    volumes: ["external-data:/data"]   # mock 的持久化（收到的消息/receipt）
  app:
    profiles: [full]               # 关键设计：默认 compose up 只起 db+mock（本地 dotnet run 开发）；
                                   # --profile full 才把 app 也容器化（验证完整交付）
    build: .                       # 用根 Dockerfile 现场构建
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: http://+:8080          # 容器内监听 8080（对照 Dockerfile EXPOSE）
      # 注意 Server=db：Compose 网络内用服务名互访，不是 localhost！
      # __ 双下划线 = .NET 配置的嵌套键（ConnectionStrings:Clinic）
      ConnectionStrings__Clinic: Server=db;Database=clinicflow;User=clinicflow;Password=${DB_PASSWORD}
      DEMO_PASSWORD: ${DEMO_PASSWORD}         # 首次种子账号密码（见 Identity.Seed）
      INTEGRATION_TOKEN: ${INTEGRATION_TOKEN} # 与 mock 共享的集成令牌
      Integration__Url: http://mock:5090      # Outbox 投递目标 = mock 服务名
    ports: ["127.0.0.1:${APP_PORT:-5080}:8080"]
    volumes: ["data-protection:/home/app/.aspnet/DataProtection-Keys"]  # Cookie 加密密钥持久化：
    # 没有这个卷，每次重建容器密钥就变 → 所有已发 Cookie 失效（用户被强制登出）
    depends_on:
      db:
        condition: service_healthy   # 等 MySQL 真正可连再起 app（配合启动时 MigrateAsync）
      mock:
        condition: service_started
volumes:
  data-protection:
  mysql-data:
  external-data:
```

**启动流程**（`scripts/start.sh`）：

```bash
./scripts/configure.sh                            # 首次生成 .env（随机密钥，umask 077 权限收紧）
docker compose --profile full up -d --build --wait  # 构建并起全套，--wait 等健康检查通过
# 然后轮询 /api/health 直到 ready
```

两种工作模式：
- **日常开发**：`docker compose up -d`（无 profile）→ 只起 db + mock；app 用 `dotnet run` 本机跑，改代码即时生效。
- **交付验证**：`--profile full` → app 也走容器，验证 Dockerfile 真的能独立构建运行（`docs/evidence/clean-start.md` 的"干净启动"证据就是这么来的）。

### 5.3 `.dockerignore`

```
.git
.env          # 密钥绝不进构建上下文（否则 docker history 里能翻到）
**/bin **/obj **/node_modules **/dist   # 本机构建产物不进镜像（镜像内自己构建，保证干净）
artifacts .DS_Store
```

作用：减小构建上下文（COPY . 时更快）、防止本机产物污染镜像内构建、防密钥泄漏。

### 5.4 `mock-external/Dockerfile`：最小服务镜像

```dockerfile
FROM node:24.13.1-alpine
WORKDIR /app
COPY . .
RUN mkdir /data && chown node:node /data   # node 镜像自带非 root 用户 node
USER node
CMD ["node", "server.mjs"]   # CMD（可被 docker run 参数覆盖）vs ENTRYPOINT（固定入口）
```

---

## 6. 编写模板与最佳实践

### 6.1 Spring Boot 应用模板（你的主战场）

```dockerfile
# ---- 构建阶段 ----
FROM maven:3.9-eclipse-temurin-21 AS build
WORKDIR /src
COPY pom.xml .
RUN mvn dependency:go-offline -q      # 依赖层缓存：pom 不变不重下
COPY src/ src/
RUN mvn package -DskipTests -q

# ---- 运行阶段 ----
FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
RUN adduser -D -u 1000 app && chown app /app   # alpine 建非 root 用户
COPY --from=build /src/target/*.jar app.jar
USER app
EXPOSE 8080
ENTRYPOINT ["java", "-XX:MaxRAMPercentage=75", "-jar", "app.jar"]
# 容器感知：现代 JVM 自动读 cgroup 限制，MaxRAMPercentage 让堆占容器内存的 75%
```

> 替代方案：**Jib**（`mvn compile jib:dockerBuild`）或 **Buildpacks** 无需手写 Dockerfile，自动分层（依赖层/资源层/类层），是 Java 社区主流做法之一。手写 Dockerfile 更通用（多语言混合构建如本项目），Jib 更省心（纯 Java）。建议先理解手写版，再用 Jib。

### 6.2 .NET 应用模板（本项目同款）

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore --locked-mode
COPY . .
RUN dotnet publish -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/ ./
USER app                 # 官方运行时镜像自带 app 用户
EXPOSE 8080
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

### 6.3 开发环境 Compose 模板（应用 + MySQL + Redis）

```yaml
name: myapp
services:
  db:
    image: mysql:8.4
    environment:
      MYSQL_ROOT_PASSWORD: ${DB_ROOT_PASSWORD:?set it in .env}
      MYSQL_DATABASE: app
      MYSQL_USER: app
      MYSQL_PASSWORD: ${DB_PASSWORD:?set it in .env}
    ports: ["127.0.0.1:3306:3306"]
    volumes: ["db-data:/var/lib/mysql"]
    healthcheck:
      test: ["CMD-SHELL", "MYSQL_PWD=$$MYSQL_PASSWORD mysql -h127.0.0.1 -uapp -Dapp -e 'SELECT 1'"]
      interval: 3s
      retries: 30
  redis:
    image: redis:7-alpine
    ports: ["127.0.0.1:6379:6379"]
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 3s
      retries: 10
  app:
    profiles: [full]            # 平时本机 IDE 跑应用；--profile full 才容器化
    build: .
    environment:
      SPRING_DATASOURCE_URL: jdbc:mysql://db:3306/app
      SPRING_DATASOURCE_PASSWORD: ${DB_PASSWORD}
      SPRING_DATA_REDIS_HOST: redis
    ports: ["127.0.0.1:8080:8080"]
    depends_on:
      db:
        condition: service_healthy
      redis:
        condition: service_healthy
volumes:
  db-data:
```

### 6.4 最佳实践清单（按重要度排序）

**安全**
1. **非 root 运行**：`USER app`（.NET/node 官方镜像自带用户；Java 镜像自己 `adduser`）。
2. **密钥不进镜像**：密钥走 `.env`（gitignore + dockerignore）或运行时环境变量；绝不 `ENV PASSWORD=xxx` 写进 Dockerfile。
3. **端口绑回环**：开发环境 `127.0.0.1:xxxx:xxxx`，数据库不暴露给局域网。
4. **钉版本**：基础镜像 `mysql:8.4.8` 不用 `latest`；依赖用 lock 文件（`npm ci` / `dotnet restore --locked-mode` / Maven lock 插件）。

**构建效率**
5. **层缓存排序**：依赖清单先于源码 COPY。
6. **写 .dockerignore**：`.git`、本机构建产物、`.env` 全部排除。
7. **多阶段构建**：编译镜像不进最终产物。

**运行可靠**
8. **数据放卷**：数据库文件、加密密钥（本项目 DataProtection-Keys 就是教训点）必须命名卷持久化。
9. **healthcheck + `depends_on: condition: service_healthy`**：解决"MySQL 进程起了但还没初始化完"的经典竞态；`--wait` 让 compose up 等到真正就绪。
10. **exec 数组形式 ENTRYPOINT**：让应用进程成为 PID 1，收到 SIGTERM 优雅停机（`docker stop` 默认先发 SIGTERM，10 秒后 SIGKILL）。
11. **profiles 区分模式**：依赖（db/mock）常驻容器，应用在 IDE 跑；全量交付验证才 `--profile full`。

**Java 容器化特别注意**
12. JVM 21 已容器感知（自动认 cgroup 内存限制），但仍建议显式 `-XX:MaxRAMPercentage`。
13. 容器里 `localhost` ≠ 宿主机：应用间互访用 Compose 服务名。
14. 时区：镜像默认 UTC。本项目恰好"全链路 UTC"是最佳实践；Java 应用如需本地时区显式 `-Duser.timezone=Asia/Shanghai`。

### 6.5 常见坑

| 症状 | 原因 | 解法 |
|---|---|---|
| 改代码后镜像没变 | 层缓存命中了 COPY 前的层 | 确认 .dockerignore 没排掉源码；`docker compose build --no-cache` |
| 应用连 `localhost:3306` 不通 | 容器内 localhost 是容器自己 | 用服务名 `db:3306` |
| `depends_on` 了还是连接拒绝 | 只等进程启动，没等就绪 | healthcheck + `condition: service_healthy` |
| 容器重建后登录态全丢 | 加密密钥在容器临时文件系统 | 密钥目录挂命名卷（本项目 data-protection 卷） |
| 镜像几个 GB | 把 SDK/源码/node_modules 打进了最终镜像 | 多阶段构建 + .dockerignore |
| `docker compose up` 报变量未设置 | `.env` 缺失 | 用 `${VAR:?提示}` 提前暴露，或跑 configure 脚本 |

---

## 7. 命令速查表

```bash
# —— Compose（日常 90% 用这些）——
docker compose up -d                 # 起依赖（db/mock），-d 后台
docker compose --profile full up -d --build --wait   # 全量：构建 app 镜像并等健康
docker compose ps                    # 看服务状态/健康
docker compose logs -f app           # 跟踪某服务日志
docker compose exec db mysql -uclinicflow -p clinicflow   # 进容器执行命令
docker compose down                  # 停并删容器（卷保留，数据不丢）
docker compose down -v               # ⚠️ 连卷一起删（数据清空，"干净启动"演练用）

# —— 镜像与容器 ——
docker build -t myapp:1.0 .          # 手动构建镜像
docker run --rm -p 8080:8080 --env-file .env myapp:1.0
docker images / docker ps -a
docker history myapp:1.0             # 看镜像层（排查"为什么这么大/密钥泄漏"）
docker exec -it <容器> sh            # 进容器排障（alpine 没有 bash，用 sh）
docker system prune                  # 清理悬空镜像/停止的容器

# —— 排障三板斧 ——
docker compose logs app              # 1. 看日志
docker compose exec app env          # 2. 看容器内环境变量是否如预期
docker compose config                # 3. 看 .env 展开后的最终 YAML
```
