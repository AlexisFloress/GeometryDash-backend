using System.Text.Json;
using GeometryDash.Api.Data;
using GeometryDash.Api.Domain;
using GeometryDash.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeometryDash.Api.Endpoints;

public sealed record RatingRequest(short Score);

public sealed record StartAttemptRequest(Guid ExpectedRevisionId);

public static class InteractionEndpoints
{
    public static void MapInteractions(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Biblioteca y juego");
        api.MapPut("/levels/{id:guid}/favorite", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var l = await db.LockLevel(id);
            LevelEndpoints.Available(l);
            var uid = ctx.UserId();

            if (!await db.Favorites.AnyAsync(f => f.UserId == uid && f.LevelId == id))
                db.Favorites.Add(new()
                {
                    UserId = uid,
                    LevelId = id
                });
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.NoContent();
        }).RequireRateLimiting("write").WithSummary("Añade un nivel publicado a tus favoritos para encontrarlo después. Necesitas haber iniciado sesión.");
        api.MapDelete("/levels/{id:guid}/favorite", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var uid = ctx.UserId();
            await db.Favorites.Where(f => f.UserId == uid && f.LevelId == id).ExecuteDeleteAsync();

            return Results.NoContent();
        }).RequireRateLimiting("write").WithSummary("Quita un nivel de tus favoritos sin eliminar el nivel. Necesitas haber iniciado sesión.");
        api.MapGet("/me/favorites", (HttpContext ctx, AppDbContext db, int? page, int? pageSize) =>
        {
            var uid = ctx.UserId();

            return LevelEndpoints.PageCards(db, db.Levels.Where(l => db.Favorites.Any(f => f.LevelId == l.Id && f.UserId == uid)), page, pageSize);
        }).WithSummary("Muestra los niveles que guardaste como favoritos y que siguen publicados. Necesitas haber iniciado sesión.");
        api.MapPut("/revisions/{id:guid}/rating", async (Guid id, RatingRequest req, HttpContext ctx, AppDbContext db) =>
        {
            if (req.Score is < 1 or > 5)
                throw new ApiError(422, "RATING_INVALID", "La nota debe ser de 1 a 5.");
            var rev = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.State == "Published") ?? throw new ApiError(404, "LEVEL_NOT_AVAILABLE", "Revisión no disponible.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var l = await db.LockLevel(rev.LevelId);
            LevelEndpoints.Available(l);
            var uid = ctx.UserId();

            if (l.OwnerId == uid)
                throw new ApiError(403, "SELF_RATING", "No puedes valorar tu nivel.");

            if (!await db.Attempts.AnyAsync(a => a.UserId == uid && a.RevisionId == id && a.State == "Finished" && a.VerificationStatus != "Rejected"))
                throw new ApiError(422, "PLAY_REQUIRED", "Termina un intento sobre esta revisión antes de valorarla.");
            var rating = await db.Ratings.FindAsync(uid, id);

            if (rating is null)
                db.Ratings.Add(new()
                {
                    UserId = uid,
                    RevisionId = id,
                    Score = req.Score
                });
            else
            {
                rating.Score = req.Score;
                rating.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.NoContent();
        }).RequireRateLimiting("write").WithSummary("Guarda o cambia tu valoración de una versión de un nivel, de 1 a 5 estrellas. Debes iniciar sesión y haber terminado una partida en esa versión; no puedes valorar tus propios niveles.");
        api.MapDelete("/revisions/{id:guid}/rating", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var uid = ctx.UserId();
            await db.Ratings.Where(r => r.UserId == uid && r.RevisionId == id).ExecuteDeleteAsync();

            return Results.NoContent();
        }).RequireRateLimiting("write").WithSummary("Borra la valoración que diste a una versión de un nivel. Necesitas haber iniciado sesión.");
        api.MapPost("/levels/{id:guid}/attempts", async (Guid id, StartAttemptRequest req, HttpContext ctx, AppDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var l = await db.LockLevel(id);
            LevelEndpoints.Available(l);

            if (l.PublishedRevisionId != req.ExpectedRevisionId)
                throw new ApiError(409, "REVISION_CHANGED", "Descarga la publicación actual.");
            var uid = ctx.UserId();
            await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {uid} FOR UPDATE").SingleAsync();
            var now = DateTimeOffset.UtcNow;
            await db.Attempts.Where(a => a.UserId == uid && a.State == "Started" && a.StartedAt < now.AddMinutes(-30)).ExecuteUpdateAsync(s => s.SetProperty(a => a.State, "Expired"));

            if (await db.Attempts.CountAsync(a => a.UserId == uid && a.State == "Started") >= 5)
                throw new ApiError(422, "ATTEMPT_QUOTA", "Máximo cinco intentos activos.");
            var r = await db.Revisions.SingleAsync(r => r.Id == l.PublishedRevisionId);
            var a = new PlayAttempt { UserId = uid, RevisionId = r.Id, PhysicsVersion = r.PhysicsVersion, ContentHash = r.ContentHash };
            db.Attempts.Add(a);
            var p = await db.Progress.FindAsync(uid, r.Id);

            if (p is null)
                db.Progress.Add(new()
                {
                    UserId = uid,
                    RevisionId = r.Id,
                    AttemptsCount = 1,
                    LastPlayedAt = now
                });
            else
            {
                p.AttemptsCount++;
                p.LastPlayedAt = now;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Created($"/api/v1/attempts/{a.Id}", a);
        }).RequireRateLimiting("attempt").WithSummary("Registra el inicio de una partida en la versión publicada de un nivel para luego guardar su resultado. Necesitas haber iniciado sesión.");
        api.MapPut("/attempts/{id:guid}/result", async (Guid id, AttemptResult req, HttpContext ctx, AppDbContext db) =>
        {
            var uid = ctx.UserId();
            var lookup = await (from played in db.Attempts where played.Id == id && played.UserId == uid join revision in db.Revisions on played.RevisionId equals revision.Id select new { revision.LevelId }).SingleOrDefaultAsync() ?? throw new ApiError(404, "ATTEMPT_NOT_FOUND", "Intento no disponible.");
            await using var tx = await db.Database.BeginTransactionAsync();
            var l = await db.LockLevel(lookup.LevelId);
            LevelEndpoints.Available(l);
            var a = await db.Attempts.SingleAsync(a => a.Id == id && a.UserId == uid);

            if (a.State == "Finished")
            {
                if (a.ClientResultId != req.ClientResultId || a.Outcome != req.Outcome || a.ElapsedTicks != req.ElapsedTicks || a.PauseTicks != req.PauseTicks || a.ProgressPercent != req.ProgressPercent)
                    throw new ApiError(409, "RESULT_CONFLICT", "El intento ya tiene otro resultado.");

                return Results.Ok(a);
            }

            if (a.State != "Started")
                throw new ApiError(409, "ATTEMPT_CLOSED", "El intento no está activo.");
            var now = DateTimeOffset.UtcNow;

            if (now - a.StartedAt > TimeSpan.FromMinutes(30))
            {
                a.State = "Expired";
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                throw new ApiError(409, "ATTEMPT_EXPIRED", "El intento expiró.");
            }
            var r = await db.Revisions.SingleAsync(r => r.Id == a.RevisionId);
            var error = AttemptRules.Validate(req, JsonSerializer.Deserialize<LevelDocument>(r.Content, LevelRules.Json)!, (now - a.StartedAt).TotalSeconds);

            if (error is not null)
                throw new ApiError(422, "RESULT_INVALID", error);
            a.ClientResultId = req.ClientResultId;
            a.Outcome = req.Outcome;
            a.ElapsedTicks = req.ElapsedTicks;
            a.PauseTicks = req.PauseTicks;
            a.ProgressPercent = req.ProgressPercent;
            a.FinishedAt = now;
            a.State = "Finished";
            var p = await db.Progress.SingleAsync(p => p.UserId == uid && p.RevisionId == a.RevisionId);
            p.BestProgress = Math.Max(p.BestProgress, req.ProgressPercent);
            p.LastPlayedAt = now;

            if (req.Outcome == "Completed")
            {
                p.CompletionCount++;
                p.FirstCompletedAt ??= now;
                p.LastCompletedAt = now;
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(a);
        }).RequireRateLimiting("attempt").WithSummary("Guarda cómo terminó tu partida y cuánto avanzaste. Reenviar el mismo resultado no lo cuenta dos veces. El resultado lo informa el juego y no se comprueba repitiendo la partida.");
        api.MapGet("/attempts/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var uid = ctx.UserId();

            return Results.Ok(await db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.UserId == uid) ?? throw new ApiError(404, "ATTEMPT_NOT_FOUND", "Intento no disponible."));
        }).WithSummary("Muestra el estado y, si ya terminó, el resultado de una de tus partidas. Solo puedes consultar tus propias partidas.");
        api.MapGet("/me/attempts", async (HttpContext ctx, AppDbContext db, int? page, int? pageSize) =>
        {
            var (p, s) = RequestRules.Paging(page, pageSize);
            var uid = ctx.UserId();
            var q = db.Attempts.AsNoTracking().Where(a => a.UserId == uid);

            return Results.Ok(new
            {
                items = await q.OrderByDescending(a => a.StartedAt).ThenBy(a => a.Id).Skip((p - 1) * s).Take(s).ToListAsync(),
                page = p,
                pageSize = s,
                totalCount = await q.CountAsync()
            });
        }).WithSummary("Muestra tu historial de partidas, empezando por las más recientes. Necesitas haber iniciado sesión.");
        api.MapGet("/me/completed", async (HttpContext ctx, AppDbContext db, int? page, int? pageSize) =>
        {
            var (p, s) = RequestRules.Paging(page, pageSize);
            var uid = ctx.UserId();
            var q = db.Levels.Where(l => db.Progress.Any(p => p.UserId == uid && p.FirstCompletedAt != null && db.Revisions.Any(r => r.Id == p.RevisionId && r.LevelId == l.Id)));
            var total = await q.CountAsync();
            var levels = await q.OrderByDescending(l => l.UpdatedAt).ThenBy(l => l.Id).Skip((p - 1) * s).Take(s).ToListAsync();
            var ids = levels.Select(l => l.Id).ToArray();
            var history = await (from progress in db.Progress where progress.UserId == uid && progress.FirstCompletedAt != null join r in db.Revisions on progress.RevisionId equals r.Id where ids.Contains(r.LevelId) select new { r.LevelId, r.Id, r.RevisionNumber, r.Title, progress.BestProgress, progress.CompletionCount, progress.FirstCompletedAt }).ToListAsync();

            return Results.Ok(new
            {
                items = levels.Select(l => new { levelId = l.Id, available = l.Status == "Published", currentRevisionCompleted = history.Any(h => h.LevelId == l.Id && h.Id == l.PublishedRevisionId), verificationStatus = "ClientReported", revisions = history.Where(h => h.LevelId == l.Id) }),
                page = p,
                pageSize = s,
                totalCount = total
            });
        }).WithSummary("Muestra los niveles que has completado y las versiones que terminaste. También indica si el nivel sigue disponible y si ya completaste su versión actual. Necesitas haber iniciado sesión.");
    }
}
