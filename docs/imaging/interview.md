# 医疗信息化面试材料

## 三分钟讲解与演示

**0:00–0:30，说明业务**：ClinicFlow 是 .NET 预约系统。这次增加就诊前已有影像关联，让工作人员在预约上下文中找到患者历史检查。预约与影像检查是两个独立概念。

**0:30–1:10，正常路径**：scheduler 打开种子患者预约，查询两个已有 Study，关联一项，展示两个 Series、六个 Instance。打开 Stone，切换图像；下载一个原始 DICOM 文件。说明显示组件和归档服务均为复用组件。

**1:10–1:50，关键规则**：展示外部 PatientID + Issuer 映射，解释不能用姓名匹配；后端关联前逐实例核验，不能信任前端选择。展示解除关联和保留的审计。

**1:50–2:30，失败证据**：展示测试结果：患者错配拒绝、重复/并发关联、匿名/患者账号拒绝。选一段故障实验记录，说明服务停止时返回 503 及请求标识，而本地解除仍有效。

**2:30–3:00，工程边界**：展示设计、接口、自动化验证和支持范围。本项目验证特定合成对象与真实 Orthanc 的 DICOMweb 互通；未接入真实医院或设备，没有医疗软件认证。FHIR R4 适配仍只覆盖既有 Patient/Appointment。

## 英文 90 秒介绍

I extended a .NET scheduling application with a pre-visit imaging workflow. Staff can find a patient's existing imaging studies, associate them with an appointment, inspect the series and instances, and open a reused web viewer.

The application integrates with Orthanc through DICOMweb. It uses QIDO-RS for search and WADO-RS for metadata and image retrieval. A repeatable fixture script stores synthetic DICOM objects through STOW-RS. Orthanc handles the archive, and Stone handles rendering; my application handles the business relationship, authorization, identity checks and audit trail.

An important design decision is to keep an appointment separate from a DICOM study. Before creating a link, the backend checks every instance against the mapped patient identifier and issuer. A database constraint prevents duplicate links, and the link and its audit entry are committed together.

I validate the workflow with real MySQL tests, HTTP tests against Orthanc, browser tests and a controlled service outage. When the archive is unavailable, the application reports a traceable error while local unlinking still works.

This is an educational integration prototype built with AI assistance. It is not deployed clinical software, a complete PACS, or evidence of medical-device certification.

学习后请按自己的实际参与程度调整 “I extended / I validate”，不要把未亲自理解的实现描述为熟练经验。

## 高频追问：回答要点与代码证据

