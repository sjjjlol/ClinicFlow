using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClinicFlow.Ois;
using ClinicFlow.Ois.Contracts;
using ClinicFlow.Ois.Modules.Clinical;
using ClinicFlow.Ois.Modules.Planning;
using ClinicFlow.Ois.Modules.Scheduling;
using ClinicFlow.Ois.Modules.Support;
using Xunit;

namespace ClinicFlow.Tests
{
    public sealed class ModuleBoundariesTests
    {
        [Fact]
        public void Modules_hold_only_owned_entities_and_contracts_for_other_business_modules()
        {
            var owners = new Dictionary<Type, Type>
            {
                [typeof(CareCase)] = typeof(ClinicalModule), [typeof(Prescription)] = typeof(ClinicalModule),
                [typeof(Patient)] = typeof(ClinicalModule),
                [typeof(TreatmentPlan)] = typeof(PlanningModule), [typeof(PhysicalValidation)] = typeof(PlanningModule),
                [typeof(Reservation)] = typeof(SchedulingModule), [typeof(TreatmentDelivery)] = typeof(SchedulingModule),
                [typeof(OisResource)] = typeof(SchedulingModule),
                [typeof(OisTask)] = typeof(SupportModule), [typeof(OisDraft)] = typeof(SupportModule),
            };
            var modules = owners.Values.Distinct().ToArray();
            foreach (var module in modules)
            {
                void Check(Type type)
                {
                    if (owners.TryGetValue(type, out var owner)) Assert.Equal(module, owner);
                    Assert.NotEqual(typeof(OisState), type);
                    Assert.NotEqual(typeof(ClinicDb), type);
                    Assert.NotEqual(typeof(OisSession), type);
                    if (type != typeof(SupportModule) && modules.Contains(type)) Assert.Equal(module, type);
                    foreach (var argument in type.GetGenericArguments()) Check(argument);
                }
                foreach (var type in new[] { module }.Concat(module.GetNestedTypes(BindingFlags.NonPublic)))
                {
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                        Check(field.FieldType);
                    foreach (var method in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        Check(method.ReturnType);
                        foreach (var parameter in method.GetParameters()) Check(parameter.ParameterType);
                        var body = method.GetMethodBody();
                        if (body != null) foreach (var local in body.LocalVariables) Check(local.LocalType);
                    }
                }
            }
        }

        [Fact]
        public void Cross_module_summaries_are_immutable_and_storage_is_not_a_public_command_contract()
        {
            foreach (var type in new[] { typeof(CaseSummary), typeof(PlanSummary), typeof(ExperimentSlot) })
                Assert.All(type.GetProperties(), property => Assert.Null(property.SetMethod));
            Assert.False(typeof(OisState).IsPublic);
            foreach (var type in new[] { typeof(CareCase), typeof(Prescription), typeof(TreatmentPlan), typeof(Reservation), typeof(PhysicalValidation), typeof(TreatmentDelivery) })
                Assert.All(type.GetProperties(), property => Assert.False(property.SetMethod?.IsPublic ?? false));
            Assert.Null(typeof(Reservation).GetProperty("ValidationOutcome"));
        }
    }
}
