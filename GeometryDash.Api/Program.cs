using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using GeometryDash.Api.Data;
using GeometryDash.Api.Domain;
using GeometryDash.Api.Documentation;
using GeometryDash.Api.Endpoints;
using GeometryDash.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
// Console works for both the API and CLI without Windows Event Log privileges.
builder.Logging.ClearProviders().AddConsole();
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();

var connection = builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Configura ConnectionStrings:Database en appsettings.Local.json o variables de entorno.");
var key = builder.Configuration["Jwt:Key"] ?? "";

if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Configura Jwt:Key con al menos 32 bytes aleatorios.");

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 600 * 1024);
builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow; o.SerializerOptions.MaxDepth = 32; });
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddIdentityCore<AppUser>(o => {
    o.User.RequireUniqueEmail = true; o.Password.RequiredLength = 10; o.Password.RequireNonAlphanumeric = false;
    o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddScoped<SessionService>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>(); builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1", options => options.AddOperationTransformer<RequestDocumentation>());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
    o.TokenValidationParameters = new() {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(10)
    };
    o.Events = new() {
        OnTokenValidated = async ctx => {
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            if (!Guid.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) || !Guid.TryParse(ctx.Principal?.FindFirstValue("sid"), out var sid)) { ctx.Fail("Token inválido."); return; }
            var sv = ctx.Principal.FindFirstValue("sv"); var now = DateTimeOffset.UtcNow;
            var valid = await db.Users.AnyAsync(u => u.Id == uid && u.Status == "Active" && u.SecurityVersion.ToString() == sv)
                && await db.Sessions.AnyAsync(s => s.Id == sid && s.UserId == uid && s.RevokedAt == null && s.ExpiresAt > now);
            if (!valid) ctx.Fail("Sesión revocada o cuenta inactiva.");
        }
    };
});

builder.Services.AddAuthorization(o => o.AddPolicy("Administrator", p => p.RequireRole("Administrator")));
var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("ETag", "Retry-After")));
builder.Services.AddRateLimiter(o => {
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    foreach (var (name, limit, window) in new[] { ("login", 5, 60), ("register", 3, 3600), ("write", 30, 60), ("publish", 5, 60), ("attempt", 60, 60) })
        o.AddPolicy(name, ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = limit, Window = TimeSpan.FromSeconds(window), QueueLimit = 0 }));
    o.OnRejected = async (ctx, ct) => {
        ctx.HttpContext.Response.StatusCode = 429;
        var seconds = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? Math.Ceiling(retry.TotalSeconds) : 60;
        ctx.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { type = "about:blank", title = "Límite de solicitudes alcanzado.", status = 429, code = "RATE_LIMITED", traceId = ctx.HttpContext.TraceIdentifier }, ct);
    };
});
var app = builder.Build();
// Migraciones explícitas: el arranque normal no modifica el esquema.
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await DatabaseMigrations.Apply(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var role in new[] { "User", "Administrator" })
        if (!await roles.RoleExistsAsync(role)) { var result = await roles.CreateAsync(new(role)); if (!result.Succeeded) throw new InvalidOperationException("No se pudo crear el rol."); }
    return;
}
if (args.Contains("--grant-admin"))
{
    var email = Environment.GetEnvironmentVariable("GD_ADMIN_EMAIL") ?? throw new InvalidOperationException("Configura GD_ADMIN_EMAIL con una cuenta existente.");
    await using var scope = app.Services.CreateAsyncScope(); var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("Registra la cuenta primero.");
    var result = await users.AddToRoleAsync(user, "Administrator"); if (!result.Succeeded) throw new InvalidOperationException("No se pudo asignar el rol.");
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Audit.Add(new() { Action = "AdministratorProvisioned", TargetType = "User", TargetId = user.Id, Reason = "CLI de despliegue", CorrelationId = Guid.NewGuid().ToString() });
    await db.SaveChangesAsync(); Console.WriteLine("Rol asignado. Inicia sesión nuevamente."); return;
}
app.UseExceptionHandler();
app.Use(async (ctx, next) => {
    var clock = Stopwatch.StartNew(); ctx.Response.Headers["X-Correlation-Id"] = ctx.TraceIdentifier;
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff"; ctx.Response.Headers["Referrer-Policy"] = "same-origin";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; script-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.Headers.CacheControl = "no-store";
    try { await next(ctx); }
    finally { app.Logger.LogInformation("{Method} {Path} {Status} {ElapsedMs}ms {TraceId}", ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, clock.ElapsedMilliseconds, ctx.TraceIdentifier); }
});
if (!app.Environment.IsDevelopment()) { app.UseHsts(); app.UseHttpsRedirection(); }
app.UseStatusCodePages(async status => {
    var ctx = status.HttpContext;
    await ctx.Response.WriteAsJsonAsync(new { type = "about:blank", title = "Solicitud rechazada.", status = ctx.Response.StatusCode, code = $"HTTP_{ctx.Response.StatusCode}", traceId = ctx.TraceIdentifier });
});
app.UseDefaultFiles(); app.UseStaticFiles(); app.UseRouting(); app.UseCors(); app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization();
app.MapOpenApi("/openapi/{documentName}.json").AllowAnonymous();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();
app.MapGet("/health/ready", async (AppDbContext db) => { try { return await db.Database.CanConnectAsync() && await db.Roles.AnyAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503); } catch { return Results.StatusCode(503); } }).ExcludeFromDescription();
app.MapAuth(); app.MapLevels(); app.MapInteractions(); app.MapAdmin();
app.MapGet("/docs/backend", () => Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html; charset=utf-8"))
    .AllowAnonymous().WithSummary("Abre la documentación pública del backend, donde cualquier persona puede leer qué hace cada operación y usar el probador al final de la página.");
app.Run();
public partial class Program;
