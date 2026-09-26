# ClinicFlow

[![CI](https://github.com/sjjjlol/ClinicFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/sjjjlol/ClinicFlow/actions/workflows/ci.yml)

A runnable C#/.NET learning project built with AI assistance: appointment scheduling, reliable external synchronization, a streaming appointment assistant, and pre-visit imaging integration. All patients and images are fictional. This is not clinical software, an Elekta product, or a certified FHIR/DICOM implementation.

## 从这里开始

- **第一次使用**：[运行与验证](docs/runbook.md) → [账号与预约流程](docs/accounts-and-lifecycle.md)。
- **不知道该读什么**：[文档导读地图](docs/README.md)按常见问题连接说明、代码和测试。
- **判断是否完成**：[当前状态与验收](docs/acceptance.md#current-status)区分本次复核、历史验证和未覆盖能力。
- **查表结构**：[数据库设计与数据字典](docs/database-design.md)，包含13张业务表、索引和AI持久化边界。
- **准备面试**：[统一演示与面试材料](docs/demo-and-interview.md)。

## 中文快速启动

需要 Docker Compose、Bash、curl、OpenSSL。首次构建需要网络，无须在本机安装 .NET 或 Node。

```sh
git clone git@github.com:sjjjlol/ClinicFlow.git
cd ClinicFlow
./scripts/start.sh
```

打开 **http://localhost:5080**。脚本生成本地忽略的 `.env`，启动基础服务、应用迁移并创建虚构种子数据。登录页可注册个人预约账号；工作人员账号为 `scheduler`、`taskoperator`、`admin`，密码读取本机 `.env` 的 `DEMO_PASSWORD`。Admin 不继承预约及影像权限。

停止并保留数据：`docker compose --profile full down`。本机调试、端口、重置、测试与升级入口统一见[运行手册](docs/runbook.md)。

## 已实现的功能

| 功能 | 核心行为 | 详细说明 |
|---|---|---|
| 预约闭环 | 注册、本人预约、前置核对、确认、改期、取消、结束后登记完成 | [账号与生命周期](docs/accounts-and-lifecycle.md) |
| 一致性 | 连续15分钟时段、资源有序锁、唯一约束、Version、请求幂等、事务审计 | [架构](docs/architecture.md) / [API](docs/api.md) |
| 外部同步 | Outbox、短事务租约、受控重试、持久接收去重及版本防倒退 | [架构](docs/architecture.md) |
| 预约助手 | Pi Agent Core + Kimi，流式文字、查询进度、最多三个候选、显式确认后写入 | [Agent 指南](docs/appointment-agent.md) |
| 医疗接口 | FHIR R4 Patient/Appointment 限定只读接口 | [FHIR 范围](docs/fhir-r4.md) |
| 就诊前影像 | Orthanc DICOMweb 查询/获取、患者核验、关系审计、复用 Stone 显示 | [影像手册](docs/imaging/README.md) |

技术基线：.NET SDK 10.0.401、ASP.NET 10.0.12、EF Core 9.0.20、Pomelo 9.0.0、MySQL 8.4.8、Node 24.13.1、React 19.2.0、TypeScript 5.9.3、Vite 7.3.6。实际依赖以项目文件和锁文件为准；[版本选择理由](docs/adr/001-platform.md)。

## 完成度与边界

约定的学习演示功能已有实现及本地验证证据。本次文档整理复跑后端 **80/80**、Pi **3/3**、接收端 **1/1**，前端构建通过；历史影像阶段记录浏览器 **14/14**，本次未重跑。最新扩展的远程 CI 未在本次核验，徽章不能替代对应提交的验收结论。

未覆盖真实医院/设备接入、临床计算、完整标准符合性、密码找回/SSO、多节点运行、大规模压测、自动保留清理及完整影像灾备。所有性能数字均是限定环境实验结果。生产化需求和证据边界见[验收记录](docs/acceptance.md)。

历史需求保留于 [SPEC](SPEC.md)；按需阅读，无须先读完所有规格再运行项目。
