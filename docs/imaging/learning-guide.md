# 从零学习 DICOM：围绕 ClinicFlow 的两周路线

这份路线的目标是：你能运行项目、读懂关键代码、解释取舍，并根据证据定位一个问题。建议总投入 20–30 小时，阅读后立刻用代码或请求验证。实现由工具协助完成；面试时把你实际理解和验证的部分说清楚。

## 1. 先认清系统各自负责什么（约 1 小时）

ClinicFlow 管患者和预约；影像服务 Orthanc 管 DICOM 对象的保存与检索；Stone 把影像显示出来。数据库只保存关联关系和身份映射，像素数据不放进预约表。

HIS 通常围绕医院业务管理；RIS 通常围绕放射科检查流程；PACS 围绕影像归档和通信。它们是职责概念，实际产品边界可能重叠。本项目既不是完整 HIS，也不是 RIS，更不是自己编写的完整 PACS。

本次用例：患者已有一次影像检查，工作人员在新的就诊预约中关联它。因此不能把预约的 ID 当作 DICOM StudyInstanceUID，也不能从“预约已完成”推出“影像已经产生”。

阅读：`Scheduling/Models.cs` 的 Appointment 与 `Imaging/Models.cs` 的 ImagingLink。问自己：如果预约改期，历史影像为什么无需改 UID？

## 2. DICOM 是什么（约 2 小时）

DICOM 是医疗影像数据表示与通信的标准体系。`.dcm` 文件是常见表现形式，但 DICOM 不只是图片扩展名。对象里既有像素，也有患者、设备、检查和图像编码相关属性；部分 DICOM 对象甚至不以普通二维像素图为核心。

本项目生成的是带文件元信息的 Part 10 文件：128 字节前导区后有 `DICM` 标记，随后是文件元信息和数据集。**看到 `DICM` 只证明文件封装特征，不代表完整合规，更不证明具有临床价值。**

### 标签、VR、值

DICOM 标签用组号和元素号表示。例如 `(0010,0020)` 是 PatientID。在 DICOM JSON 里写作 `00100020`。

```json
{
  "00100020": { "vr": "LO", "Value": ["CF-IMG-001"] },
  "00100021": { "vr": "LO", "Value": ["ClinicFlowDemo"] },
  "0020000D": { "vr": "UI", "Value": ["2.25.123"] }
}
```

`vr` 是 Value Representation：LO 表示 Long String，UI 表示 UID。Value 是数组，因为某些属性允许多个值。空属性可能没有 Value；PN（人名）可以是带 Alphabetic 等字段的对象，不能把所有值都当作普通字符串。

`DicomWebClient.Value` 只用于当前实现需要的标量/多值标签。它不是通用 DICOM 字典或完整解析器。

### 必须认识的标识

| 标识 | 本项目用途 | 不能替代什么 |
|---|---|---|
| 内部 Patient.Id | MySQL 患者主键 | 不能直接充当外部 PatientID |
| PatientID + IssuerOfPatientID | 外部患者身份核验 | 姓名不能替代它们 |
| AccessionNumber | 测试数据中的检查申请/业务编号示例 | 不等于 StudyInstanceUID，也未在项目中实现申请流程 |
| StudyInstanceUID | 唯一标识一项影像检查 | 不等于预约 ID |
| SeriesInstanceUID | 检查内的一个序列 | 不等于 SeriesNumber |
| SOPInstanceUID | 标识一个 DICOM 实例 | 不一定是一张单帧图片 |
| SOPClassUID | 表明对象属于哪种类型 | 不用于标识某个具体实例 |
| TransferSyntaxUID | 编码、字节序及压缩等规则 | 不等于模态 |

UID 使用数字和点，最长 64 个字符，有自己的格式规则。不要用随机字符串冒充 UID。本项目以 UUID 派生 `2.25.<十进制整数>`，确定性生成只为测试复现，不用患者姓名生成真实业务标识。

## 3. 检查、序列、实例和帧（约 1 小时）

```text
患者 1
├── Study A：已有影像检查
│   ├── Series 1：几何图案
│   │   ├── Instance 1：单帧图像
│   │   ├── Instance 2
│   │   └── Instance 3
│   └── Series 2：另一个序列
└── Study B：另一项已有影像检查
```

真实 CT 的一个序列常有多张切片，但不要说“每个 DICOM 文件永远只是一张切片”：增强型、多帧等对象可以在单个实例中包含多帧。本项目只验证单帧 Secondary Capture，模态是 OT，几何图案不能称为真实 CT 数据。

打开 `imaging/seed.py`：观察相同 Study UID 下怎样生成两个 Series UID，以及每个 Series 下三个 SOPInstanceUID。修改图案并观察查看器变化前，先理解同 UID 已存在时外部服务如何处理重复导入。

## 4. 三类 DICOMweb 操作（约 2 小时）

| 操作 | 含义 | 本项目在哪里调用 |
|---|---|---|
| QIDO-RS | 查询符合条件的检查、序列、实例 | .NET 查询患者已有 Study；Stone 查询检查与序列 |
| WADO-RS | 获取对象、元数据、帧等表示 | 关联前核验；序列详情；下载；Stone 加载像素 |
| STOW-RS | 通过 Web 存储 DICOM 对象 | seed.py 导入合成文件 |

