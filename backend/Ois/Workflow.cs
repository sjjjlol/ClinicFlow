
namespace ClinicFlow.Ois;

// Pure workflow rules; the service executes each command under a database writer lock.
public class OisWorkflow
{
    public List<OisDraft> Drafts { get; set; } = [];
    public List<CareCase> Cases { get; set; } = [];
    public List<Prescription> Prescriptions { get; set; } = [];
    public List<TreatmentPlan> Plans { get; set; } = [];
    public List<OisResource> Resources { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
    public List<TreatmentDelivery> Deliveries { get; set; } = [];
    public List<OisTask> Tasks { get; set; } = [];
    public List<Patient> Patients { get; set; } = [];

    public static string RequiredRole(string op, OisCommand c) =>
        op switch
        {
            "book" => c.Kind is "Experiment" ? "Clinician"
            : c.Kind is "Maintenance" ? "Physicist"
            : "Scheduler",
            "save-draft" => c.Kind switch
            {
                "prescribe" => "Clinician",
                "plan" => "Physicist",
                _ => throw new BusinessException("invalid_kind", "草稿类型无效", 400),
            },
            "terminate" or "note" => "Clinician",
            "case"
            or "prescribe"
            or "review"
            or "start"
            or "finish"
            or "consult"
            or "not-executed" => "Clinician",
            "plan" or "validate" or "machine" => "Physicist",
            "cancel" or "restore" or "reschedule" or "no-show" => "reservation-owner",
            "read-task" or "resolve-task" => "task-owner",
            "correction" => "clinical-author",
            _ => throw new BusinessException("invalid_operation", "未知 OIS 操作", 400),
        };

    public string Apply(string op, OisCommand c, string actor, string role, DateTime now)
    {
        var required = RequiredRole(op, c);
        if (required == "reservation-owner")
        {
            var r = Get(Reservations, c.Id);
            required =
                r.Kind == "Experiment" ? "Clinician"
                : r.Kind == "Maintenance" ? "Physicist"
                : "Scheduler";
        }
        if (required == "task-owner")
            required = Get(Tasks, c.Id).Role;
        if (required == "clinical-author")
        {
            required = Plans.Any(x => x.Id == c.Id) ? "Physicist" : "Clinician";
            if (
                !Plans.Any(x => x.Id == c.Id)
                && !Prescriptions.Any(x => x.Id == c.Id)
                && !Deliveries.Any(x => x.Id == c.Id)
            )
                throw new BusinessException("not_found", "签署记录不存在", 404);
        }
        Need(role == required, "forbidden", "当前角色没有此操作权限", 403);

        switch (op)
        {
            case "save-draft":
                var draftCare = Current(Cases, c.CaseId, c.Version);
                Need(!draftCare.Terminated, "terminated", "诊疗档案已终止");
                Need(c.DraftJson.Length <= 64000, "invalid_text", "草稿过长", 400);
                try
                {
                    using var payload = System.Text.Json.JsonDocument.Parse(c.DraftJson);
                    Need(payload.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object,
                        "invalid_input", "草稿须为表单对象", 400);
                }
                catch (System.Text.Json.JsonException)
                {
                    throw new BusinessException("invalid_input", "草稿格式无效", 400);
                }
                var draft = Drafts.SingleOrDefault(x =>
                    x.CaseId == c.CaseId && x.Actor == actor && x.Operation == c.Kind);
                if (draft is null)
                {
                    Need(c.DraftVersion == 0, "stale_version", "草稿已提交，请重新打开");
                    draft = new OisDraft { CaseId = c.CaseId, Actor = actor, Operation = c.Kind };
                    Drafts.Add(draft);
                }
                else
                {
                    Need(draft.Version == c.DraftVersion, "stale_version", "草稿已更新，请刷新");
                    draft.Version++;
                }
                draft.Payload = c.DraftJson;
                draft.CaseVersion = c.Version;
                return draft.Id;
            case "terminate":
                var ended = Current(Cases, c.Id, c.Version);
                NoActiveTreatment(ended.Id);
                Need(!ended.Terminated, "terminated", "诊疗档案已终止");
                ended.TerminationReason = Text(c.Reason);
                ended.Terminated = true;
                ended.Version++;
                BlockedTasks(ended.Id, "诊疗方案已终止", now);
                AddTask(ended.Id, "Physicist", "诊疗方案已终止：" + c.Reason, now);
                return ended.Id;
            case "note":
                Current(Cases, c.Id, c.Version);
                Text(c.Text);
                return c.Id;
            case "case":
                Need(Patients.Any(x => x.Id == c.PatientId), "patient_missing", "患者不存在", 404);
                var care = new CareCase { PatientId = c.PatientId, Title = Text(c.Text) };
                Cases.Add(care);
                return care.Id;
            case "prescribe":
                care = Current(Cases, c.Id, c.Version);
                NoActiveTreatment(care.Id);
                Need(!care.Terminated, "terminated", "诊疗档案已终止");
                ValidateClinical(c);
                var rx = new Prescription
                {
                    CaseId = care.Id,
                    TreatmentSite = c.TreatmentSite,
                    TotalDoseGy = c.TotalDoseGy,
                    FractionDoseGy = c.FractionDoseGy,
                    Fractions = c.Fractions,
                    TreatmentMaterials = c.TreatmentMaterials,
                    Diagnosis = Text(c.Diagnosis),
                    Content = Text(c.Text),
                    Reason = care.CurrentPrescriptionId is null ? c.Reason : Text(c.Reason),
                    SupersedesId = care.CurrentPrescriptionId,
                    SignedBy = actor,
                    SignedUtc = now,
                };
                Prescriptions.Add(rx);
                care.CurrentPrescriptionId = rx.Id;
                care.CurrentPlanId = null;
                care.Version++;
                AddTask(care.Id, "Physicist", "处方已签署，请登记匹配的治疗计划", now);
                BlockedTasks(care.Id, "处方更新，待计划审核与验证", now);
                return rx.Id;
            case "plan":
                care = Current(Cases, c.CaseId, c.Version);
                NoActiveTreatment(care.Id);
                Need(!care.Terminated, "terminated", "诊疗档案已终止");
                Need(
                    care.CurrentPrescriptionId is not null,
                    "prescription_required",
                    "请先签署处方"
                );
                Need(!Plans.Any(x => x.PrescriptionId == care.CurrentPrescriptionId && x.Validation == "Failed" && x.FailureType == "Clinical"), "clinical_revision_required", "临床问题须先由诊疗人员修订处方或终止");
                Need(HasClinical(Get(Prescriptions, care.CurrentPrescriptionId!)), "clinical_details_required", "历史处方缺少结构化信息，请先签署完整新处方");
                ValidateVerification(c);
                Machine(c.ResourceId);
                Need(c.Attachment.Length <= 1000, "invalid_text", "附件引用过长", 400);
                var plan = new TreatmentPlan
                {
                    CaseId = care.Id,
                    PrescriptionId = care.CurrentPrescriptionId!,
                    Revision = Plans.Count(x => x.CaseId == care.Id) + 1,
                    VerificationMethod = c.VerificationMethod,
                    Equipment = c.Equipment,
                    MeasurementConditions = c.MeasurementConditions,
                    ExpectedValues = c.ExpectedValues,
                    StandardName = c.StandardName,
                    StandardVersion = c.StandardVersion,
                    AcceptanceCriteria = c.AcceptanceCriteria,
                    AllowedDeviation = c.AllowedDeviation,
                    Summary = Text(c.Text),
                    Attachment = c.Attachment,
                    MachineId = c.ResourceId,
                    RegisteredBy = actor,
                    RegisteredUtc = now,
                };
                if (care.CurrentPlanId is not null)
                    Text(c.Reason);
                Plans.Add(plan);
                care.CurrentPlanId = plan.Id;
                care.Version++;
                AddTask(plan.Id, "Clinician", "新计划待审核及实验排期", now);
                BlockedTasks(care.Id, "计划更新，待重新审核与验证", now);
                return plan.Id;
            case "review":
                plan = Current(Plans, c.Id, c.Version);
                CurrentPlan(plan);
                Need(plan.ReviewedBy is null, "signed", "本版本已审核；修改请创建新版本");
                Need(plan.Validation != "Failed", "failed_plan", "验证失败的计划须修订为新版本");
                plan.ReviewedBy = actor;
                plan.ReviewedUtc = now;
                plan.Version++;
                return plan.Id;
            case "book":
                Need(
                    c.Kind is "Consultation" or "Treatment" or "Experiment" or "Maintenance",
                    "invalid_kind",
                    "预约类型无效",
                    400
                );
                var booking = new Reservation
                {
                    Kind = c.Kind,
                    ResourceId = c.ResourceId,
                    StartUtc = Time(c.StartUtc),
                    EndUtc = Time(c.EndUtc),
                    Reason = c.Reason,
                };
                if (c.Kind is "Consultation" or "Treatment")
                {
                    Need(
                        Patients.Any(x => x.Id == c.PatientId),
                        "patient_missing",
                        "患者不存在",
                        404
                    );
                    booking.PatientId = c.PatientId;
                    if (c.Kind == "Treatment" || c.CaseId != "")
                    {
                        care = Get(Cases, c.CaseId);
                        Need(
                            care.PatientId == c.PatientId,
                            "patient_mismatch",
                            "诊疗档案与患者不匹配",
                            400
                        );
                        booking.CaseId = care.Id;
                    }
                }
                if (c.Kind == "Experiment")
                {
                    plan = Get(Plans, c.PlanId);
                    CurrentPlan(plan);
                    Need(CanExperiment(plan), "signed", "临床问题须修改处方；已通过计划不可重复验证");
                    Need(plan.ReviewedBy is not null, "review_required", "请先审核计划再安排实验");
                    Need(
                        plan.MachineId == c.ResourceId,
                        "machine_mismatch",
                        "实验机器须与计划一致"
                    );
                    Need(
                        !Reservations.Any(x =>
                            x.PlanId == plan.Id && x.Kind == "Experiment" && x.Status == "Booked"
                        ),
                        "duplicate_experiment",
                        "该计划已有待执行实验"
                    );
                    booking.PlanId = plan.Id;
                    booking.CaseId = plan.CaseId;
                }
                if (c.Kind == "Maintenance")
                    booking.Reason = Text(c.Reason);
                CheckSlot(booking, now);
                Reservations.Add(booking);
                if (c.Kind == "Experiment")
                    AddTask(booking.Id, "Physicist", "实验已排期，待执行并签署验证结果", now);
                return booking.Id;
            case "reschedule":
            case "restore":
                booking = Current(Reservations, c.Id, c.Version);
                Need(
                    booking.Status == (op == "restore" ? "Cancelled" : "Booked"),
                    "invalid_state",
                    "当前状态不可改期或恢复"
                );
                var moved = new Reservation
                {
                    Id = booking.Id,
                    Kind = booking.Kind,
                    PatientId = booking.PatientId,
                    CaseId = booking.CaseId,
                    PlanId = booking.PlanId,
                    ResourceId = op == "restore" ? booking.ResourceId : c.ResourceId,
                    StartUtc = op == "restore" ? booking.StartUtc : Time(c.StartUtc),
                    EndUtc = op == "restore" ? booking.EndUtc : Time(c.EndUtc),
                };
                if (booking.Kind == "Experiment")
                {
                    plan = Get(Plans, booking.PlanId!);
                    CurrentPlan(plan);
                    Need(
                        CanExperiment(plan) && plan.MachineId == moved.ResourceId,
                        "invalid_plan",
                        "实验须使用当前待验证计划及其指定机器"
                    );
                    Need(
                        !Reservations.Any(x =>
                            x.Id != booking.Id
                            && x.PlanId == plan.Id
                            && x.Kind == "Experiment"
                            && x.Status == "Booked"
                        ),
                        "duplicate_experiment",
                        "该计划已有待执行实验"
                    );
                }
                CheckSlot(moved, now);
                booking.ResourceId = moved.ResourceId;
                booking.StartUtc = moved.StartUtc;
                booking.EndUtc = moved.EndUtc;
                booking.Status = "Booked";
                booking.Reason = Text(c.Reason);
                booking.Version++;
                return booking.Id;
            case "cancel":
            case "no-show":
                booking = Current(Reservations, c.Id, c.Version);
                Need(booking.Status == "Booked", "invalid_state", "只可处理尚未执行的预约");
                if (op == "no-show")
                    Need(
                        booking.PatientId is not null && now >= booking.EndUtc,
                        "invalid_state",
                        "预约结束后才可登记未到场"
                    );
                booking.Reason = Text(c.Reason);
                booking.Status = op == "cancel" ? "Cancelled" : "NoShow";
                booking.Version++;
                return booking.Id;
            case "validate":
                booking = Current(Reservations, c.Id, c.Version);
                Need(
                    booking.Kind == "Experiment" && booking.Status == "Booked",
                    "invalid_state",
                    "实验不可提交结果"
                );
                Need(now >= booking.StartUtc, "not_started", "实验预约尚未开始");
                plan = Get(Plans, booking.PlanId!);
                CurrentPlan(plan);
                Need(CanExperiment(plan), "signed", "本版本不可验证");
                Need(plan.ReviewedBy is not null, "review_required", "计划尚未审核");
                Text(c.MeasuredValues);
                Text(c.ResultAttachment);
                Need(
                    c.Outcome is "Passed" or "Failed",
                    "invalid_result",
                    "请选择通过或不通过",
                    400
                );
                Need(Machine(booking.ResourceId).Available, "machine_unavailable", "机器已停用");
                Text(c.Text);
                if (c.Outcome == "Failed")
                    Need(
                        c.FailureType is "Plan" or "Clinical" or "Equipment",
                        "invalid_failure",
                        "请选择失败类型",
                        400
                    );
                plan.Validation = c.Outcome;
                plan.ValidatedBy = actor;
                plan.ValidatedUtc = now;
                plan.ValidationNote = c.Text;
                plan.FailureType = c.Outcome == "Failed" ? c.FailureType : "";
                plan.Version++;
                booking.ValidationOutcome = c.Outcome;
                booking.ValidationNote = c.Text;
                booking.FailureType = plan.FailureType;
                booking.MeasuredValues = c.MeasuredValues;
                booking.ResultAttachment = c.ResultAttachment;
                booking.SignedBy = actor;
                booking.SignedUtc = now;
                booking.Status = "Completed";
                booking.Version++;
                if (c.Outcome == "Failed")
                {
                    AddTask(
                        plan.Id,
                        c.FailureType == "Clinical" ? "Clinician" : "Physicist",
                        c.Text,
                        now
                    );
                    if (c.FailureType != "Clinical")
                        AddTask(plan.Id, "Clinician", "物理验证失败，物理师处理中：" + c.Text, now);
                    BlockedTasks(plan.CaseId, "物理验证失败：" + c.Text, now);
                    if (c.FailureType == "Equipment")
                        StopMachine(booking.ResourceId, c.Text, now);
                }
                return plan.Id;
            case "machine":
                var machine = Current(Resources, c.Id, c.Version);
                Need(machine.Kind == "Machine", "invalid_resource", "仅可设置机器可用性");
                Text(c.Reason);
                if (c.Available)
                {
                    machine.Available = true;
                    machine.Reason = c.Reason;
                    machine.Version++;
                }
                else
                    StopMachine(machine.Id, c.Reason, now);
                return machine.Id;
            case "consult":
                booking = Current(Reservations, c.Id, c.Version);
                Need(
                    booking.Kind == "Consultation"
                        && booking.Status == "Booked"
                        && now >= booking.StartUtc,
                    "invalid_state",
                    "门诊尚未开始或已处理"
                );
                Need(
                    booking.ResourceId == actor,
                    "wrong_clinician",
                    "请由预约接诊人员登记完成",
                    403
                );
                care = Get(Cases, c.CaseId);
                Need(
                    care.PatientId == booking.PatientId && care.CurrentPrescriptionId is not null,
                    "clinical_record_required",
                    "请先为该患者建立档案并签署诊疗记录"
                );
                booking.CaseId = care.Id;
                booking.Status = "Completed";
                booking.Version++;
                return booking.Id;
            case "start":
                booking = Current(Reservations, c.Id, c.Version);
                Need(
                    booking.Kind == "Treatment" && booking.Status == "Booked",
                    "invalid_state",
                    "预约不可开始治疗"
                );
                Need(
                    now >= booking.StartUtc && now < booking.EndUtc,
                    "outside_slot",
                    "请在预约时段内开始治疗，超时需改期"
                );
                Need(c.IdentityChecked, "identity_required", "请先核对患者身份");
                var blockers = Blockers(booking);
                Need(blockers.Count == 0, "not_released", string.Join("；", blockers));
                Need(
                    !Deliveries.Any(x =>
                        x.Outcome == "InProgress"
                        && (
                            x.MachineId == booking.ResourceId
                            || Get(Reservations, x.ReservationId).PatientId == booking.PatientId
                        )
                    ),
                    "in_progress",
                    "患者或机器仍有未结束的治疗"
                );
                care = Get(Cases, booking.CaseId!);
                Need(
                    c.PlanId == care.CurrentPlanId,
                    "plan_changed",
                    "计划版本已变更，请重新核对当前计划"
                );
                var delivery = new TreatmentDelivery
                {
                    ReservationId = booking.Id,
                    PlanId = care.CurrentPlanId!,
                    MachineId = booking.ResourceId,
                    StartedBy = actor,
                    StartedUtc = now,
                };
                Deliveries.Add(delivery);
                booking.Status = "InProgress";
                booking.Version++;
                return delivery.Id;
            case "finish":
                delivery = Current(Deliveries, c.Id, c.Version);
                Need(delivery.Outcome == "InProgress", "signed", "执行结果已签署，只能追加更正");
                Need(
                    c.Outcome is "Completed" or "Interrupted",
                    "invalid_result",
                    "请选择完成或中断",
                    400
                );
                delivery.Note = Text(c.Text);
                Need(
                    c.Outcome != "Completed" || Machine(delivery.MachineId).Available,
                    "machine_unavailable",
                    "机器已停用，请记录中断并评估"
                );
                delivery.Outcome = c.Outcome;
                delivery.FinishedBy = actor;
                delivery.FinishedUtc = now;
                delivery.Version++;
                booking = Get(Reservations, delivery.ReservationId);
                booking.Status = "Completed";
                booking.Version++;
                if (c.Outcome == "Interrupted")
                    AddTask(delivery.Id, "Clinician", "治疗中断，需评估后续安排：" + c.Text, now);
                return delivery.Id;
            case "not-executed":
                booking = Current(Reservations, c.Id, c.Version);
                Need(
                    booking.Kind == "Treatment"
                        && booking.Status == "Booked"
                        && now >= booking.StartUtc,
                    "invalid_state",
                    "该预约不可登记未执行"
                );
                booking.Reason = Text(c.Reason);
                booking.Status = "NotExecuted";
                booking.Version++;
                AddTask(booking.Id, "Clinician", "治疗未执行，需评估：" + c.Reason, now);
                return booking.Id;
            case "read-task":
            case "resolve-task":
                var task = Current(Tasks, c.Id, c.Version);
                Need(task.ResolvedBy is null, "invalid_state", "待办已经处理");
                task.ReadBy = actor;
                if (op == "resolve-task")
                {
                    var failed = Plans.SingleOrDefault(x => x.Id == task.TargetId && x.Validation == "Failed");
                    if (failed is not null)
                    {
                        var owner = Get(Cases, failed.CaseId);
                        var current = Plans.SingleOrDefault(x => x.Id == owner.CurrentPlanId);
                        Need(owner.Terminated || (failed.FailureType == "Clinical"
                            ? owner.CurrentPrescriptionId != failed.PrescriptionId
                            : current?.Validation == "Passed"), "unresolved_failure", "须先完成修订、重测通过或终止，已读不等于已解决");
                    }
                    task.Resolution = Text(c.Text);
                    task.ResolvedBy = actor;
                    task.ResolvedUtc = now;
                }
                task.Version++;
                return task.Id;
            case "correction":
                Text(c.Text);
                Text(c.Reason);
                // Corrections are appended in OisAudit. They never silently change a signed result or release a plan.
                if (Plans.Any(x => x.Id == c.Id))
                {
                    plan = Current(Plans, c.Id, c.Version);
                    Need(plan.ValidatedBy is not null, "not_signed", "本计划尚无签署验证记录");
                    AddTask(plan.Id, "Physicist", "验证更正待修订计划：" + c.Text, now);
                    care = Get(Cases, plan.CaseId);
                    if (care.CurrentPlanId == plan.Id)
                    {
                        NoActiveTreatment(care.Id);
                        care.CurrentPlanId = null;
                        care.Version++;
                        BlockedTasks(care.Id, "验证更正，待新版验证", now);
                    }
                }
                else if (Prescriptions.Any(x => x.Id == c.Id))
                    throw new BusinessException(
                        "revision_required",
                        "处方更正请签署新处方版本",
                        400
                    );
                else
                {
                    delivery = Current(Deliveries, c.Id, c.Version);
                    Need(delivery.Outcome != "InProgress", "not_signed", "请先结束治疗记录");
                    AddTask(delivery.Id, "Clinician", "治疗记录更正需评估：" + c.Text, now);
                }
                return c.Id;
        }
        throw new BusinessException("invalid_operation", "未知操作", 400);
    }

    public List<string> Blockers(Reservation r)
    {
        var reasons = new List<string>();
        var resource = Get(Resources, r.ResourceId);
        if (!resource.Available)
            reasons.Add("机器停用：" + resource.Reason);
        if (r.Kind != "Treatment")
            return reasons;
        var care = Get(Cases, r.CaseId!);
        if (care.Terminated) reasons.Add("诊疗方案已终止");
        var plan = Plans.SingleOrDefault(x => x.Id == care.CurrentPlanId);
        if (plan is null)
        {
            reasons.Add("尚无当前治疗计划");
            return reasons;
        }
        if (!HasClinical(Get(Prescriptions, plan.PrescriptionId)) || !HasVerification(plan))
            reasons.Add("方案缺少结构化信息，需修订后重新验证");
        if (plan.PrescriptionId != care.CurrentPrescriptionId)
            reasons.Add("计划与当前处方不一致");
        if (plan.MachineId != r.ResourceId)
            reasons.Add("预约机器与计划不一致");
        if (plan.ReviewedBy is null)
            reasons.Add("待诊疗审核");
        if (plan.Validation != "Passed")
            reasons.Add(plan.Validation == "Failed" ? "物理验证未通过" : "待物理验证");
        return reasons;
    }

    void CheckSlot(Reservation r, DateTime now)
    {
        if (r.StartUtc.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
            || r.EndUtc.Ticks % TimeSpan.FromMinutes(15).Ticks != 0
            || r.EndUtc <= r.StartUtc || r.EndUtc - r.StartUtc > TimeSpan.FromHours(4))
            throw new BusinessException("invalid_time", "起止时间必须对齐15分钟，时长为15分钟至4小时", 400);
        Need(r.StartUtc > now, "past_slot", "请选择未来时段");
        var resource = Get(Resources, r.ResourceId);
        Need(
            resource.Kind == (r.Kind == "Consultation" ? "Clinician" : "Machine"),
            "invalid_resource",
            "资源类型与活动不匹配",
            400
        );
        Need(resource.Available, "machine_unavailable", "机器已停用");
        Need(
            !Deliveries.Any(x => x.Outcome == "InProgress" && x.MachineId == r.ResourceId),
            "in_progress",
            "机器有尚未结束的治疗，请先处理"
        );
        Need(
            !Reservations.Any(x =>
                x.Id != r.Id
                && x.Status != "Cancelled"
                && x.StartUtc < r.EndUtc
                && r.StartUtc < x.EndUtc
                && (
                    x.ResourceId == r.ResourceId
                    || (r.PatientId is not null && x.PatientId == r.PatientId)
                )
            ),
            "slot_conflict",
            "患者或资源时段冲突，请刷新日历"
        );
    }

    static bool HasClinical(Prescription p) =>
        !string.IsNullOrWhiteSpace(p.TreatmentSite)
        && !string.IsNullOrWhiteSpace(p.TreatmentMaterials)
        && p.TotalDoseGy > 0 && p.FractionDoseGy > 0 && p.Fractions > 0;
    static bool HasVerification(TreatmentPlan p) => new[]
    {
        p.VerificationMethod, p.Equipment, p.MeasurementConditions, p.ExpectedValues,
        p.StandardName, p.StandardVersion, p.AcceptanceCriteria, p.AllowedDeviation,
    }.All(x => !string.IsNullOrWhiteSpace(x));

    static bool CanExperiment(TreatmentPlan p) =>
        p.Validation == "Pending" || (p.Validation == "Failed" && p.FailureType != "Clinical");

    static void ValidateClinical(OisCommand c)
    {
        Text(c.TreatmentSite);
        Text(c.TreatmentMaterials);
        Need(c.TotalDoseGy > 0 && c.FractionDoseGy > 0 && c.Fractions > 0, "invalid_dose", "剂量和分次数必须大于零", 400);
    }

    static void ValidateVerification(OisCommand c)
    {
        Text(c.VerificationMethod);
        Text(c.Equipment);
        Text(c.MeasurementConditions);
        Text(c.ExpectedValues);
        Text(c.StandardName);
        Text(c.StandardVersion);
        Text(c.AcceptanceCriteria);
        Text(c.AllowedDeviation);
    }

    void CurrentPlan(TreatmentPlan plan)
    {
        var care = Get(Cases, plan.CaseId);
        Need(!care.Terminated, "terminated", "诊疗档案已终止");
        Need(HasVerification(plan) && HasClinical(Get(Prescriptions, plan.PrescriptionId)), "details_required", "历史方案须补齐结构化信息并重新验证");
        Need(
            care.CurrentPlanId == plan.Id && care.CurrentPrescriptionId == plan.PrescriptionId,
            "superseded_plan",
            "此计划已被替代，请操作当前版本"
        );
    }

    void NoActiveTreatment(string caseId) =>
        Need(
            !Reservations.Any(x => x.CaseId == caseId && x.Status == "InProgress"),
            "in_progress",
            "请先结束正在执行的治疗再修订计划或处方"
        );

    OisResource Machine(string id)
    {
        var machine = Get(Resources, id);
        Need(machine.Kind == "Machine", "invalid_resource", "请选择放疗机器", 400);
        return machine;
    }

    void StopMachine(string id, string reason, DateTime now)
    {
        var machine = Machine(id);
        machine.Available = false;
        machine.Reason = reason;
        machine.Version++;
        foreach (
            var r in Reservations.Where(x =>
                x.ResourceId == id && (x.Status == "InProgress" || x.Status == "Booked")
            )
        )
            AddTask(
                r.Id,
                r.Status == "InProgress" ? "Clinician"
                    : r.Kind == "Experiment" ? "Clinician"
                    : "Scheduler",
                "机器停用：" + reason,
                now
            );
    }

    void BlockedTasks(string caseId, string reason, DateTime now)
    {
        foreach (
            var r in Reservations.Where(x =>
                x.CaseId == caseId && x.Kind == "Treatment" && x.Status == "Booked"
            )
        )
            AddTask(r.Id, "Scheduler", reason, now);
    }

    void AddTask(string target, string role, string reason, DateTime now) =>
        Tasks.Add(
            new OisTask
            {
                TargetId = target,
                Role = role,
                Reason = reason,
                CreatedUtc = now,
            }
        );

    static T Get<T>(List<T> values, string id)
        where T : IOisEntity =>
        values.SingleOrDefault(x => x.Id == id)
        ?? throw new BusinessException("not_found", "记录不存在", 404);

    static T Current<T>(List<T> values, string id, int version)
        where T : IOisEntity
    {
        var value = Get(values, id);
        Need(value.Version == version, "version_conflict", "记录已更新，请刷新后重新操作");
        return value;
    }

    static DateTime Time(DateTimeOffset? value) =>
        value?.UtcDateTime ?? throw new BusinessException("invalid_time", "请选择起止时间", 400);

    static string Text(string? value)
    {
        Need(
            !string.IsNullOrWhiteSpace(value) && value.Length <= 4000,
            "invalid_text",
            "请填写1至4000字符的内容或原因",
            400
        );
        return value!.Trim();
    }

    static void Need(bool condition, string code, string message, int status = 409)
    {
        if (!condition)
            throw new BusinessException(code, message, status);
    }
}
