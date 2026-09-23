# FHIR R4 练习范围（4.0.1）

2026-09-24核验 HL7 官方 R4 固定版本页面：[Patient](https://hl7.org/fhir/R4/patient.html)、[Appointment字段](https://hl7.org/fhir/R4/appointment-definitions.html)、[状态值集](https://hl7.org/fhir/R4/valueset-appointmentstatus.html)、[HTTP交互](https://hl7.org/fhir/R4/http.html)。

这是小范围只读映射练习，不声明完整FHIR服务器兼容、认证或医院互联能力。

| 交互 | 支持内容 |
|---|---|
| GET /fhir/r4/metadata | CapabilityStatement，版本4.0.1，仅声明Patient/Appointment的read |
| GET /fhir/r4/Patient/{id} | resourceType/id/active/identifier/name.text；内部虚构患者编号 |
| GET /fhir/r4/Appointment/{uuid} | id/meta.versionId/meta.lastUpdated/status/start/end/minutesDuration/description/participant；ETag和Last-Modified |

返回 application/fhir+json。Pending→pending、Confirmed→booked、Cancelled→cancelled。两个participant分别是Patient相对引用与资源的Location逻辑identifier，后者不是一个可读取的Location端点。participant.status只是模拟排程确认映射，不能解释为患者真实同意。start/end成对输出UTC instant，原预约取消后仍保留其历史时间。仅存在于本练习中的internal Version映射成FHIR meta.versionId字符串；不提供版本历史。

登录Cookie与普通API共享。无效标识400、资源不存在404、不支持交互405、不支持查询400、只要求XML时406；这些适配器错误返回OperationOutcome。认证失败仍由统一中间件返回401/403，不声称完整FHIR错误协商。

不支持写入、搜索、Bundle、事务、历史、条件读/更新、XML、profile验证、术语服务、SMART-on-FHIR或临床字段。读取练习不接收FHIR资源写入；无效输入验收以标识、查询和不支持方法为范围。自定义 `/messages` 的messageId/version/snapshot不是FHIR协议。

测试：FhirTests映射契约；tests/http-fhir.mjs真实HTTP读取和错误边界。这些检查不是全量FHIR Validator验证。
