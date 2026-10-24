using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace ClinicFlow
{
    // ===== 演示用身份认证：Cookie 会话 + 角色 claims（对照 Spring Security 表单登录）=====
    public static class Identity
    {
        // Seed：首次启动写入三个演示账号（每角色一个），密码取自环境变量 DEMO_PASSWORD。
        public static Task SignIn(StaffIdentity user, HttpContext ctx)
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
