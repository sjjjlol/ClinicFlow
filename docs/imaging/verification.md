# 影像扩展验证记录

日期：2026-09-26。环境：本地 macOS、.NET SDK 10.0.401、Docker 27.3.1；MySQL与Orthanc在容器中；最终应用通过项目Dockerfile构建；浏览器测试使用本机Chrome。只使用合成患者/图案。以下是实际执行结果，不是验收计划。

## 已执行结果

| 层级 | 命令/证据 | 结果 |
|---|---|---|
| 环境 | `scripts/imaging-up.sh` | Orthanc健康；STOW-RS导入18实例/6序列/3检查；重复运行成功，UID不变 |
| 编译 | `dotnet build backend --no-restore`；`npm --prefix frontend run build` | 后端零警告/零错误；TypeScript/Vite构建成功 |
| 数据库与后端 | `scripts/test.sh --no-restore` | **80通过、0失败、0跳过**；约19秒；包含新增14项影像测试 |
| 真实标准接口与权限 | `node tests/http-imaging.mjs` | 通过；QIDO候选、两项关联、错配/重复拒绝、WADO元数据与原文件、代理跨检查拒绝、角色/CSRF/解除审计 |
| 受控外部故障 | `node tests/http-imaging-outage.mjs` | 通过；停机503、本地关系可读/解除、审计、恢复后查询成功 |
| 全量浏览器 | `BASE_URL=http://127.0.0.1:5080 PW_CHANNEL=chrome npm --prefix frontend run test:e2e` | **14通过、0失败**；包括新增影像端到端流程 |
| 影像实际显示 | Playwright像素请求+可见canvas+截图 | 已检查合成灰度图实际显示；查看器网络无失败响应 |
| 格式 | Prettier、CSharpier、`git diff --check` | 通过 |
| 容器 | 本地app重新构建和健康启动 | 已验证应用容器能够访问Orthanc服务名并完成影像流程 |

浏览器证据见 [查看器截图](images/viewer.png) 与 [预约面板截图](images/panel.png)。下载测试检查返回文件的Part10封装标记，结合真实STOW/WADO/显示验证；这不是完整DICOM对象一致性验证器。

最终容器重建后又单独运行了影像HTTP与浏览器测试，均通过。完整后端结果与原有浏览器流程的回归结果如上。原始本地输出保留在被Git忽略的 `artifacts/imaging-verification/`；这些文件不含服务器凭据。

## 后端测试证明的关键不变量

- 患者编号错误、签发机构错误或缺失时，没有新关系和成功审计。
- 一项检查混入其他患者实例时拒绝，不仅检查第一个实例。
- 没有映射或尚未关联的检查不能读取。
- 并发关联只有一个赢家、一个成功审计；重复关联不生成第二条记录。
- 解除保留审计，不改变预约Version；影像服务不可用时也可以解除。
- 非法UID（含路径注入和末尾换行）被拒绝。
- 外部错误与无效JSON分别映射；查询达到截断上限时，即使部分身份被过滤，仍报告结果不完整。

## 一次实际停机实验

命令在 `try/finally` 中停止并恢复本项目Orthanc。捕获到的两次预期错误：

```text
imaging_unavailable correlationId=0HNORNFGOK55P:00000004
imaging_unavailable correlationId=0HNORNFGOK55Q:00000003
PASS imaging outage: 503 with correlation, local unlink works, recovery succeeds
```

对应应用日志（已检查）：

```text
Imaging request rejected imaging_unavailable 503 0HNORNFGOK55P:00000004
Imaging request rejected imaging_unavailable 503 0HNORNFGOK55Q:00000003
```

这说明界面/响应里的请求标识能关联到应用日志；并不代表跨所有外部系统的分布式追踪已经建立。

## 验证中发现并修复的问题

1. 当前Orthanc的QIDO `includefield=all` 未给出所需Issuer：显式请求标签，保留身份校验。
2. Stone使用标签编号及全局序列/实例查询，初版代理拒绝：补充受控标准路由，并测试跨Study请求仍403。
3. 缩略图通过序列 `/rendered` 获取：加入该只读路径，浏览器不再只验证首页200。
4. 本机Python证书链缺失：指定系统CA成功安装；未关闭TLS验证。
5. 本机没有Playwright自带Chromium：使用已有Chrome完成全量测试；CI安装Chromium。
6. 一次Docker registry TLS握手超时：与应用代码无关，重试仅应用镜像构建；保留既有业务与影像数据卷。

## 没有证明的内容

没有推送本次提交，也没有运行本次远端GitHub Actions，因此不能将CI配置变更称为远端CI成功。没有真实设备/医院对接、厂商矩阵、大影像性能压测、医疗器械认证、全量DICOM/FHIR一致性或影像灾备演练。对象范围及工程限制见 [设计文档](design.md)。

## 提交阶段

- 第一阶段 `3114277`：Orthanc环境、合成数据与标准导入。
- 第二阶段 `b960648`：身份映射、.NET集成、事务关系、审计与后端/HTTP测试。
- 第三阶段 `6005c3a`：页面、受控查看器、实际显示和故障验证、CI接入。
- 第四阶段：学习、面试、运行与排障文档，以及旧文档纠偏。

用 `git log -4 --oneline` 查看最终四个本地提交。用户原有未跟踪IDE目录和解决方案文件保留，没有纳入本次提交。
