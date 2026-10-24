using System;
using System.Collections.Generic;
using System.Linq;
using ClinicFlow.Ois.Modules.Clinical;
using ClinicFlow.Ois.Modules.Planning;
using ClinicFlow.Ois.Modules.Scheduling;
using ClinicFlow.Ois.Modules.Support;

namespace ClinicFlow.Ois
{
    public sealed partial class OisSession
    {
        private readonly OisState state;
        public ClinicalModule Clinical { get; }
        public PlanningModule Planning { get; }
        public SchedulingModule Scheduling { get; }
        public SupportModule Support { get; }
        // Factories close synchronous dependencies; no factory is invoked during construction.
        // One session lives entirely within one command transaction (or one query snapshot).
        internal OisSession(OisState state)
        {
            this.state = state;
            Clinical = new ClinicalModule(state.Cases, state.Prescriptions, state.Patients, () => Scheduling!, () => Support!);
            Support = new SupportModule(state.Tasks, state.Drafts, Clinical, () => Planning!);
            Planning = new PlanningModule(state.Plans, state.Validations, Clinical, () => Scheduling!, Support);
            Scheduling = new SchedulingModule(state.Reservations, state.Deliveries, state.Resources, Clinical, Planning, Support);
        }
    }
}
