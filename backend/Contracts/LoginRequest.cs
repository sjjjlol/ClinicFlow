namespace ClinicFlow.Contracts
{
    public sealed class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
