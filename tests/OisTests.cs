using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClinicFlow;
using ClinicFlow.Ois;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClinicFlow.Tests
{
    // Complete demo inputs for workflow scenarios; missing-field tests call Apply directly.
    static class VerificationInputs
    {
        public static ScenarioCommand Complete(string op, ScenarioCommand command)
        {
            ScenarioCommand input = Copy(command);
            switch (op)
            {
                case "prescribe":
                    input.TreatmentSite = "模拟部位";
                    input.TotalDoseGy = 60;
                    input.FractionDoseGy = 2;
                    input.Fractions = 30;
                    input.TreatmentMaterials = "无";
                    break;
                case "plan":
                    input.VerificationMethod = "水模体测量";
                    input.Equipment = "模拟水箱与探测器";
                    input.MeasurementConditions = "模拟测量条件";
                    input.ExpectedValues = "测点 A 2 Gy";
                    input.StandardName = "演示规范（非临床）";
                    input.StandardVersion = "1";
                    input.AcceptanceCriteria = "测点剂量偏差";
                    input.AllowedDeviation = "演示阈值";
                    break;
                case "validate":
                    input.MeasuredValues = "测点 A 2 Gy";
                    input.ResultAttachment = "DEMO-REPORT";
                    break;
            }
            return input;
        }

        private static ScenarioCommand Copy(ScenarioCommand input)
        {
            return new ScenarioCommand
            {
                Id = input.Id,
                Version = input.Version,
                PatientId = input.PatientId,
                CaseId = input.CaseId,
                PlanId = input.PlanId,
                ResourceId = input.ResourceId,
                Kind = input.Kind,
                StartUtc = input.StartUtc,
                EndUtc = input.EndUtc,
                Text = input.Text,
                Diagnosis = input.Diagnosis,
                Reason = input.Reason,
                Attachment = input.Attachment,
                Outcome = input.Outcome,
                FailureType = input.FailureType,
                IdentityChecked = input.IdentityChecked,
                TreatmentSite = input.TreatmentSite,
                TotalDoseGy = input.TotalDoseGy,
                FractionDoseGy = input.FractionDoseGy,
                Fractions = input.Fractions,
                TreatmentMaterials = input.TreatmentMaterials,
                VerificationMethod = input.VerificationMethod,
                Equipment = input.Equipment,
                MeasurementConditions = input.MeasurementConditions,
                ExpectedValues = input.ExpectedValues,
                StandardName = input.StandardName,
                StandardVersion = input.StandardVersion,
                AcceptanceCriteria = input.AcceptanceCriteria,
                AllowedDeviation = input.AllowedDeviation,
                MeasuredValues = input.MeasuredValues,
                ResultAttachment = input.ResultAttachment,
                DraftJson = input.DraftJson,
                DraftVersion = input.DraftVersion,
                Available = input.Available,
            };
        }
    }

    public class OisRulesTests
    {
        readonly DateTime now = new DateTime(2030, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        OisState Create() =>
            new OisState()
            {
                Patients = new List<Patient> { new Patient { Id = 1 }, new Patient { Id = 2 } },
                Resources = new List<OisResource>
                {
                    new OisResource { Id = "clinician", Kind = "Clinician" },
                    new OisResource { Id = "a" },
                    new OisResource { Id = "b" },
                },
            };

        string Run(OisState w, string op, ScenarioCommand c, string role, DateTime? at = null) =>
            w.Apply(op, VerificationInputs.Complete(op, c), role.ToLowerInvariant(), role, at ?? now);

        (CareCase, TreatmentPlan) Plan(OisState w)
        {
            var id = Run(w, "case", new ScenarioCommand { PatientId = 1, Text = "模拟诊疗" }, "Clinician");
            var care = w.Cases.Single(x => x.Id == id);
            Run(
                w,
                "prescribe",
                new ScenarioCommand
                {
                    Id = id,
                    Version = care.Version,
                    Diagnosis = "模拟诊断",
                    Text = "模拟放疗处方",
                },
                "Clinician"
            );
            var planId = Run(
                w,
                "plan",
                new ScenarioCommand
                {
                    CaseId = id,
                    Version = care.Version,
                    ResourceId = "a",
                    Text = "计划 v1",
                },
                "Physicist"
            );
            return (care, w.Plans.Single(x => x.Id == planId));
        }

        Reservation Book(
            OisState w,
            string kind,
            string resource,
            string care = "",
            string plan = "",
            int patient = 1,
            int hour = 10
        )
        {
            if (kind == "Experiment")
            {
                var p = w.Plans.Single(x => x.Id == plan);
                if (p.ReviewedBy is null) Run(w, "review", new ScenarioCommand { Id = p.Id, Version = p.Version }, "Clinician");
            }
            var id = Run(
                w,
                "book",
                new ScenarioCommand
                {
                    Kind = kind,
                    ResourceId = resource,
                    CaseId = care,
                    PlanId = plan,
                    PatientId = patient,
                    StartUtc = now.Date.AddHours(hour),
                    EndUtc = now.Date.AddHours(hour + 1),
                },
                kind == "Experiment" ? "Clinician" : "Scheduler"
            );
            return w.Reservations.Single(x => x.Id == id);
        }

        void Release(OisState w, TreatmentPlan p)
        {
            Run(w, "review", new ScenarioCommand { Id = p.Id, Version = p.Version }, "Clinician");
            var exp = Book(w, "Experiment", "a", plan: p.Id, hour: 9);
            Run(
                w,
                "validate",
                new ScenarioCommand
                {
                    Id = exp.Id,
                    Version = exp.Version,
                    Outcome = "Passed",
                    Text = "模拟验证通过",
                },
                "Physicist",
                now.Date.AddHours(9)
            );
        }

        [Fact]
        public void Missing_clinical_details_and_unreviewed_experiments_are_rejected()
        {
            var w = Create();
            var id = Run(w, "case", new ScenarioCommand { PatientId = 1, Text = "缺少信息" }, "Clinician");
            Assert.Throws<BusinessException>(() => w.Apply("prescribe", new ScenarioCommand
            {
                Id = id,
                Version = 1,
                Diagnosis = "诊断",
                Text = "处方",
            }, "doctor", "Clinician", now));
            Assert.Empty(w.Prescriptions);
            var (_, p) = Plan(w);
            Assert.Throws<BusinessException>(() => w.Apply("book", new ScenarioCommand
            {
                Kind = "Experiment",
                PlanId = p.Id,
                ResourceId = "a",
                StartUtc = now.AddHours(1),
                EndUtc = now.AddHours(2),
            }, "doctor", "Clinician", now));
        }

        [Fact]
        public void Physical_failure_notifies_both_roles_and_retest_preserves_signed_attempt()
        {
            var w = Create();
            var (care, p) = Plan(w);
            var exp = Book(w, "Experiment", "a", plan: p.Id, hour: 9);
            Run(w, "validate", new ScenarioCommand
            {
                Id = exp.Id,
                Version = 1,
                Outcome = "Failed",
                FailureType = "Plan",
                Text = "摆位需调整",
            }, "Physicist", now.AddHours(1));
            Assert.Contains(w.Tasks, t => t.TargetId == p.Id && t.Role == "Clinician" && t.Reason.Contains("失败"));
            var task = w.Tasks.Last(t => t.TargetId == p.Id && t.Role == "Physicist");
            Run(w, "read-task", new ScenarioCommand { Id = task.Id, Version = task.Version }, "Physicist");
            Assert.Null(task.ResolvedBy);
            Assert.Throws<BusinessException>(() => Run(w, "resolve-task", new ScenarioCommand { Id = task.Id, Version = task.Version, Text = "已查看" }, "Physicist"));
            var treatment = Book(w, "Treatment", "a", care.Id, hour: 12);
            Assert.NotEmpty(w.Blockers(treatment));
            var retry = Book(w, "Experiment", "a", plan: p.Id, hour: 10);
            Run(w, "validate", new ScenarioCommand
            {
                Id = retry.Id,
                Version = 1,
                Outcome = "Passed",
                Text = "调整后通过",
            }, "Physicist", now.AddHours(2));
            Assert.Equal("Failed", w.Validations.Single(x => x.ReservationId == exp.Id).ValidationOutcome);
            Assert.Equal("Passed", w.Validations.Single(x => x.ReservationId == retry.Id).ValidationOutcome);
            Assert.Equal("摆位需调整", w.Validations.Single(x => x.ReservationId == exp.Id).ValidationNote);
            Assert.Empty(w.Blockers(treatment));
            Run(w, "resolve-task", new ScenarioCommand { Id = task.Id, Version = task.Version, Text = "重测通过" }, "Physicist");
            Assert.NotNull(task.ResolvedBy);
        }

        [Fact]
        public void Clinical_failure_requires_prescription_revision_or_termination()
        {
            var w = Create();
            var (care, p) = Plan(w);
            var exp = Book(w, "Experiment", "a", plan: p.Id, hour: 9);
            Run(w, "validate", new ScenarioCommand
            {
                Id = exp.Id,
                Version = 1,
                Outcome = "Failed",
                FailureType = "Clinical",
                Text = "临床要求需修订",
            }, "Physicist", now.AddHours(1));
            Assert.Throws<BusinessException>(() => Book(w, "Experiment", "a", plan: p.Id, hour: 10));
            Assert.Throws<BusinessException>(() => Run(w, "plan", new ScenarioCommand
            {
                CaseId = care.Id,
                Version = care.Version,
                ResourceId = "a",
                Text = "绕过修订",
                Reason = "重做",
            }, "Physicist"));
            var treatment = Book(w, "Treatment", "a", care.Id, hour: 12);
            Run(w, "terminate", new ScenarioCommand { Id = care.Id, Version = care.Version, Reason = "终止此方案" }, "Clinician");
            Assert.Contains("诊疗方案已终止", w.Blockers(treatment));
            Assert.Throws<BusinessException>(() => Run(w, "prescribe", new ScenarioCommand
            {
                Id = care.Id,
                Version = care.Version,
                Diagnosis = "诊断",
                Text = "处方",
                Reason = "重做",
            }, "Clinician"));
        }

        [Fact]
        public void Drafts_accept_incomplete_data_without_changing_release_and_are_versioned()
        {
            var w = Create();
            var (care, p) = Plan(w);
            Release(w, p);
            var version = care.Version;
            Run(w, "save-draft", new ScenarioCommand
            {
                CaseId = care.Id,
                Version = version,
                Kind = "prescribe",
                DraftJson = "{\"treatmentSite\":\"草稿\"}",
            }, "Clinician");
            var draft = Assert.Single(w.Drafts);
            Assert.Equal(version, care.Version);
            Assert.Equal("Passed", p.Validation);
            Assert.Throws<BusinessException>(() => Run(w, "save-draft", new ScenarioCommand
            {
                CaseId = care.Id,
                Version = version,
                Kind = "prescribe",
                DraftJson = "{}",
            }, "Clinician"));
            Run(w, "save-draft", new ScenarioCommand
            {
                CaseId = care.Id,
                Version = version,
                Kind = "prescribe",
                DraftJson = "{}",
                DraftVersion = draft.Version,
            }, "Clinician");
            Assert.Equal(2, draft.Version);
            Run(w, "note", new ScenarioCommand { Id = care.Id, Version = care.Version, Text = "仅补充备注" }, "Clinician");
            Assert.Equal("Passed", p.Validation);
        }

        [Theory]
        [InlineData(1, 60)]
        [InlineData(0, 16)]
        [InlineData(0, 0)]
        [InlineData(0, -15)]
        [InlineData(0, 255)]
        public void Booking_rejects_unaligned_or_invalid_duration(int minute, int duration)
        {
            var w = Create();
            var start = now.AddHours(1).AddMinutes(minute);
            Assert.Equal("invalid_time", Assert.Throws<BusinessException>(() => Run(w, "book",
                new ScenarioCommand
                {
                    Kind = "Consultation",
                    PatientId = 1,
                    ResourceId = "clinician",
                    StartUtc = start,
                    EndUtc = start.AddMinutes(duration),
                }, "Scheduler")).Code);
            Assert.Empty(w.Reservations);
        }

        [Fact]
        public void Release_is_independent_of_booking_and_requires_both_signatures()
        {
            var w = Create();
            var (care, plan) = Plan(w);
            var booking = Book(w, "Treatment", "a", care.Id);
            Assert.Contains("待诊疗审核", w.Blockers(booking));
            Assert.Throws<BusinessException>(() =>
                Run(
                    w,
                    "start",
                    new ScenarioCommand
                    {
                        Id = booking.Id,
                        Version = booking.Version,
                        IdentityChecked = true,
                        PlanId = w.Cases.Single().CurrentPlanId!,
                    },
                    "Clinician",
                    now.Date.AddHours(10)
                )
            );
            Release(w, plan);
            Assert.Empty(w.Blockers(booking));
            var id = Run(
                w,
                "start",
                new ScenarioCommand
                {
                    Id = booking.Id,
                    Version = booking.Version,
                    IdentityChecked = true,
                    PlanId = w.Cases.Single().CurrentPlanId!,
                },
                "Clinician",
                now.Date.AddHours(10)
            );
            var d = w.Deliveries.Single(x => x.Id == id);
            Run(
                w,
                "finish",
                new ScenarioCommand
                {
                    Id = d.Id,
                    Version = d.Version,
                    Outcome = "Completed",
                    Text = "完成",
                },
                "Clinician",
                now.Date.AddHours(10).AddMinutes(20)
            );
            Assert.Equal(plan.Id, d.PlanId);
            Assert.Equal("Completed", booking.Status);
        }

        [Fact]
        public void Revision_does_not_inherit_release_or_rewrite_delivery_history()
        {
            var w = Create();
            var (care, old) = Plan(w);
            Release(w, old);
            var booking = Book(w, "Treatment", "a", care.Id);
            var id = Run(
                w,
                "start",
                new ScenarioCommand
                {
                    Id = booking.Id,
                    Version = booking.Version,
                    IdentityChecked = true,
                    PlanId = w.Cases.Single().CurrentPlanId!,
                },
                "Clinician",
                now.Date.AddHours(10)
            );
            var d = w.Deliveries.Single(x => x.Id == id);
            Assert.Throws<BusinessException>(() =>
                Run(
                    w,
                    "plan",
                    new ScenarioCommand
                    {
                        CaseId = care.Id,
                        Version = care.Version,
                        ResourceId = "a",
                        Text = "v2",
                        Reason = "修订",
                    },
                    "Physicist"
                )
            );
            Run(
                w,
                "finish",
                new ScenarioCommand
                {
                    Id = d.Id,
                    Version = d.Version,
                    Outcome = "Completed",
                    Text = "完成",
                },
                "Clinician"
            );
            Run(
                w,
                "plan",
                new ScenarioCommand
                {
                    CaseId = care.Id,
                    Version = care.Version,
                    ResourceId = "a",
                    Text = "v2",
                    Reason = "修订",
                },
                "Physicist"
            );
            var next = Book(w, "Treatment", "a", care.Id, hour: 12);
            Assert.NotEmpty(w.Blockers(next));
            Assert.Equal(old.Id, d.PlanId);
            Assert.Equal("Passed", old.Validation);
            Assert.Throws<BusinessException>(() =>
                Run(w, "review", new ScenarioCommand { Id = old.Id, Version = old.Version }, "Clinician")
            );
        }

        [Fact]
        public void Patient_and_machine_conflicts_include_experiments_but_experiments_do_not_book_patients()
        {
            var w = Create();
            var (_, p) = Plan(w);
            var exp = Book(w, "Experiment", "a", plan: p.Id);
            Assert.Null(exp.PatientId);
            Book(w, "Consultation", "clinician");
            Assert.Equal(
                "slot_conflict",
                Assert
                    .Throws<BusinessException>(() => Book(w, "Treatment", "a", p.CaseId, hour: 10))
                    .Code
            );
            Assert.Equal(
                "slot_conflict",
                Assert
                    .Throws<BusinessException>(() => Book(w, "Treatment", "b", p.CaseId, hour: 10))
                    .Code
            );
            Book(w, "Treatment", "a", p.CaseId, hour: 11);
        }

        [Fact]
        public void Failed_plan_blocks_treatment_without_stopping_machine()
        {
            var w = Create();
            var (care, p) = Plan(w);
            var booking = Book(w, "Treatment", "a", care.Id);
            var exp = Book(w, "Experiment", "a", plan: p.Id, hour: 9);
            Run(
                w,
                "validate",
                new ScenarioCommand
                {
                    Id = exp.Id,
                    Version = exp.Version,
                    Outcome = "Failed",
                    FailureType = "Clinical",
                    Text = "需评估",
                },
                "Physicist",
                now.Date.AddHours(9)
            );
            Assert.True(w.Resources.Single(x => x.Id == "a").Available);
            Assert.Contains("物理验证未通过", w.Blockers(booking));
            Assert.Contains(w.Tasks, x => x.Role == "Clinician" && x.TargetId == p.Id);
            Assert.Contains(w.Tasks, x => x.Role == "Scheduler" && x.TargetId == booking.Id);
        }

        [Fact]
        public void Machine_recovery_does_not_release_failed_plan()
        {
            var w = Create();
            var (care, p) = Plan(w);
            var booking = Book(w, "Treatment", "a", care.Id);
            var exp = Book(w, "Experiment", "a", plan: p.Id, hour: 9);
            Run(
                w,
                "validate",
                new ScenarioCommand
                {
                    Id = exp.Id,
                    Version = exp.Version,
                    Outcome = "Failed",
                    FailureType = "Equipment",
                    Text = "设备异常",
                },
                "Physicist",
                now.Date.AddHours(9)
            );
            var m = w.Resources.Single(x => x.Id == "a");
            Assert.False(m.Available);
            Run(
                w,
                "machine",
                new ScenarioCommand
                {
                    Id = m.Id,
                    Version = m.Version,
                    Available = true,
                    Reason = "检修完成",
                },
                "Physicist"
            );
            Assert.Contains("物理验证未通过", w.Blockers(booking));
            Assert.Equal("Booked", booking.Status);
        }

        [Fact]
        public void Restore_rechecks_conflicts_and_stale_versions_are_rejected()
        {
            var w = Create();
            var r = Book(w, "Consultation", "clinician");
            Run(w, "cancel", new ScenarioCommand { Id = r.Id, Version = r.Version, Reason = "误操作" }, "Scheduler");
            Book(w, "Consultation", "clinician", patient: 2);
            Assert.Equal(
                "slot_conflict",
                Assert
                    .Throws<BusinessException>(() =>
                        Run(
                            w,
                            "restore",
                            new ScenarioCommand { Id = r.Id, Version = r.Version, Reason = "恢复" },
                            "Scheduler"
                        )
                    )
                    .Code
            );
            Assert.Equal("Cancelled", r.Status);
            Assert.Equal(
                "version_conflict",
                Assert
                    .Throws<BusinessException>(() =>
                        Run(w, "restore", new ScenarioCommand { Id = r.Id, Version = 1, Reason = "恢复" }, "Scheduler")
                    )
                    .Code
            );
        }

        [Fact]
        public void Physicist_signature_cannot_be_supplied_by_clinician()
        {
            var w = Create();
            var (_, p) = Plan(w);
            var exp = Book(w, "Experiment", "a", plan: p.Id, hour: 9);
            Assert.Equal(
                "forbidden",
                Assert
                    .Throws<BusinessException>(() =>
                        Run(
                            w,
                            "validate",
                            new ScenarioCommand
                            {
                                Id = exp.Id,
                                Version = exp.Version,
                                Outcome = "Passed",
                                Text = "通过",
                            },
                            "Clinician"
                        )
                    )
                    .Code
            );
        }

        [Fact]
        public void Interrupted_delivery_requires_manual_followup_and_cannot_be_overwritten()
        {
            var w = Create();
            var (care, p) = Plan(w);
            Release(w, p);
            var r = Book(w, "Treatment", "a", care.Id);
            var id = Run(
                w,
                "start",
                new ScenarioCommand
                {
                    Id = r.Id,
                    Version = r.Version,
                    IdentityChecked = true,
                    PlanId = w.Cases.Single().CurrentPlanId!,
                },
                "Clinician",
                now.Date.AddHours(10)
            );
            var d = w.Deliveries.Single(x => x.Id == id);
            Run(
                w,
                "finish",
                new ScenarioCommand
                {
                    Id = d.Id,
                    Version = d.Version,
                    Outcome = "Interrupted",
                    Text = "模拟中断",
                },
                "Clinician"
            );
            Assert.Contains(w.Tasks, x => x.TargetId == id && x.Role == "Clinician");
            Assert.Equal(2, w.Reservations.Count);
            Assert.Throws<BusinessException>(() =>
                Run(
                    w,
                    "finish",
                    new ScenarioCommand
                    {
                        Id = d.Id,
                        Version = d.Version,
                        Outcome = "Completed",
                        Text = "覆盖",
                    },
                    "Clinician"
                )
            );
        }

        [Fact]
        public void Experiment_restore_cannot_create_duplicate_validation_bookings()
        {
            var w = Create();
            var (_, p) = Plan(w);
            var old = Book(w, "Experiment", "a", plan: p.Id, hour: 9);
            Run(w, "cancel", new ScenarioCommand { Id = old.Id, Version = old.Version, Reason = "改排" }, "Clinician");
            Book(w, "Experiment", "a", plan: p.Id, hour: 11);
            Assert.Equal(
                "duplicate_experiment",
                Assert
                    .Throws<BusinessException>(() =>
                        Run(
                            w,
                            "restore",
                            new ScenarioCommand { Id = old.Id, Version = old.Version, Reason = "恢复" },
                            "Clinician"
                        )
                    )
                    .Code
            );
        }
    }

    public class OisDatabaseTests : IAsyncLifetime
    {
        readonly DatabaseFixture fixture = new DatabaseFixture();

        public Task InitializeAsync() => fixture.InitializeAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        async Task<object> Send(string op, ScenarioCommand c, string role = "Scheduler", string? key = null)
        {
            await using var db = fixture.Db();
            return await new CommandExecutor(db, TimeProvider.System).Execute(
                op,
                c,
                role.ToLowerInvariant(),
                role,
                key ?? Guid.NewGuid().ToString(),
                "test",
                default
            );
        }

        ScenarioCommand Booking() =>
            new ScenarioCommand
            {
                PatientId = 1,
                Kind = "Consultation",
                ResourceId = "clinician",
                StartUtc = new DateTimeOffset(2035,
                1,
                1,
                10,
                0,
                0,
                TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2035,
                1,
                1,
                11,
                0,
                0,
                TimeSpan.Zero),
            };

        sealed class Clock : TimeProvider
        {
            public DateTimeOffset Now = new DateTimeOffset(2030, 1, 1, 8, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => Now;
        }

        [Fact]
        public async Task Persisted_failure_revision_release_and_interruption_close_the_loop()
        {
            var clock = new Clock();
            async Task<string> Act(string op, ScenarioCommand input, string role)
            {
                await using var db = fixture.Db();
                var result = await new CommandExecutor(db, clock).Execute(
                    op,
                    VerificationInputs.Complete(op, input),
                    role.ToLowerInvariant(),
                    role,
                    Guid.NewGuid().ToString(),
                    "full-loop",
                    default
                );
                return System
                    .Text.Json.JsonSerializer.SerializeToElement(result)
                    .GetProperty("targetId")
                    .GetString()!;
            }
            var care = await Act("case", new ScenarioCommand { PatientId = 1, Text = "闭环" }, "Clinician");
            await Act(
                "prescribe",
                new ScenarioCommand
                {
                    Id = care,
                    Version = 1,
                    Diagnosis = "模拟诊断",
                    Text = "模拟处方",
                },
                "Clinician"
            );
            var p1 = await Act(
                "plan",
                new ScenarioCommand
                {
                    CaseId = care,
                    Version = 2,
                    ResourceId = "linac-a",
                    Text = "v1",
                },
                "Physicist"
            );
            await Act("review", new ScenarioCommand { Id = p1, Version = 1 }, "Clinician");
            var exp1 = await Act(
                "book",
                new ScenarioCommand
                {
                    Kind = "Experiment",
                    PlanId = p1,
                    ResourceId = "linac-a",
                    StartUtc = clock.Now.AddHours(1),
                    EndUtc = clock.Now.AddHours(1).AddMinutes(15),
                },
                "Clinician"
            );
            var treatment = await Act(
                "book",
                new ScenarioCommand
                {
                    Kind = "Treatment",
                    PatientId = 1,
                    CaseId = care,
                    ResourceId = "linac-a",
                    StartUtc = clock.Now.AddHours(2),
                    EndUtc = clock.Now.AddHours(3),
                },
                "Scheduler"
            );
            clock.Now = clock.Now.AddHours(1);
            await Act(
                "validate",
                new ScenarioCommand
                {
                    Id = exp1,
                    Version = 1,
                    Outcome = "Failed",
                    FailureType = "Plan",
                    Text = "计划需修订",
                },
                "Physicist"
            );
            var p2 = await Act(
                "plan",
                new ScenarioCommand
                {
                    CaseId = care,
                    Version = 3,
                    ResourceId = "linac-a",
                    Text = "v2",
                    Reason = "修订",
                },
                "Physicist"
            );
            await Act("review", new ScenarioCommand { Id = p2, Version = 1 }, "Clinician");
            var exp2 = await Act(
                "book",
                new ScenarioCommand
                {
                    Kind = "Experiment",
                    PlanId = p2,
                    ResourceId = "linac-a",
                    StartUtc = clock.Now.AddMinutes(15),
                    EndUtc = clock.Now.AddMinutes(30),
                },
                "Clinician"
            );
            clock.Now = clock.Now.AddMinutes(15);
            await Act(
                "validate",
                new ScenarioCommand
                {
                    Id = exp2,
                    Version = 1,
                    Outcome = "Passed",
                    Text = "模拟通过",
                },
                "Physicist"
            );
            clock.Now = clock.Now.AddMinutes(45);
            Assert.Equal(
                "plan_changed",
                (
                    await Assert.ThrowsAsync<BusinessException>(() =>
                        Act(
                            "start",
                            new ScenarioCommand
                            {
                                Id = treatment,
                                Version = 1,
                                PlanId = p1,
                                IdentityChecked = true,
                            },
                            "Clinician"
                        )
                    )
                ).Code
            );
            var delivery = await Act(
                "start",
                new ScenarioCommand
                {
                    Id = treatment,
                    Version = 1,
                    PlanId = p2,
                    IdentityChecked = true,
                },
                "Clinician"
            );
            await Act(
                "finish",
                new ScenarioCommand
                {
                    Id = delivery,
                    Version = 1,
                    Outcome = "Interrupted",
                    Text = "需人工评估",
                },
                "Clinician"
            );
            await using var verify = fixture.Db();
            Assert.Equal("Failed", (await verify.Set<TreatmentPlan>().FindAsync(p1))!.Validation);
            Assert.Equal("Passed", (await verify.Set<TreatmentPlan>().FindAsync(p2))!.Validation);
            Assert.Equal(p2, (await verify.Set<TreatmentDelivery>().FindAsync(delivery))!.PlanId);
            Assert.Equal(
                "Interrupted",
                (await verify.Set<TreatmentDelivery>().FindAsync(delivery))!.Outcome
            );
            Assert.True(
                await verify
                    .Set<OisTask>()
                    .AnyAsync(t => t.TargetId == delivery && t.Role == "Clinician")
            );
            Assert.Equal(3, await verify.Set<Reservation>().CountAsync());
        }

        [Fact]
        public async Task Concurrent_booking_has_one_winner_and_atomic_audit()
        {
            async Task<bool> Try()
            {
                try
                {
                    await Send("book", Booking());
                    return true;
                }
                catch (BusinessException)
                {
                    return false;
                }
            }
            var results = await Task.WhenAll(Try(), Try());
            Assert.Single(results, x => x);
            await using var db = fixture.Db();
            Assert.Equal(1, await db.Set<Reservation>().CountAsync());
            Assert.Equal(1, await db.Set<OisAudit>().CountAsync());
        }

        private ScenarioCommand ChangedBooking()
        {
            ScenarioCommand command = Booking();
            command.PatientId = 2;
            return command;
        }

        [Fact]
        public async Task Same_key_replays_once_and_different_payload_is_rejected()
        {
            await Task.WhenAll(
                Send("book", Booking(), key: "same"),
                Send("book", Booking(), key: "same")
            );
            Assert.Equal(
                "idempotency_conflict",
                (
                    await Assert.ThrowsAsync<BusinessException>(() =>
                        Send("book", ChangedBooking(), key: "same")
                    )
                ).Code
            );
            await using var db = fixture.Db();
            Assert.Equal(1, await db.Set<Reservation>().CountAsync());
            Assert.Equal(1, await db.Set<OisAudit>().CountAsync());
        }

        [Fact]
        public async Task Forbidden_and_failed_commands_leave_no_partial_changes()
        {
            await Send("book", Booking());
            await using var db = fixture.Db();
            var r = await db.Set<Reservation>().SingleAsync();
            await Assert.ThrowsAsync<BusinessException>(() =>
                Send("cancel", new ScenarioCommand { Id = r.Id, Version = r.Version, Reason = "禁止" }, "Physicist")
            );
            await Assert.ThrowsAsync<BusinessException>(() =>
                Send(
                    "reschedule",
                    new ScenarioCommand
                    {
                        Id = r.Id,
                        Version = r.Version,
                        ResourceId = "clinician",
                        StartUtc = Booking().StartUtc,
                        EndUtc = Booking().EndUtc,
                        Reason = "",
                    }
                )
            );
            await db.Entry(r).ReloadAsync();
            Assert.Equal("Booked", r.Status);
            Assert.Equal(1, r.Version);
            Assert.Equal(1, await db.Set<OisAudit>().CountAsync());
        }
    }
}
