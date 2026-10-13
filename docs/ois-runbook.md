# OIS 运行、接口与验收

## 运行

Docker 完整运行：`./scripts/start.sh`，访问 http://localhost:5080。启动时自动应用增量迁移，原预约表保留。

本机开发：

```bash
./scripts/configure.sh
docker compose up -d db --wait
./scripts/api.sh
# 另一个终端
npm --prefix frontend ci
npm --prefix frontend run dev
```

访问 http://localhost:5173。`scheduler`（预约）、`clinician`（诊疗）、`physicist`（物理）共用本地 `.env` 中配置的演示密码。已有数据库升级时会补充缺失的新角色账号，不重置已有账号密码。

本次开发使用独立工作区和 `docker compose -p clinicflow-ois`，不会改动原工作区未提交的文档整理。独立工作区中开发时应继续使用相同 Compose 项目名；数据库端口默认为 3308。

旧学习模块 `/legacy` 与 OIS 的预约/资源隔离；旧预约不会自动成为诊疗或执行事实。旧影像浏览仍使用原预约和影像身份关联。历史 Agent 回归仅在源码开发环境显式设置 `Legacy__Enabled=true` 并安装 `agent-runtime` 依赖时启用，默认运行镜像不包含该运行时。

## 数据表

|表|含义|
|---|---|
|CareCase|患者诊疗档案，当前处方/计划引用及并发版本|
|Prescription|追加的诊疗记录与处方版本、签署人、被替代版本|
|TreatmentPlan|外部计划摘要与引用、指定机器、版本审核和物理验证结果|
|OisResource|接诊人员或机器，可用性、原因、版本|
|Reservation|门诊、单次治疗、实验、维护的时间占用；实验无 PatientId|
|TreatmentDelivery|实际治疗所用计划/机器、身份核对后的开始、结果、签署人|
|OisTask|责任角色、原因、已读及处理结果|
|OisAudit|追加式操作记录及原状态/请求内容；签署更正不覆盖原事实|
|OisGate|单院区演示写事务的数据库互斥行|

幂等继续使用既有 Idempotency 表，OIS 的 key 命名空间包含业务前缀和操作者角色。所有写入同一事务提交；失败全部回滚。门诊绑定具体接诊账号，只有对应诊疗人员能登记接诊完成。

## API

GET `/api/ois/workspace`：一致性快照，含档案、处方、计划、资源、预约及其放行阻碍、执行、待办、患者和审计。

POST `/api/ois/commands/{operation}`：Cookie 身份认证、CSRF 头和 `Idempotency-Key` 必需。修改记录需要 `id` 与当前 `version`；计划登记使用 `caseId` 和档案版本。客户端不得自行指定操作者。

|操作|角色|主要字段|
|---|---|---|
|case|诊疗|patientId, text|
|prescribe|诊疗|id, version, diagnosis, text, reason（修订必填）|
|plan|物理|caseId, version, resourceId, text, attachment, reason（修订必填）|
|review|诊疗|id, version|
|book|预约/诊疗/物理|kind, resourceId, startUtc, endUtc；患者预约带 patientId；治疗带 caseId；实验带 planId|
|reschedule|对应活动负责人|id, version, resourceId, startUtc, endUtc, reason|
|cancel / restore / no-show|对应活动负责人|id, version, reason|
|validate|物理|实验预约 id, version, outcome=Passed/Failed, text, failureType=Plan/Clinical/Equipment|
|machine|物理|id, version, available, reason|
|consult|诊疗|门诊 id, version, caseId|
|start|诊疗|治疗预约 id, version, planId（核对的当前版本）, identityChecked=true|
|finish|诊疗|执行记录 id, version, outcome=Completed/Interrupted, text|
|not-executed|诊疗|治疗预约 id, version, reason|
|read-task / resolve-task|待办责任角色|id, version；处理须 text|
|correction|物理验证/诊疗执行对应角色|id, version, text, reason|

时间带 UTC 偏移传入，存储 UTC，界面固定上海时间。时段按15分钟对齐、最长4小时，检查半开区间重叠。开始治疗必须在预约时段内，过期时段需改期。实验到开始时间后可登记结果；未到场在结束时间之后登记。过期取消预约不能原时段恢复，需新约。

主要错误：400 输入不合法或缺 CSRF/key；401 未登录；403 角色无权限；404 记录不存在；409 冲突/过期版本/未放行。网络结果不明时使用原 key 重试；版本冲突应刷新后重新核对，不能盲目覆盖。

## 演示流程

1. 预约人员选择患者，创建门诊预约；诊疗人员建立诊疗档案，签署模拟诊疗记录与处方。
2. 物理师登记计划摘要和指定机器；诊疗人员审核该版本，安排机器实验。
3. 物理师在实验开始后提交通过/失败及原因。失败时查看诊疗/物理和预约人员的待办。
4. 预约人员提前安排单次治疗，查看待审核、待验证或机器不可用等阻碍。
5. 当前版本双重通过后，诊疗人员在预约时段核对患者和计划，开始执行，再记录完成或中断。
6. 验证失败后登记新版，旧版通过记录不沿用；设备恢复不自动放行失败计划。治疗中断产生人工待办，无自动补排。

## 验证命令

```bash
# 纯规则测试，无需数据库
dotnet test tests --filter FullyQualifiedName~OisRulesTests
# 使用 *_tests 隔离库；scripts/test.sh 从本地配置生成连接
./scripts/test.sh
# 启动 API 后
set -a
source .env
set +a
node tests/http-ois.mjs
# 浏览器测试，启动 Vite 后
npm --prefix frontend exec -- playwright install chromium
npm --prefix frontend run test:e2e -- ois.spec.ts
```

全量历史 .NET 测试包含旧 Agent 运行时测试，首次需 `npm --prefix agent-runtime ci`。这些依赖仅用于历史回归，OIS 运行不需要。数据库全闭环测试使用测试端 TimeProvider 推进时间，无生产跳过放行或快进接口。

## 实际边界

本 MVP 是小规模单院区演示：快照读取全量数据，写入由数据库单行锁串行化；没有生产规模的分页、细分锁或组织级患者权限。计划附件只保存引用，不上传文件。签署属于应用内记录，不是外部证书电子签名。没有真实设备控制、剂量计算、批量疗程排期和生产临床认证。
