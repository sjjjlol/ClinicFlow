using System.Collections.Generic;
using System.Linq;
using ClinicFlow.Controllers;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace ClinicFlow
{
    // ===== OpenAPI 定制（≈ springdoc-openapi 的 OpenApiCustomizer）=====
    public static class ApiDocumentation
    {
        // 扩展 IServiceCollection 的注册方法，Hosting/ServiceCollectionExtensions.AddClinicServices 里 services.AddClinicOpenApi() 调用。
        public static void AddClinicOpenApi(this IServiceCollection services) =>
            services.AddOpenApi(options =>
                // AddOperationTransformer：遍历所有端点统一补充文档（lambda 三参数：操作、上下文、取消令牌）。
                options.AddOperationTransformer(
                    (operation, context, ct) =>
                    {
                        operation.Description =
                            "Same-origin demo session cookie. Errors: 400 invalid request, 401 unauthenticated, 403 role denied, 404 missing object, 409 business conflict. Responses include X-Correlation-ID.";
                        if (context.Description.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE")
                        {
                            if (operation.Parameters == null)
                                operation.Parameters = new List<IOpenApiParameter>();
                            // 写操作统一补 CSRF 头参数说明（该头由中间件校验，签名里看不到）。
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
                            // Endpoint metadata keeps documentation independent of route naming.
                            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotentAttribute>().Any())
                                operation.Parameters.Add(
                                    new OpenApiParameter
                                    {
                                        Name = "Idempotency-Key",
                                        In = ParameterLocation.Header,
                                        Required = true,
                                        Description =
                                            "Same actor/role/key and command replays the original success. A changed command conflicts. Version must match the current target.",
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
}
