using System;
namespace ClinicFlow
{
    public class BusinessException : Exception
    {
        public BusinessException(string code, string message, int status = 409) : base(message)
        {
            Code = code;
            Status = status;
        }

        public string Code { get; }
        public int Status { get; }
    }
}
