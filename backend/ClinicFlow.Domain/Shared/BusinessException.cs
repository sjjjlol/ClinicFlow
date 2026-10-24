using System;
namespace ClinicFlow
{
    public enum BusinessErrorKind { InvalidInput, Forbidden, NotFound, Conflict }

    public class BusinessException : Exception
    {
        public BusinessException(string code, string message, BusinessErrorKind kind = BusinessErrorKind.Conflict) : base(message)
        {
            Code = code;
            Kind = kind;
        }

        public string Code { get; }
        public BusinessErrorKind Kind { get; }
    }
}
