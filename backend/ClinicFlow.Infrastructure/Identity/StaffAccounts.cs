using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
namespace ClinicFlow
{
    public sealed class StaffAuthenticator : IStaffAuthenticator
    {
        private readonly ClinicDb db;
        public StaffAuthenticator(ClinicDb db) => this.db = db;
        public async Task<StaffIdentity?> Authenticate(string username, string password, CancellationToken ct)
        {
            var user = await db.Users.FindAsync(new object[] { username.Trim().ToLowerInvariant() }, ct);
            if (user == null || !new[] { "Scheduler", "Clinician", "Physicist" }.Contains(user.Role)
                || new PasswordHasher<DemoUser>().VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
                return null;
            return new StaffIdentity(user.Id, user.Role);
        }
    }
    internal static class StaffAccounts
    {
        public static async Task Seed(ClinicDb db, IConfiguration config)
        {
            var existing = await db.Users.Select(x => x.Id).ToListAsync();
            var roles = new[] { "Scheduler", "Clinician", "Physicist" };
            if (roles.All(role => existing.Contains(role.ToLowerInvariant())))
                return;
            var password =
                config["DEMO_PASSWORD"]
                ?? throw new InvalidOperationException("DEMO_PASSWORD required for initial seed");
            if (password.Length < 8)
                throw new InvalidOperationException("Demo password requires 8+ characters");
            // new[] { ... }：隐式类型数组（≈ Java new String[]{...}）。
            foreach (var role in roles)
            {
                if (existing.Contains(role.ToLowerInvariant()))
                    continue;
                // ToLowerInvariant ≈ toLowerCase(Locale.ROOT)。
                var user = new DemoUser { Id = role.ToLowerInvariant(), Role = role };
                // PasswordHasher<T>：ASP.NET Identity 的 PBKDF2 密码哈希（≈ Spring 的 BCryptPasswordEncoder）。
                user.PasswordHash = new PasswordHasher<DemoUser>().HashPassword(user, password);
                db.Users.Add(user);
            }
            await db.SaveChangesAsync();
        }

    }
}
