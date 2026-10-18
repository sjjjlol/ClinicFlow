using ClinicFlow.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicFlow.Hosting
{
    public static class WebApplicationExtensions
    {
        public static WebApplication UseClinicPipeline(this WebApplication app)
        {
            app.UseMiddleware<ResponseHeadersMiddleware>();
            app.UseMiddleware<ApiExceptionMiddleware>();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            app.UseMiddleware<CsrfMiddleware>();
            app.MapControllers();
            app.MapOpenApi("/api/openapi/{documentName}.json").RequireAuthorization("ois");
            app.MapFallbackToFile("/", "index.html");
            return app;
        }
    }
}
