using Microsoft.AspNetCore.Antiforgery;

namespace ClinicFlow.Middleware
{
    public sealed class CsrfMiddleware
    {
        private readonly RequestDelegate next;

        public CsrfMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext ctx, IAntiforgery antiforgery)
        {
            // HttpMethods.IsPost(...) 静态工具方法 ≈ "POST".equals(method)。
            if (
                HttpMethods.IsPost(ctx.Request.Method)
                || HttpMethods.IsPut(ctx.Request.Method)
                || HttpMethods.IsDelete(ctx.Request.Method)
            )
            {
                try
                {
                    // InvokeAsync 参数由当前请求的 DI 作用域提供。
                    await antiforgery.ValidateRequestAsync(ctx);
                }
                catch (AntiforgeryValidationException)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsJsonAsync(
                        new { code = "csrf", message = "安全令牌已过期，请刷新页面" }
                    );
                    return; // 不调 next() → 短路，请求到此为止
                }
            }
            await next(ctx);
        }
    }
}
