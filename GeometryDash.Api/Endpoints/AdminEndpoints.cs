using GeometryDash.Api.Data;
using GeometryDash.Api.Domain;
using GeometryDash.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace GeometryDash.Api.Endpoints;
public sealed record ReasonRequest(string Reason);
public static class AdminEndpoints
{
    public static void MapAdmin(this WebApplication app)
    {
        var a = app.MapGroup("/api/v1/admin").RequireAuthorization("Administrator").RequireRateLimiting("write").WithTags("Administración");
        a.MapPost("/levels/{id:guid}/withdraw", (Guid id, ReasonRequest r, HttpContext ctx, AppDbContext db) => ChangeLevel(id, r, ctx, db, false)).WithSummary("Oculta un nivel publicado para que deje de estar disponible al público. Solo un administrador puede hacerlo y debe indicar el motivo.");
        a.MapPost("/levels/{id:guid}/restore", (Guid id, ReasonRequest r, HttpContext ctx, AppDbContext db) => ChangeLevel(id, r, ctx, db, true)).WithSummary("Vuelve a mostrar un nivel retirado por administración. Solo un administrador puede hacerlo y debe indicar el motivo.");
        a.MapPost("/users/{id:guid}/suspend", (Guid id, ReasonRequest r, HttpContext ctx, AppDbContext db) => ChangeUser(id, r, ctx, db, false)).WithSummary("Suspende el acceso de una cuenta y cierra sus sesiones. Solo un administrador puede hacerlo y debe indicar el motivo.");
        a.MapPost("/users/{id:guid}/activate", (Guid id, ReasonRequest r, HttpContext ctx, AppDbContext db) => ChangeUser(id, r, ctx, db, true)).WithSummary("Habilita de nuevo una cuenta suspendida; la persona deberá iniciar sesión otra vez. Solo un administrador puede hacerlo y debe indicar el motivo.");
        a.MapGet("/audit", async (AppDbContext db, int? page, int? pageSize) => {
            var (p, s) = RequestRules.Paging(page, pageSize); return Results.Ok(new { items = await db.Audit.AsNoTracking().OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).Skip((p - 1) * s).Take(s).ToListAsync(), page = p, pageSize = s, totalCount = await db.Audit.CountAsync() });
        }).WithSummary("Muestra el historial de acciones de seguridad y administración para revisar qué ocurrió y cuándo. Solo está disponible para administradores.");
    }
    private static void Reason(ReasonRequest r) { if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Trim().Length > 1000) throw new ApiError(422, "REASON_REQUIRED", "Motivo de 1 a 1000 caracteres requerido."); }
    private static async Task<IResult> ChangeLevel(Guid id, ReasonRequest req, HttpContext ctx, AppDbContext db, bool restore)
    {
        Reason(req); await using var tx = await db.Database.BeginTransactionAsync(); var l = await db.LockLevel(id);
        if ((restore && (l.Status != "Withdrawn" || l.PublishedRevisionId is null)) || (!restore && l.Status != "Published")) throw new ApiError(409, "LEVEL_STATE_CONFLICT", "El estado no permite la operación.");
        l.Status = restore ? "Published" : "Withdrawn"; l.WithdrawnAt = restore ? null : DateTimeOffset.UtcNow; l.Version++; l.UpdatedAt = DateTimeOffset.UtcNow;
        if (!restore) { var revs = db.Revisions.Where(r => r.LevelId == id).Select(r => r.Id); await db.Attempts.Where(a => revs.Contains(a.RevisionId) && a.State == "Started").ExecuteUpdateAsync(s => s.SetProperty(a => a.State, "Abandoned")); }
        db.Audit.Add(new() { ActorUserId = ctx.UserId(), Action = restore ? "LevelRestored" : "LevelWithdrawn", TargetType = "Level", TargetId = id, Reason = req.Reason.Trim(), CorrelationId = ctx.TraceIdentifier });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.NoContent();
    }
    private static async Task<IResult> ChangeUser(Guid id, ReasonRequest req, HttpContext ctx, AppDbContext db, bool activate)
    {
        Reason(req); if (id == ctx.UserId()) throw new ApiError(409, "SELF_MODERATION", "No puedes moderar tu propia cuenta.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var u = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync() ?? throw new ApiError(404, "USER_NOT_FOUND", "Cuenta no disponible.");
        if (u.Status == "Deleted") throw new ApiError(409, "USER_DELETED", "Cuenta eliminada.");
        u.Status = activate ? "Active" : "Suspended"; u.SecurityVersion++;
        await db.Sessions.Where(s => s.UserId == id && s.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow));
        db.Audit.Add(new() { ActorUserId = ctx.UserId(), Action = activate ? "UserActivated" : "UserSuspended", TargetType = "User", TargetId = id, Reason = req.Reason.Trim(), CorrelationId = ctx.TraceIdentifier });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.NoContent();
    }
}
