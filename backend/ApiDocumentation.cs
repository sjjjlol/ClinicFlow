using Microsoft.OpenApi;

namespace ClinicFlow;

// ===== OpenAPI 定制（≈ springdoc-openapi 的 OpenApiCustomizer）=====
public static class ApiDocumentation
{
    // 扩展 IServiceCollection 的注册方法，Program.cs 里 builder.Services.AddClinicOpenApi() 调用。
    public static void AddClinicOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
            // AddOperationTransformer：遍历所有端点统一补充文档（lambda 三参数：操作、上下文、取消令牌）。
            options.AddOperationTransformer(
                (operation, context, ct) =>
                {
                    var path = context.Description.RelativePath ?? "";
                    operation.Description =
                        "Same-origin demo session cookie. Errors: 400 invalid request, 401 unauthenticated, 403 role denied, 404 missing object, 409 business conflict. Responses include X-Correlation-ID.";
                    if (context.Description.HttpMethod == "POST")
                    {
                        // ??= ：null 合并赋值——为 null 才赋新值（≈ if (x == null) x = ...）。
                        // [] 空集合表达式：新建空参数列表。
                        operation.Parameters ??= [];
                        // POST 统一补 CSRF 头参数说明（该头由中间件校验，签名里看不到）。
                        operation.Parameters.Add(
                            new OpenApiParameter
                            {
                                Name = "X-CSRF-TOKEN",
                                In = ParameterLocation.Header,
                                Required = true,
                                Description =
                                    "Request token from GET /api/auth/csrf; refresh after login/logout.",
                                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                            }
                        );
                        // StartsWith ≈ Java String.startsWith。
                        if (path.StartsWith("api/appointments") || path.StartsWith("api/sync"))
                            operation.Parameters.Add(
                                new OpenApiParameter
                                {
                                    Name = "Idempotency-Key",
                                    In = ParameterLocation.Header,
                                    Required = true,
                                    Description =
                                        "Same subject/operation/key plus same canonical body replays original success. Mutation body Version must match current appointment.",
                                    Schema = new OpenApiSchema
                                    {
                                        Type = JsonSchemaType.String,
                                        MaxLength = 128,
                                    },
                                }
                            );
                    }
                    return Task.CompletedTask; // 转换器要求返回 Task：无异步操作时返回已完成任务
                }
            )
        );
}
