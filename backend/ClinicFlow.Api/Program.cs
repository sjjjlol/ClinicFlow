using ClinicFlow.Hosting;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddClinicServices(builder.Configuration, builder.Environment);

await using var app = builder.Build();
await app.InitializeClinicDatabaseAsync();
app.UseClinicMiddleware();
app.MapClinicEndpoints();
await app.RunAsync();
