# ===== ClinicFlow 应用镜像：三阶段构建（前端 → 后端 → 运行时）=====
# 多阶段构建：编译环境（SDK/Node，几百MB）不进最终镜像，交付物只含运行时 + 产物。

# ---- 阶段1：前端构建（React + Vite）----
# AS frontend：给阶段命名，后面用 --from=frontend 引用
FROM node:24.13.1-alpine AS frontend
# WORKDIR ≈ cd，后续指令的相对路径基准（不存在则创建）
WORKDIR /src/frontend
# 层缓存技巧：先只拷依赖清单——package-lock 不变则下一层 RUN 命中缓存
COPY frontend/package*.json ./
# npm ci 严格按 lock 文件安装（可复现构建；不用 npm install）
RUN npm ci
# 再拷源码：改源码只重建此层之后的层，依赖层不重跑
COPY frontend/ ./
# Vite 构建，产物到 dist/
RUN npm run build

# ---- 阶段2：后端发布（.NET SDK 编译镜像）----
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS backend
WORKDIR /src
# 构建配置先行：global.json 钉 SDK 版本；Build.props 开 lock 还原
COPY global.json Directory.Build.props ./
COPY backend/ backend/
# --locked-mode：严格按 packages.lock.json 还原（≈ npm ci）
RUN dotnet restore backend --locked-mode
# 发布生产产物到 /out（≈ mvn package）
RUN dotnet publish backend -c Release --no-restore -o /out
# 前端静态文件塞进 ASP.NET 的 wwwroot
COPY --from=frontend /src/frontend/dist/ /out/wwwroot/
# → 最终一个容器同时服务 API 和前端（Program.cs 的 UseStaticFiles/MapFallbackToFile 托管它）

# ---- 阶段3：最终运行时镜像（真正部署的只有这个）----
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12
WORKDIR /app
# ASP.NET Data Protection（Cookie 加密体系）需要可写的密钥目录；
# 官方镜像自带非 root 用户 app，chown 授权。此目录在 compose.yaml 里挂了命名卷持久化，
# 否则重建容器密钥轮换 → 所有已发会话 Cookie 失效（用户被强制登出）。
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
# 只拿发布产物：源码、SDK、Node 全部不进最终镜像
COPY --from=backend /out/ ./
# 非 root 运行（生产安全基线）
USER app
# 声明监听端口（文档作用；真正映射靠 compose 的 ports）
EXPOSE 8080
# exec 数组形式 ENTRYPOINT：dotnet 进程成为 PID 1，能收到 docker stop 的 SIGTERM → 优雅停机
# （停机时 OutboxWorker 靠 stoppingToken 退出循环，就是这个信号链的终点）
ENTRYPOINT ["dotnet", "ClinicFlow.dll"]
