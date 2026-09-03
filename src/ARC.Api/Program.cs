using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ARC.Agents.DependencyInjection;
using ARC.Api.Auth;
using ARC.Api.Configuration;
using ARC.Api.Middleware;
using ARC.Data.Configuration;
using ARC.Data.DependencyInjection;
using ARC.Knowledge.DependencyInjection;
using ARC.Api.Services;
using ARC.Tools.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddArcKeyVault();
builder.Services.Configure<ArcApiOptions>(builder.Configuration.GetSection(ArcApiOptions.SectionName));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ArcExceptionHandler>();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var apiOptions = builder.Configuration.GetSection(ArcApiOptions.SectionName).Get<ArcApiOptions>() ?? new();
if (!string.IsNullOrWhiteSpace(apiOptions.JwtAuthority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = apiOptions.JwtAuthority;
            options.Audience = apiOptions.JwtAudience;
            options.MapInboundClaims = false;
        });
    builder.Services.AddAuthorization();
}

builder.Services.AddArcData(builder.Configuration);
builder.Services.AddArcKnowledge(builder.Configuration);
builder.Services.AddArcTools(builder.Configuration);
builder.Services.AddArcLlm(builder.Configuration);
builder.Services.AddArcAgents();
builder.Services.AddSingleton<ChatSessionStore>();
builder.Services.AddSingleton<ChatOrchestrator>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseDefaultFiles();
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = ctx =>
        {
            var path = ctx.Context.Request.Path.Value ?? "";
            if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            }
        }
    });
}
else
{
    app.UseStaticFiles();
}
if (!string.IsNullOrWhiteSpace(apiOptions.JwtAuthority))
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseMiddleware<ArcActorMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTimeOffset.UtcNow }));
app.MapControllers();
app.Run();
