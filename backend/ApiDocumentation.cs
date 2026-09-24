using Microsoft.OpenApi;

namespace ClinicFlow;

public static class ApiDocumentation
{
    public static void AddClinicOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
            options.AddOperationTransformer(
                (operation, context, ct) =>
                {
                    var path = context.Description.RelativePath ?? "";
                    operation.Description =
                        "Same-origin demo session cookie. Errors: 400 invalid request, 401 unauthenticated, 403 role denied, 404 missing object, 409 business conflict. Responses include X-Correlation-ID.";
                    if (context.Description.HttpMethod == "POST")
                    {
                        operation.Parameters ??= [];
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
                    return Task.CompletedTask;
                }
            )
        );
}
