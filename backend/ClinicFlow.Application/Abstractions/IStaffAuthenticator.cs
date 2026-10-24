using System.Threading;
using System.Threading.Tasks;
namespace ClinicFlow
{
    public interface IStaffAuthenticator
    {
        Task<StaffIdentity?> Authenticate(string username, string password, CancellationToken ct);
    }
    public sealed class StaffIdentity
    {
        public string Id { get; }
        public string Role { get; }
        public StaffIdentity(string id, string role) { Id = id; Role = role; }
    }
    public interface IDatabaseHealth
    {
        Task<bool> CanConnect(CancellationToken ct);
    }
}
