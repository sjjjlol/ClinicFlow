using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace ClinicFlow.Middleware
{
    public sealed class ApiExceptionMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<ApiExceptionMiddleware> logger;

        public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
        {
            this.next = next;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            try
            {
                await next(ctx);
            }
            catch (BusinessException ex) // 业务异常 → 约定状态码（默认 409）
            {
                ctx.Response.StatusCode = ex.Kind switch
                {
                    BusinessErrorKind.InvalidInput => 400,
                    BusinessErrorKind.Forbidden => 403,
                    BusinessErrorKind.NotFound => 404,
                    _ => 409,
                };
                // new { ... } 匿名类型：编译器生成只读类，直接 JSON 序列化为错误响应。
                await ctx.Response.WriteAsJsonAsync(
                    new
                    {
                        code = ex.Code,
                        message = ex.Message,
                        correlationId = ctx.TraceIdentifier,
                    }
                );
            }
            catch (BadHttpRequestException) // JSON 解析失败等 → 400
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsJsonAsync(
                    new
                    {
                        code = "invalid_request",
                        message = "请求格式不正确",
                        correlationId = ctx.TraceIdentifier,
                    }
                );
            }
            // catch ... when：C# 独有的异常过滤器——只有 when 条件为真才捕获。
            // 这里把 OperationCanceledException（客户端断开/停机取消）排除在 500 之外。
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                logger.LogError(ex, "Request failed {CorrelationId}", ctx.TraceIdentifier);
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(
                    new
                    {
                        code = "internal_error",
                        message = "操作未完成，请使用原请求标识重试",
                        correlationId = ctx.TraceIdentifier,
                    }
                );
            }
        }
    }
}
