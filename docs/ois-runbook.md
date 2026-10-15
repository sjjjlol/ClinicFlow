# OIS 操作与接口

适用主入口 `/`。启动、账号与验证命令统一见[运行手册](runbook.md)，范围见[OIS MVP](ois-mvp.md)，执行证据见[验收记录](acceptance.md#current-status)。

## 数据与事务

OIS 使用九张领域表及患者、用户和幂等记录表；[数据库设计](database-design.md#ois-data)说明关系与约束。所有写入先锁定 `OisGate`，在同一事务内提交领域状态、待办、审计和幂等响应，失败全部回滚。门诊绑定具体接诊账号，只有对应诊疗人员能登记接诊完成。

## API

GET `/api/ois/workspace`：一致性快照，含档案、处方、计划、资源、预约及其放行阻碍、执行、待办、患者和审计。

POST `/api/ois/commands/{operation}`：Cookie 身份认证、CSRF 头和 `Idempotency-Key` 必需。多数修改需要 `id` 与目标记录当前 `version`；`prescribe` 使用档案 id/version，`plan` 使用 `caseId` 和档案 version。客户端不得自行指定操作者。

|操作|角色|主要字段|
|---|---|---|
|case|诊疗|patientId, text|
|prescribe|诊疗|id, version, diagnosis, text, reason（修订必填）|
|plan|物理|caseId, version, resourceId, text, attachment, reason（修订必填）|
|review|诊疗|id, version|
|book|门诊/治疗：预约；实验：诊疗；维护：物理|kind, resourceId, startUtc, endUtc；患者预约带 patientId；治疗带 caseId；实验带 planId|
|reschedule|对应活动负责人|id, version, resourceId, startUtc, endUtc, reason|
|cancel / restore / no-show|对应活动负责人|id, version, reason|
|validate|物理|实验预约 id, version, outcome=Passed/Failed, text, failureType=Plan/Clinical/Equipment|
|machine|物理|id, version, available, reason|
|consult|诊疗|门诊 id, version, caseId|
|start|诊疗|治疗预约 id, version, planId（核对的当前版本）, identityChecked=true|
|finish|诊疗|执行记录 id, version, outcome=Completed/Interrupted, text|
|not-executed|诊疗|治疗预约 id, version, reason|
|read-task / resolve-task|待办责任角色|id, version；处理须 text|
|correction|计划验证：物理；处方/执行：诊疗|id, version, text, reason|

成功命令返回 `{ "targetId": "..." }`，客户端随后重读 workspace。幂等键按 `ois + actor + role + key` 隔离，指纹包含操作和请求内容；同一操作者不可将同一 key 用于另一操作或修改后的内容。回放返回原 targetId，不返回最新快照。

时间带 UTC 偏移传入，存储 UTC，界面固定上海时间。时段按15分钟对齐、最长4小时，检查半开区间重叠。开始治疗必须在预约时段内，过期时段需改期。实验到开始时间后可登记结果；未到场在结束时间之后登记。过期取消预约不能原时段恢复，需新约。

主要错误：400 输入不合法或缺 CSRF/key；401 未登录；403 角色无权限；404 记录不存在；409 冲突/过期版本/未放行。网络结果不明时使用原 key 重试；版本冲突应刷新后重新核对，不能盲目覆盖。

## 演示流程

1. 预约人员选择患者，创建门诊预约；诊疗人员建立诊疗档案，签署模拟诊疗记录与处方。
2. 物理师登记计划摘要和指定机器；诊疗人员审核该版本，安排机器实验。
3. 物理师在实验开始后提交通过/失败及原因。失败时查看诊疗/物理和预约人员的待办。
4. 预约人员提前安排单次治疗，查看待审核、待验证或机器不可用等阻碍。
5. 当前版本双重通过后，诊疗人员在预约时段核对患者和计划，开始执行，再记录完成或中断。
6. 验证失败后登记新版，旧版通过记录不沿用；设备恢复不自动放行失败计划。治疗中断产生人工待办，无自动补排。

## 实际边界

本 MVP 是小规模单院区演示：快照读取全量数据，写入由数据库单行锁串行化；没有生产规模的分页、细分锁或组织级患者权限。计划附件只保存引用，不上传文件。签署属于应用内记录，不是外部证书电子签名。没有真实设备控制、剂量计算、批量疗程排期和生产临床认证。
