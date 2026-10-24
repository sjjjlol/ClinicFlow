using System;
using System.Collections.Generic;
using System.Linq;
using ClinicFlow.Ois.Contracts;
using ClinicFlow.Application.Commands;
using static ClinicFlow.Ois.DomainRules;

namespace ClinicFlow.Ois.Modules.Support
{
    public sealed partial class SupportModule
    {
        private readonly List<OisTask> tasks;
        private readonly List<OisDraft> drafts;
        private readonly IClinicalContext clinical;
        private readonly Func<IPlanningContext> planning;
        internal SupportModule(List<OisTask> tasks, List<OisDraft> drafts, IClinicalContext clinical, Func<IPlanningContext> planning)
        { this.tasks = tasks; this.drafts = drafts; this.clinical = clinical; this.planning = planning; }
        internal void AddTask(string target, string role, string reason, DateTime now) =>
            tasks.Add(OisTask.Create(target, role, reason, now));
        internal void RemoveDraft(string caseId, string actor, string operation) =>
            drafts.RemoveAll(x => x.CaseId == caseId && x.Actor == actor && x.Operation == operation);
    }
}
