using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ClinicFlow
{
    // ===== 演示用身份认证：Cookie 会话 + 角色 claims（对照 Spring Security 表单登录）=====
    public static class Identity
    {
        // Seed：首次启动写入三个演示账号（每角色一个），密码取自环境变量 DEMO_PASSWORD。
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

        public static Task SignIn(DemoUser user, HttpContext ctx)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Id),
                new Claim(ClaimTypes.Role, user.Role),
            };
            return ctx.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(
                    new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
                )
            );
        }

        static readonly string[] Roles = new[] { "Scheduler", "Clinician", "Physicist" };

        public static bool IsStaffRole(string role) => Roles.Contains(role);

        public static bool IsStaff(ClaimsPrincipal principal) => Roles.Any(principal.IsInRole);
    }
}
