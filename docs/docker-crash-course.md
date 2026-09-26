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

### 5.1 `Dockerfile`：四个构建阶段

以仓库根目录 [Dockerfile](../Dockerfile) 为准，避免在教程中复制一份逐渐过时的完整配置。

| 阶段 | 输入与职责 | 输出去向 |
|---|---|---|
| frontend | Node 24.13.1 Alpine，npm ci 后构建 React/Vite | dist 复制进 ASP.NET 的 wwwroot |
| agent-runtime | Node 24.13.1 Debian，安装 Pi 运行依赖并复制 worker/runtime | Node 二进制、私有运行时及依赖复制进最终镜像 |
| backend | .NET SDK 10.0.401，锁定还原与 Release publish | 发布 DLL 与前端静态资源 |
| 最终运行阶段 | ASP.NET 10.0.12，复制后端及 Agent 产物 | 非 root 用户 app 启动 ClinicFlow.dll |

最终镜像包含 Node，因为 Pi 通过私有子进程执行；它不需要 .NET SDK 或前端开发工具链。这个 Node 子进程不单独暴露公开端口。不要将早期三阶段示意误读为当前镜像没有 Node。

前端产物由 ASP.NET 的静态文件与 fallback 路由提供，一个 app 容器同时服务页面和 API。Data Protection 密钥目录由 app 用户写入，并通过卷持久化。COPY 清单先于源码、npm ci 和 locked restore 的目的，是依赖缓存与可复现构建。

### 5.2 `compose.yaml`：基础服务与可选 profile

实际环境变量、健康检查和卷定义见 [compose.yaml](../compose.yaml)。

| 服务 | 启动方式 | 数据与网络职责 |
|---|---|---|
| db | 默认启动 | MySQL，mysql-data 卷；默认宿主回环 3308；通过 TCP 应用账号查询判定就绪 |
| mock | 默认启动 | 独立 Node 接收端，external-data 卷持久保存去重和快照；默认回环 5090 |
| app | full profile | ASP.NET + React + Pi 子进程；默认回环 5080；data-protection 卷保存会话密钥 |
| orthanc | imaging profile | 影像归档与 Stone 插件，imaging-data 卷；回环 8042，服务端 Basic 认证 |

容器内用服务名互访，例如数据库 `db`、外部接收方 `mock`、影像服务 `orthanc`；`localhost` 在容器里指容器自身。`ConnectionStrings__Clinic` 的双下划线映射 .NET 嵌套配置；Compose 的 `$$` 用于把字面 `$` 留给容器 shell。

- **日常开发**：默认启动 db/mock 后，使用 `scripts/api.sh` 和 Vite；后端修改需重新启动或另外配置 watch，普通 dotnet run 不会自动重载。
- **完整应用**：`scripts/start.sh` 启动 full profile，并检查 API 健康状态。
- **影像演示**：按[影像手册](imaging/README.md#operations)启动、导入 fixture，再构建启用影像配置的 app。

密码变量只负责配置输入；数据库首次种子密码和已持久化账号不会因修改 `.env` 自动轮换。停机保留卷与删除卷是两种不同操作，具体命令查[运行手册](runbook.md)。

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