HTTP/JSON 只是外观；这些接口还定义了资源层级、DICOM JSON 标签表示、内容协商和 multipart 格式。普通 `/api/imaging/.../links` 是我们自己的业务 API，不因处理影像就自动成为 DICOM 标准端点。

传统 DICOM 网络服务常使用 DIMSE，例如 C-STORE、C-FIND、C-MOVE。知道名称和目的即可；本项目没有实现这些服务，也没有通过模拟设备验证它们。DICOMweb 与 DIMSE 不应混称为同一组 HTTP 接口。

### 一次具体调用

1. 前端请求 `/api/imaging/{appointmentId}/search`。
2. 后端从预约获得内部患者，再读取映射。
3. HttpClient 向 Orthanc 请求 `dicom-web/studies?PatientID=...`，显式请求签发机构与描述标签。
4. 返回 DICOM JSON；后端核对 PatientID 和 Issuer，转换为界面 DTO。
5. 用户点“关联”后，后端重新获取该 Study 的 metadata 并逐实例检查，然后提交关系。

为什么要再次检查？查询列表可能过时，浏览器输入也不可信。QIDO 找到候选，不等于它已经通过业务授权。

## 5. 原始文件、元数据和显示图像（约 2 小时）

原始 DICOM 文件包含对象的数据集和像素编码；元数据描述对象但不直接携带整段像素；渲染后的 PNG/JPEG 便于浏览，但通常不保留完整原始信息。

下载接口从 WADO multipart 响应中解出单个 `application/dicom` 文件。不能把整段 multipart 响应直接命名为 `.dcm`：它还有分隔符和 MIME 头。

Stone 使用 WebAssembly 等组件解析和显示数据。我们复用它，不声称窗宽窗位或图像渲染算法由自己实现。可练习调整窗宽窗位：它改变灰度值如何映射到显示亮度，并不意味着修改了外部原始影像。

阅读 `Endpoints.cs` 的下载路由、`MapViewer`。重点解释为何凭据只留在服务器，以及为何每次代理请求仍要检查关联。

## 6. .NET 工程能力怎么讲（约 3 小时）

- **DI**：ImagingService 每个请求持有一个 DbContext；Typed HttpClient 负责外部 HTTP 连接管理。
- **异步与取消**：CancellationToken 沿数据库、HTTP 和流读取传递；用户断开和外部超时不应混为一种失败。
- **事务**：关联与审计共同落库；无需把一次 HTTP 调用和 MySQL 写入包装成虚假的跨系统原子事务。
- **数据库约束**：UI 禁用按钮只改善体验，复合主键才解决并发重复关联。
- **边界转换**：DICOM JSON 在适配器中处理，前端接收面向业务的 DTO。
- **可测试性**：HTTP 适配器用可替换的 HttpMessageHandler，数据库约束用真实 MySQL，协议互通用真实 Orthanc，最终行为用浏览器测试。

不要背“用了依赖注入、DDD、微服务”等名词。选一个失败场景，把调用、状态变化、事务和测试讲完整。

## 7. 测试、故障和面试准备（约 5–8 小时）

依次运行后端测试、HTTP 测试、浏览器测试，再执行受控停机实验。详细命令见 [运行与排障](operations.md)。阅读断言，理解每个测试排除了什么错误。

建议学习记录：

| 练习 | 自己应能回答 |
|---|---|
| 给患者 1 关联患者 2 的 Study UID | 为什么 409，哪个字段不匹配，有没有新增关系和成功审计？ |
| 两个请求同时关联相同检查 | 谁保证只有一条关系，失败请求的审计去哪里了？ |
| 直接打开查看器 URL，退出登录后刷新 | 后端如何挡住绕过页面的访问？已经下载的数据能否收回？ |
| 停止 Orthanc | 为什么本地预约仍可操作、影像查询失败、解除关联仍成功？ |
| 查看一条故障响应 | correlationId 如何把界面和一次请求对应起来？它不是跨所有系统的完整链路追踪 |
| 新注册患者查询影像 | 为什么不能因为姓名一样就自动关联演示患者的检查？ |

## 8. 面试中的能力边界

完成本项目后可表述为“实现并验证了基于 DICOMweb 的就诊前影像关联原型”。不要说“有医院上线经验”“满足医疗器械质量体系”“实现完整 DICOM/PACS”“已获得标准认证”。

可以用测试、审计、标准支持范围、故障证据、需求追踪来展示严谨工程习惯。受监管开发经验还包括组织流程、风险管理、评审、变更控制及真实验证活动，本项目不能自动等同这些经历。

## 参考资料

- [DICOM PS3.3 信息对象定义](https://dicom.nema.org/medical/dicom/current/output/html/part03.html)
- [DICOM PS3.5 数据结构与编码](https://dicom.nema.org/medical/dicom/current/output/html/part05.html)
- [DICOM PS3.6 数据字典](https://dicom.nema.org/medical/dicom/current/output/html/part06.html)
- [DICOM PS3.10 文件格式](https://dicom.nema.org/medical/dicom/current/output/html/part10.html)
- [DICOM PS3.18 Web 服务与 JSON 表示](https://dicom.nema.org/medical/dicom/current/output/html/part18.html)

按问题查具体章节，不需要从头背整套标准。项目支持范围以实现、测试与设计文档为准。
