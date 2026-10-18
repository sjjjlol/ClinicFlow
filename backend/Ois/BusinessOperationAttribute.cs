using System;

namespace ClinicFlow.Ois
{
    // Stable audit identity, not an operation dispatcher. HTTP paths may evolve independently.
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BusinessOperationAttribute : Attribute
    {
        public BusinessOperationAttribute(string name) => Name = name;
        public string Name { get; }
    }
}
