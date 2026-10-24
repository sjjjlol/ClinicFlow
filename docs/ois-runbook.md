# OIS 操作与接口

适用主入口 `/`。启动、账号与验证命令统一见[运行手册](runbook.md)，范围见[OIS MVP](ois-mvp.md)，执行证据见[验收记录](acceptance.md#current-status)。

## 数据与事务

OIS 使用诊疗、计划与验证、排程与执行三个业务模块的领域表，以及草稿、待办、审计、患者、用户和幂等记录表；[数据库设计](database-design.md#ois-data)说明关系与约束。所有写入先锁定 `OisGate`，在同一事务内提交领域状态、待办、审计和幂等响应，失败全部回滚。门诊绑定具体接诊账号，只有对应诊疗人员能登记接诊完成。

## API

GET `/api/ois/workspace`：一致性快照，含档案、处方、计划、资源、预约及其放行阻碍、执行、待办、患者、审计和当前账号的草稿。

资源写接口需要 Cookie、`X-CSRF-TOKEN` 和 `Idempotency-Key`。目标 ID 放在路径中，正文不接受覆盖目标；`version` 是目标当前版本，签署处方和登记计划使用档案版本，实验结果使用预约版本。操作者来自会话。旧 `/api/ois/commands/*` 返回 404。

|接口|角色|请求正文字段|
|---|---|---|
|POST `/api/ois/cases`|诊疗|patientId, text|
|POST `/api/ois/cases/{id}/terminate`|诊疗|version, reason|
|POST `/api/ois/cases/{id}/notes`|诊疗|version, text|
|POST `/api/ois/cases/{id}/prescriptions`|诊疗|version, diagnosis, text, reason, treatmentSite, totalDoseGy, fractionDoseGy, fractions, treatmentMaterials|
|POST `/api/ois/cases/{id}/plans`|物理|version, resourceId, text, reason, attachment, verificationMethod, equipment, measurementConditions, expectedValues, standardName, standardVersion, acceptanceCriteria, allowedDeviation|
|POST `/api/ois/plans/{id}/review`|诊疗|version|
|POST `/api/ois/reservations/{id}/reschedule`|活动负责人|version, resourceId, startUtc, endUtc, reason|
|POST `/api/ois/reservations/{id}/restore`|活动负责人|version, reason|
|POST `/api/ois/reservations/{id}/cancel`|活动负责人|version, reason|
|POST `/api/ois/reservations/{id}/no-show`|活动负责人|version, reason|
|POST `/api/ois/reservations/{id}/experiment-results`|物理|version, outcome, text, failureType, measuredValues, resultAttachment|
|POST `/api/ois/reservations/{id}/complete-consultation`|诊疗|version, caseId|
|POST `/api/ois/reservations/{id}/start-treatment`|诊疗|version, planId, identityChecked|
|POST `/api/ois/treatment-deliveries/{id}/finish`|诊疗|version, outcome, text|
|POST `/api/ois/reservations/{id}/not-executed`|诊疗|version, reason|
|PUT `/api/ois/machines/{id}/availability`|物理|version, available, reason|
|POST `/api/ois/tasks/{id}/read`|待办责任角色|version|
|POST `/api/ois/tasks/{id}/resolve`|待办责任角色|version, text|
|POST `/api/ois/reservations/consultation`|预约|resourceId, startUtc, endUtc, reason, patientId, caseId|
|POST `/api/ois/reservations/treatment`|预约|resourceId, startUtc, endUtc, reason, patientId, caseId|
|POST `/api/ois/reservations/experiment`|诊疗|resourceId, startUtc, endUtc, reason, planId|
|POST `/api/ois/reservations/maintenance`|物理|resourceId, startUtc, endUtc, reason|
|PUT `/api/ois/cases/{id}/prescription-draft`|诊疗|version, draftJson, draftVersion|
|PUT `/api/ois/cases/{id}/plan-draft`|物理|version, draftJson, draftVersion|
|POST `/api/ois/plans/{id}/corrections`|物理|version, text, reason|
|POST `/api/ois/treatment-deliveries/{id}/corrections`|诊疗|version, text, reason|

成功命令返回 `{ "targetId": "..." }`，客户端随后重读 workspace。幂等键按 `ois + actor + role + key` 隔离，指纹包含操作和请求内容；同一操作者不可将同一 key 用于另一操作或修改后的内容。回放返回原 targetId，不返回最新快照。

时间带 UTC 偏移传入，存储 UTC，界面固定上海时间。时段按15分钟对齐、最长4小时，检查半开区间重叠。开始治疗必须在预约时段内，过期时段需改期。实验到开始时间后可登记结果；未到场在结束时间之后登记。过期取消预约不能原时段恢复，需新约。

主要错误：400 输入不合法或缺 CSRF/key；401 未登录；403 角色无权限；404 记录不存在；409 冲突/过期版本/未放行。网络结果不明时使用原 key 重试；版本冲突应刷新后重新核对，不能盲目覆盖。

## 工作台读取与模块边界

工作台使用一个一致性快照聚合三个模块，不要求客户端分别调用三个服务。预约结果为 `{ reservation, blockers }`；`reservation` 内的 `validationOutcome`、`validationNote`、`failureType`、`measuredValues`、`resultAttachment`、`signedBy`、`signedUtc` 是兼容投影，实际记录保存在 PhysicalValidation。没有记录时，字符串字段为空、签署时间为 null。

`blockers` 仅对 Booked 预约计算，其他状态返回空列表。空列表不代表任意状态的预约都能开始治疗；服务端仍检查预约类型、状态、时段、身份核对和计划版本。`drafts` 只含当前账号的草稿，草稿审计也按账号过滤；其余工作台数据按当前演示范围聚合，不是生产级患者权限过滤。

本次模块化重构保持现有资源路径与 JSON 契约，客户端无需改为微服务调用。从更早的 `/api/ois/commands/*` 版本升级时，才需要同步更新使用旧路径的客户端。

## 演示流程

1. 预约人员选择患者，创建门诊预约；诊疗人员建立诊疗档案，签署模拟诊疗记录与处方。
2. 物理师登记计划摘要和指定机器；诊疗人员审核该版本，安排机器实验。
3. 物理师在实验开始后提交通过/失败及原因。失败时查看诊疗/物理和预约人员的待办。
4. 预约人员提前安排单次治疗，查看待审核、待验证或机器不可用等阻碍。
5. 当前版本双重通过后，诊疗人员在预约时段核对患者和计划，开始执行，再记录完成或中断。
6. 方案变更后登记新版，旧版通过记录不沿用；物理执行问题可按原方案重测并保留每次结果；设备恢复不自动放行失败计划。治疗中断产生人工待办，无自动补排。

## 实际边界

本 MVP 是小规模单院区演示：快照读取全量数据，写入由数据库单行锁串行化；没有生产规模的分页、细分锁或组织级患者权限。计划附件只保存引用，不上传文件。签署属于应用内记录，不是外部证书电子签名。没有真实设备控制、剂量计算、批量疗程排期和生产临床认证。

## 治疗前物理验证

诊断与待验证治疗方案分别记录。诊疗人员提交处方时须填写 `treatmentSite`、`totalDoseGy`、`fractionDoseGy`、`fractions`、`treatmentMaterials`（无用品时明确填“无”）。剂量单位为 Gy；系统检查正数，不计算临床剂量或提供临床阈值。

物理师登记计划时须填写 `verificationMethod`、`equipment`、`measurementConditions`、`expectedValues`（含单位、测量点）、`standardName`、`standardVersion`、`acceptanceCriteria`、`allowedDeviation`。每个计划绑定处方版本及机器，诊疗人员审核后才能排实验。水模体测量属于患者方案验证的一种方式，设备校准不属于本次流程。

实验签署必填 `measuredValues`、`resultAttachment`（报告引用）及原有 `text` 判断依据，物理师依据记录的规范人工判定。每次签署结果由计划与验证模块独立保存，通过 `ReservationId` 关联对应实验预约；工作台仍在 `reservations[].reservation` 中返回验证字段，重测不覆盖旧签署。物理问题可在同一已审核版本安排重测；改变剂量、用品或计划则创建新版，重新审核验证。设备失败仍会停用机器，恢复机器不代表验证通过。

所有失败都通知诊疗人员；物理问题另派物理师待办。临床失败必须先修订处方或终止，不能通过登记同处方的新计划绕过。待办已读不等于已解决：物理问题须重测通过，临床问题须修订处方或终止，才能记录解决结果。物理师签署通过后不增加重复审批，其他治疗条件仍须满足。

`PUT /api/ois/cases/{id}/prescription-draft` 或 `plan-draft` 保存不完整处方或计划：档案 `version`、`draftJson`（表单值对象的 JSON 字符串）、`draftVersion`（新建为 0）。草稿按账号和档案隔离，覆盖检查版本，成功提交后删除对应草稿。恢复草稿时若关联版本发生变化，界面提示重新核对；草稿不改变当前签署和放行状态。

诊疗人员可用 `POST /api/ois/cases/{id}/notes`（`version/text`）追加备注，不改变已签署版本；`POST /api/ois/cases/{id}/terminate`（`version/reason`）终止档案并阻断后续治疗，已有历史保留。活动治疗期间不能终止。

升级保留历史记录；缺少结构化字段的旧方案不会被自动补造数据，须签署完整新处方和计划后重新验证，才能继续治疗。

跨升级重放保留历史操作名称和规范化指纹，不需要保留旧 URL。新请求对应相同业务输入时返回历史 targetId，不再执行领域变更。处方更正通过签署新版实现；计划验证更正和治疗记录更正分别归属其资源接口。