| 问题 | 回答思路 | 证据入口 |
|---|---|---|
| 为什么加入 DICOM？ | 就诊前需要查看已有影像，提供具体业务价值；不是仅增加文件上传入口 | ImagingPanel、用例与设计文档 |
| 为什么采用 DICOMweb？ | 当前 .NET Web 集成可用标准 HTTP；它是有明确数据/资源语义的标准，项目无需先构建设备网络服务 | DicomWebClient |
| 你实现了 PACS 吗？ | 没有，复用 Orthanc；应用承担受控业务集成 | compose.yaml |
| 为什么还需要 FHIR？ | 既有 FHIR 表达患者与预约资源；DICOM 本阶段负责影像检索。当前没有 ImagingStudy 跨标准映射 | FhirAdapter、标准支持范围 |
| Study 与 Appointment 为什么分开？ | 历史检查可以关联后续就诊；改期不改变影像；没有建立检查订单流程 | ImagingLink 复合键 |
| 姓名相同能关联吗？ | 姓名不作为身份键；严格核对外部编号与签发机构，缺失就阻止 | ImagingIdentity、Verify |
| Study 层信息正确是否足够？ | 不够，逐实例校验可以拒绝混合身份的检查；读取时再次验证 | Verify、混合实例测试 |
| 两个工作人员同时关联怎么办？ | 数据库复合主键保证唯一；重复键转为409，失败事务不保留成功审计 | 并发关联测试 |
| 为什么关联不增加预约 Version？ | 影像参考资料是独立关系，不改变预约占用和状态；自身靠唯一约束与独立审计维护 | 版本不变测试 |
| 为什么不用 Outbox 做影像查询？ | Outbox 用于预约状态的可靠异步投递；这里是有时效的交互查询，业务语义不同 | Integration 与 Imaging 的边界 |
| 外部返回成功、本地保存失败呢？ | 查询没有创建远端业务对象；本地关系和审计一起失败，用户可重试 | Link 调用顺序 |
| 关联成功但响应丢失呢？ | 该接口没有预约那套幂等回放；重复返回409，刷新查看已存在关系 | HTTP重复关联测试 |
| 解除关联为什么不删除文件？ | 关系属于本次预约，影像有独立生命周期，还可能用于其他就诊 | Remove 不调用外部删除 |
| 前端隐藏按钮能算授权吗？ | 不能，所有影像路由及查看器资源由后端策略保护 | MapGroup.RequireAuthorization |
| 可以拿到查看器 URL 绕过权限吗？ | URL 不含服务器凭据，仍需 Cookie、角色、有效关联和身份校验 | HTTP查看器越权测试 |
| 如何防止代理变成任意访问通道？ | 固定服务端地址，路径白名单，限制Study，拒绝任意方法，不透传客户端凭据 | MapViewer |
| 服务坏了如何定位？ | 收集请求标识，区分缺失映射、身份不匹配、服务错误，再检查容器/网络/凭据 | 故障脚本、operations.md |
| 测试为什么不全部 mock？ | mock验证映射和失败；真实数据库验证约束；真实Orthanc暴露协议差异；浏览器验证实际显示 | 分层测试记录 |
| 这体现了受监管开发经验吗？ | 可以展示可追踪需求、边界、测试与审计的工程习惯；不能等同真实受监管组织工作经历 | 验收矩阵与限制 |

## 两个可以讲清楚的 STAR 案例

### 协议互通问题

- **情境**：测试数据已经导入，查询返回200，应用却显示没有影像。
- **任务**：恢复查询，同时不能降低患者身份检查。
- **行动**：比较真实 QIDO 响应与应用期望，发现所需 Issuer 标签缺失；显式请求标签；保留元数据逐实例核验，并增加真实服务回归。
- **结果**：患者自己的两项检查可查到，另一患者的检查仍不能关联。结果以本地验证记录为证，不夸大为生产事故处理。

### 外部依赖故障隔离

- **情境**：影像服务是独立容器，可能停机；预约和关系在 MySQL 中。
- **任务**：明确区分影像不可用与本地状态丢失，允许纠正错误关系。
- **行动**：注入停机，检查503与请求标识；验证本地关系可读、解除及审计成功；finally恢复服务并再次查询。
- **结果**：验证了当前同步交互和本地事务边界。没有因此宣称具备高可用集群或容灾方案。

## 岗位要求对应的可展示成果

| 任职要求 | 本项目材料 | 应避免的夸大 |
|---|---|---|
| .NET 开发 | Minimal API、DI、EF、Typed HttpClient、取消与错误映射 | 不能仅凭生成代码声称精通 |
| Scrum/敏捷 | 分阶段切片、明确验收、独立提交、复盘 | 个人项目不等于真实团队 Scrum 经历 |
| 自动化测试/CI | xUnit、HTTP、Playwright、故障脚本与工作流 | 本地通过不等于远端已经通过 |
| Web Service/API | DICOMweb客户端、业务API、既有FHIR只读接口 | 不声称实现完整标准服务端 |
| 分布式架构 | 外部影像与本地事务边界、既有Outbox | 单体加容器不等于大规模分布式生产经验 |
| 严格质量环境 | 需求→实现→测试、审计、范围声明 | 不声称合规或认证 |
| 故障与根因分析 | 两个真实互通问题、受控停机实验 | 模拟故障明确标为模拟 |
| DICOM/HL7/FHIR | DICOMweb端到端验证、FHIR现有范围、概念区分 | 没有HL7 v2、设备或真实医院对接 |

## 学习完成自检

不看答案，用5分钟画出一次“关联”调用经过的三个系统；指出失败发生在事务前还是事务内；解释三个不同 UID；把两个患者编号换一下预测返回结果；根据请求标识复盘一次停机。做不到的部分回到代码和测试，不必先背更多标准名词。
