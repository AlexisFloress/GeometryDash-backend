using System.ComponentModel.DataAnnotations;
using GeometryDash.Api.Data;
using GeometryDash.Api.Domain;
using GeometryDash.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GeometryDash.Api.Endpoints;

public sealed record RegisterRequest(string UserName, string DisplayName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record ProfileRequest(string DisplayName);
public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Identidad");
        auth.MapPost("/register", async (RegisterRequest req, UserManager<AppUser> users, AppDbContext db) => {
            if (string.IsNullOrWhiteSpace(req.UserName) || req.UserName.Length is < 3 or > 30 ||
                string.IsNullOrWhiteSpace(req.DisplayName) || req.DisplayName.Trim().Length > 60 ||
                string.IsNullOrWhiteSpace(req.Email) || req.Email.Length > 254 || !new EmailAddressAttribute().IsValid(req.Email) ||
                req.Password is null || req.Password.Length is < 10 or > 128)
                throw new ApiError(422, "REGISTRATION_INVALID", "Revisa usuario (3–30), nombre (1–60), correo y contraseña (10–128).");
            await using var tx = await db.Database.BeginTransactionAsync();
            var user = new AppUser { Id = Guid.NewGuid(), UserName = req.UserName.Trim(), Email = req.Email.Trim(), DisplayName = req.DisplayName.Trim() };
            var result = await users.CreateAsync(user, req.Password);
            if (!result.Succeeded) throw new ApiError(422, "REGISTRATION_INVALID", "No se pudo registrar la cuenta.", result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
            var role = await users.AddToRoleAsync(user, "User");
            if (!role.Succeeded) throw new InvalidOperationException("No se pudo asignar el rol User.");
            await tx.CommitAsync();
            return Results.Created("/api/v1/me", SessionService.Profile(user));
        }).RequireRateLimiting("register").WithSummary("Crear una cuenta; el servidor asigna el rol User.");

        auth.MapPost("/login", async (LoginRequest req, HttpContext ctx, UserManager<AppUser> users, AppDbContext db, SessionService sessions) => {
            sessions.RequireOrigin(ctx);
            var user = string.IsNullOrWhiteSpace(req.Email) ? null : await users.FindByEmailAsync(req.Email.Trim());
            if (user is null || user.Status != "Active" || await users.IsLockedOutAsync(user) || req.Password is null || !await users.CheckPasswordAsync(user, req.Password))
            {
                if (user is not null && !await users.IsLockedOutAsync(user)) await users.AccessFailedAsync(user);
                throw new ApiError(401, "INVALID_CREDENTIALS", "Credenciales inválidas.");
            }
            await users.ResetAccessFailedCountAsync(user);
            return Results.Ok(await sessions.Issue(ctx, user));
        }).RequireRateLimiting("login").WithSummary("Obtener Bearer de 15 minutos y cookie HttpOnly de renovación.");

        auth.MapPost("/refresh", async (HttpContext ctx, AppDbContext db, SessionService sessions) => {
            sessions.RequireOrigin(ctx);
            var raw = ctx.Request.Cookies[SessionService.CookieName];
            if (string.IsNullOrEmpty(raw) || raw.Length > 128) throw new ApiError(401, "SESSION_INVALID", "Sesión inválida.");
            var hash = SessionService.TokenHash(raw);
            await using var tx = await db.Database.BeginTransactionAsync();
            // Serialize the whole family on the account row, including replay detection.
            var sessionUserId = await db.Sessions.Where(s => s.RefreshTokenHash == hash).Select(s => (Guid?)s.UserId).SingleOrDefaultAsync();
            if (sessionUserId is null) throw new ApiError(401, "SESSION_INVALID", "Sesión inválida.");
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {sessionUserId.Value} FOR UPDATE").SingleAsync();
            var old = await db.Sessions.SingleAsync(s => s.RefreshTokenHash == hash);
            if (old.RevokedAt is not null)
            {
                await db.Sessions.Where(s => s.FamilyId == old.FamilyId && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
                await tx.CommitAsync();
                throw new ApiError(401, "REFRESH_REUSED", "La sesión fue revocada. Inicia sesión otra vez.");
            }
            if (old.ExpiresAt <= DateTimeOffset.UtcNow || user.Status != "Active") throw new ApiError(401, "SESSION_EXPIRED", "Sesión vencida o inactiva.");
            var response = await sessions.Issue(ctx, user, old);
            await tx.CommitAsync();
            return Results.Ok(response);
        }).RequireRateLimiting("login").WithSummary("Rotar el refresh token; reutilizarlo revoca toda su familia.");

        auth.MapPost("/logout", async (HttpContext ctx, AppDbContext db, SessionService sessions) => {
            sessions.RequireOrigin(ctx);
            if (ctx.Request.Cookies[SessionService.CookieName] is { } token)
            {
                var hash = SessionService.TokenHash(token);
                await using var tx = await db.Database.BeginTransactionAsync();
                var uid = await db.Sessions.Where(s => s.RefreshTokenHash == hash).Select(s => (Guid?)s.UserId).SingleOrDefaultAsync();
                if (uid is not null)
                {
                    await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {uid.Value} FOR UPDATE").SingleAsync();
                    var family = await db.Sessions.Where(s => s.RefreshTokenHash == hash).Select(s => s.FamilyId).SingleAsync();
                    await db.Sessions.Where(s => s.FamilyId == family && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
                }
                await tx.CommitAsync();
            }
            ctx.Response.Cookies.Delete(SessionService.CookieName, sessions.CookieOptions());
            return Results.NoContent();
        }).RequireRateLimiting("login");
        app.MapGet("/api/v1/me", async (HttpContext ctx, AppDbContext db) => SessionService.Profile(await db.Users.SingleAsync(u => u.Id == ctx.UserId())))
            .RequireAuthorization().WithTags("Identidad");
        app.MapPatch("/api/v1/me", async (ProfileRequest req, HttpContext ctx, AppDbContext db) => {
            if (string.IsNullOrWhiteSpace(req.DisplayName) || req.DisplayName.Trim().Length > 60) throw new ApiError(422, "PROFILE_INVALID", "Nombre de 1 a 60 caracteres.");
            var user = await db.Users.SingleAsync(u => u.Id == ctx.UserId());
            user.DisplayName = req.DisplayName.Trim(); await db.SaveChangesAsync();
            return Results.Ok(SessionService.Profile(user));
        }).RequireAuthorization().RequireRateLimiting("write").WithTags("Identidad");
    }
}
