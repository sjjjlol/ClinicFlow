using System;

namespace ClinicFlow.Controllers
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class IdempotentAttribute : Attribute { }
}
