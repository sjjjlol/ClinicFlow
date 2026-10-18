namespace ClinicFlow.Middleware
{
    public sealed class ResponseHeadersMiddleware
    {
        private readonly RequestDelegate next;

        public ResponseHeadersMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            // ctx.TraceIdentifier ≈ MDC 里的 traceId；挂到响应头便于排障。
            ctx.Response.Headers["X-Correlation-ID"] = ctx.TraceIdentifier;
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            await next(ctx);
        }
    }
}
