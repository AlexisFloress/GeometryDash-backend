using System.Text.Json;
using GeometryDash.Api.Data;
using GeometryDash.Api.Domain;
using GeometryDash.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace GeometryDash.Api.Endpoints;

public sealed record CreateLevelRequest(string Title, string Description = "");

public sealed record SaveDraftRequest(string Title, string Description, string Difficulty, LevelDocument Content);

public sealed record PublishRequest(Guid DraftRevisionId, int ExpectedLevelVersion);

public sealed record LevelCard
{
    public Guid Id
    {
        get; init;
    }
    public Guid RevisionId
    {
        get; init;
    }
    public int RevisionNumber
    {
        get; init;
    }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Difficulty { get; init; } = "";
    public Guid AuthorId
    {
        get; init;
    }
    public string Author { get; init; } = "";
    public double SpeedMultiplier
    {
        get; init;
    }
    public double? AverageRating
    {
        get; init;
    }
    public int RatingCount
    {
        get; init;
    }
    public int CompletionCount
    {
        get; init;
    }
    public DateTimeOffset UpdatedAt
    {
        get; init;
    }
}

public static class LevelEndpoints
{
    public static IQueryable<LevelCard> Cards(AppDbContext db, IQueryable<Level> levels) =>
        from l in levels
        where l.Status == "Published"
        join r in db.Revisions on l.PublishedRevisionId equals r.Id
        join u in db.Users on l.OwnerId equals u.Id
        select new LevelCard
        {
            Id = l.Id,
            RevisionId = r.Id,
            RevisionNumber = r.RevisionNumber,
            Title = r.Title,
            Description = r.Description,
            Difficulty = r.Difficulty,
            AuthorId = u.Id,
            Author = u.DisplayName,
            AverageRating = db.Ratings.Where(v => v.RevisionId == r.Id).Average(v => (double?)v.Score),
            RatingCount = db.Ratings.Count(v => v.RevisionId == r.Id),
            CompletionCount = db.Progress.Where(p => p.RevisionId == r.Id).Sum(p => p.CompletionCount),
            UpdatedAt = l.UpdatedAt
        };
    // Speed is projected after paging because jsonb content is stored as a typed document string.
    public static async Task<object> PageCards(AppDbContext db, IQueryable<Level> levels, int? page, int? pageSize, string? search = null, string? difficulty = null, Guid? authorId = null, string? sort = null, double? speed = null)
    {
        var (p, size) = RequestRules.Paging(page, pageSize);

        if (difficulty is not null && difficulty is not ("Easy" or "Normal" or "Hard"))
            throw new ApiError(400, "INVALID_FILTER", "Dificultad inválida.");

        if (sort is not null && sort is not ("recent" or "rating" or "completed"))
            throw new ApiError(400, "INVALID_SORT", "Orden inválido.");

        if (search?.Length > 80)
            throw new ApiError(400, "INVALID_FILTER", "Búsqueda demasiado larga.");

        if (speed is not null && speed is not (0.75 or 1 or 1.25 or 1.5))
            throw new ApiError(400, "INVALID_FILTER", "Velocidad inválida.");

        if (speed is not null)
        {
            // No client interpolation in SQL: Npgsql parameterizes the decimal value.
            var value = (decimal)speed.Value;
            var matching = db.Revisions.FromSqlInterpolated($"SELECT * FROM \"Revisions\" WHERE (\"Content\"->>'speedMultiplier')::numeric = {value}").Select(r => r.Id);
            levels = levels.Where(l => matching.Contains(l.PublishedRevisionId!.Value));
        }
        var query = Cards(db, levels);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(term));
        }

        if (difficulty is not null)
            query = query.Where(c => c.Difficulty == difficulty);

        if (authorId is not null)
            query = query.Where(c => c.AuthorId == authorId);
        var total = await query.CountAsync();
        query = sort switch
        {
            "rating" => query.OrderByDescending(c => c.AverageRating ?? -1).ThenBy(c => c.Id),
            "completed" => query.OrderByDescending(c => c.CompletionCount).ThenBy(c => c.Id),
            _ => query.OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id)
        };
        var cards = await query.Skip((p - 1) * size).Take(size).ToListAsync();
        var revIds = cards.Select(c => c.RevisionId).ToArray();
        var contents = await db.Revisions.Where(r => revIds.Contains(r.Id)).Select(r => new { r.Id, r.Content }).ToDictionaryAsync(r => r.Id);

        return new
        {
            items = cards.Select(c => c with { SpeedMultiplier = JsonSerializer.Deserialize<LevelDocument>(contents[c.RevisionId].Content, LevelRules.Json)!.SpeedMultiplier }),
            page = p,
            pageSize = size,
            totalCount = total
        };
    }

    public static void Available(Level level)
    {
        if (level.Status != "Published")
            throw new ApiError(404, "LEVEL_NOT_AVAILABLE", "Nivel no disponible.");
    }

    public static void Owned(Level level, HttpContext ctx)
    {
        if (level.OwnerId != ctx.UserId() || level.Status == "Deleted")
            throw new ApiError(404, "LEVEL_NOT_AVAILABLE", "Nivel no disponible.");
    }

    public static object Draft(Level l, LevelRevision r) => new { levelId = l.Id, levelVersion = l.Version, revisionId = r.Id, r.RevisionNumber, r.Version, r.Title, r.Description, r.Difficulty, content = JsonSerializer.Deserialize<LevelDocument>(r.Content, LevelRules.Json), r.ContentHash, r.State };

    public static void MapLevels(this WebApplication app)
    {
        var routes = app.MapGroup("/api/v1").WithTags("Niveles");
        routes.MapGet("/levels", (AppDbContext db, int? page, int? pageSize, string? search, string? difficulty, Guid? authorId, string? sort, double? speed) => PageCards(db, db.Levels, page, pageSize, search, difficulty, authorId, sort, speed)).WithSummary("Muestra los niveles publicados. Puedes buscar por título, filtrar por dificultad, autor o velocidad y elegir el orden de la lista, sin iniciar sesión.");
        routes.MapGet("/levels/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var l = await db.Levels.AsNoTracking().SingleOrDefaultAsync(l => l.Id == id) ?? throw new ApiError(404, "LEVEL_NOT_AVAILABLE", "Nivel no disponible.");
            Available(l);
            var card = await Cards(db, db.Levels.Where(l => l.Id == id)).SingleAsync();
            var content = await db.Revisions.Where(r => r.Id == card.RevisionId).Select(r => r.Content).SingleAsync();

            return Results.Ok(card with
            {
                SpeedMultiplier = JsonSerializer.Deserialize<LevelDocument>(content, LevelRules.Json)!.SpeedMultiplier
            });
        }).WithSummary("Muestra la ficha de un nivel publicado: título, descripción, autor, dificultad y valoraciones. No necesitas iniciar sesión.");
        routes.MapGet("/levels/{id:guid}/content", async (Guid id, AppDbContext db) =>
        {
            var r = await (from l in db.Levels where l.Id == id && l.Status == "Published" join rev in db.Revisions on l.PublishedRevisionId equals rev.Id select rev).AsNoTracking().SingleOrDefaultAsync()
                ?? throw new ApiError(404, "LEVEL_NOT_AVAILABLE", "Nivel no disponible.");

            return Results.Ok(new
            {
                levelId = id,
                revisionId = r.Id,
                r.ContentHash,
                r.PhysicsVersion,
                content = JsonSerializer.Deserialize<LevelDocument>(r.Content, LevelRules.Json)
            });
        }).WithSummary("Entrega el recorrido de la versión publicada de un nivel, con sus obstáculos y posiciones, para poder jugarlo. No necesitas iniciar sesión.");
        routes.MapPost("/levels", async (CreateLevelRequest req, HttpContext ctx, AppDbContext db) =>
        {
            RequestRules.Validate(LevelRules.Validate(req.Title, req.Description, "Normal", LevelDocument.Empty, false));
            var keyText = ctx.Request.Headers["Idempotency-Key"].ToString();

            if (!Guid.TryParse(keyText, out var key))
                throw new ApiError(400, "IDEMPOTENCY_KEY_REQUIRED", "Envía un UUID en Idempotency-Key y consérvalo al reintentar.");
            var uid = ctx.UserId();
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {uid} FOR UPDATE").SingleAsync();
            var previous = await db.Levels.SingleOrDefaultAsync(l => l.OwnerId == uid && l.CreationKey == key);

            if (previous is not null)
            {
                if (previous.CreationHash != LevelRules.Hash(JsonSerializer.Serialize(req)))
                    throw new ApiError(409, "IDEMPOTENCY_CONFLICT", "La clave ya se usó con otro contenido.");

                if (previous.Status == "Deleted")
                    throw new ApiError(409, "LEVEL_DELETED", "El nivel creado con esta clave fue eliminado.");
                var old = await db.Revisions.Where(r => r.LevelId == previous.Id).OrderBy(r => r.RevisionNumber).FirstAsync();

                return Results.Ok(Draft(previous, old));
            }

            if (await db.Levels.CountAsync(l => l.OwnerId == uid && l.Status != "Deleted") >= 50)
                throw new ApiError(422, "LEVEL_QUOTA", "Máximo 50 niveles activos.");
            var level = new Level { OwnerId = uid, CreationKey = key, CreationHash = LevelRules.Hash(JsonSerializer.Serialize(req)) };
            db.Levels.Add(level);
            await db.SaveChangesAsync();
            var revision = new LevelRevision { LevelId = level.Id, Title = req.Title.Trim(), Description = req.Description };
            revision.ContentHash = LevelRules.Hash(revision.Content);
            db.Revisions.Add(revision);
            await db.SaveChangesAsync();
            level.DraftRevisionId = revision.Id;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            ctx.Response.Headers.ETag = "\"1\"";

            return Results.Created($"/api/v1/levels/{level.Id}/draft", Draft(level, revision));
        }).RequireAuthorization().RequireRateLimiting("write").WithSummary("Crea un nivel nuevo con un borrador privado para empezar a editarlo. Necesitas iniciar sesión; todavía no será visible para otras personas.");

        routes.MapGet("/levels/{id:guid}/draft", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            var l = await db.Levels.SingleOrDefaultAsync(l => l.Id == id) ?? throw new ApiError(404, "LEVEL_NOT_AVAILABLE", "Nivel no disponible.");
            Owned(l, ctx);
            var r = await db.Revisions.SingleOrDefaultAsync(r => r.Id == l.DraftRevisionId) ?? throw new ApiError(404, "DRAFT_NOT_FOUND", "Crea un borrador a partir de la publicación.");
            ctx.Response.Headers.ETag = $"\"{r.Version}\"";

            return Results.Ok(Draft(l, r));
        }).RequireAuthorization().WithSummary("Recupera el borrador de un nivel para continuar editándolo. Solo su creador puede consultarlo.");
        routes.MapPost("/levels/{id:guid}/draft", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var l = await db.LockLevel(id);
            Owned(l, ctx);

            if (l.DraftRevisionId is { } existing)
            {
                var current = await db.Revisions.SingleAsync(r => r.Id == existing);
                ctx.Response.Headers.ETag = $"\"{current.Version}\"";

                return Results.Ok(Draft(l, current));
            }
            var old = await db.Revisions.SingleAsync(r => r.Id == l.PublishedRevisionId);
            var r = new LevelRevision
            {
                LevelId = id,
                RevisionNumber = await db.Revisions.Where(r => r.LevelId == id).MaxAsync(r => r.RevisionNumber) + 1,
                Title = old.Title,
                Description = old.Description,
                Difficulty = old.Difficulty,
                Content = old.Content,
                ContentHash = old.ContentHash,
                ObjectCount = old.ObjectCount
            };
            db.Revisions.Add(r);
            await db.SaveChangesAsync();
            l.DraftRevisionId = r.Id;
            l.Version++;
            l.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            ctx.Response.Headers.ETag = "\"1\"";

            return Results.Ok(Draft(l, r));
        }).RequireAuthorization().RequireRateLimiting("write").WithSummary("Prepara un borrador a partir de la versión publicada de tu nivel. Si ya tienes uno, lo recupera; la versión pública sigue disponible mientras editas.");
        routes.MapPut("/levels/{id:guid}/draft", async (Guid id, SaveDraftRequest req, HttpContext ctx, AppDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var l = await db.LockLevel(id);
            Owned(l, ctx);
            var r = await db.Revisions.SingleOrDefaultAsync(r => r.Id == l.DraftRevisionId) ?? throw new ApiError(409, "DRAFT_NOT_FOUND", "No hay borrador editable.");
            RequestRules.ExpectedVersion(ctx, r.Version);
            RequestRules.Validate(LevelRules.Validate(req.Title, req.Description, req.Difficulty, req.Content, false));
            r.Title = req.Title.Trim();
            r.Description = req.Description;
            r.Difficulty = req.Difficulty;
            r.Content = LevelRules.Canonical(req.Content);
            r.ContentHash = LevelRules.Hash(r.Content);
            r.ObjectCount = req.Content.Objects.Length;
            r.Version++;
            l.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            ctx.Response.Headers.ETag = $"\"{r.Version}\"";

            return Results.Ok(Draft(l, r));
        }).RequireAuthorization().RequireRateLimiting("write").WithSummary("Guarda los cambios del título, descripción, dificultad y recorrido de tu borrador. Comprueba la versión para evitar que sobrescribas cambios más recientes.");
        routes.MapPost("/levels/{id:guid}/publish", async (Guid id, PublishRequest req, HttpContext ctx, AppDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var l = await db.LockLevel(id);
            Owned(l, ctx);

            if (l.Status == "Withdrawn")
                throw new ApiError(409, "LEVEL_WITHDRAWN", "El nivel está retirado por administración.");

            if (l.PublishedRevisionId == req.DraftRevisionId && l.DraftRevisionId is null)
            {
                var published = await db.Revisions.SingleAsync(r => r.Id == req.DraftRevisionId);
                RequestRules.ExpectedVersion(ctx, published.Version);

                if (req.ExpectedLevelVersion + 1 != l.Version)
                    throw new ApiError(409, "LEVEL_VERSION_CONFLICT", "El nivel cambió desde la publicación.");

                return Results.Ok(new
                {
                    levelId = l.Id,
                    revisionId = l.PublishedRevisionId,
                    levelVersion = l.Version
                });
            }

            if (l.Version != req.ExpectedLevelVersion || l.DraftRevisionId != req.DraftRevisionId)
                throw new ApiError(409, "LEVEL_VERSION_CONFLICT", "El nivel cambió.");
            var r = await db.Revisions.SingleAsync(r => r.Id == l.DraftRevisionId);
            RequestRules.ExpectedVersion(ctx, r.Version);
            var doc = JsonSerializer.Deserialize<LevelDocument>(r.Content, LevelRules.Json)!;
            RequestRules.Validate(LevelRules.Validate(r.Title, r.Description, r.Difficulty, doc, true));
            r.State = "Published";
            r.PublishedAt = DateTimeOffset.UtcNow;
            l.PublishedRevisionId = r.Id;
            l.DraftRevisionId = null;
            l.Status = "Published";
            l.Version++;
            l.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new
            {
                levelId = l.Id,
                revisionId = r.Id,
                levelVersion = l.Version
            });
        }).RequireAuthorization().RequireRateLimiting("publish").WithSummary("Publica el borrador de tu nivel para que otras personas puedan encontrarlo y jugarlo. Antes comprueba que el recorrido cumpla las reglas del juego.");
        routes.MapDelete("/levels/{id:guid}", async (Guid id, HttpContext ctx, AppDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var l = await db.LockLevel(id);

            if (l.OwnerId != ctx.UserId())
                throw new ApiError(404, "LEVEL_NOT_AVAILABLE", "Nivel no disponible.");

            if (l.Status == "Deleted")
                return Results.NoContent();
            l.Status = "Deleted";
            l.DeletedAt = l.UpdatedAt = DateTimeOffset.UtcNow;
            l.Version++;
            await db.Favorites.Where(f => f.LevelId == id).ExecuteDeleteAsync();
            var revisions = db.Revisions.Where(r => r.LevelId == id).Select(r => r.Id);
            await db.Attempts.Where(a => revisions.Contains(a.RevisionId) && a.State == "Started").ExecuteUpdateAsync(s => s.SetProperty(a => a.State, "Abandoned"));
            db.Audit.Add(new()
            {
                ActorUserId = ctx.UserId(),
                Action = "LevelDeleted",
                TargetType = "Level",
                TargetId = id,
                CorrelationId = ctx.TraceIdentifier
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("write").WithSummary("Elimina uno de tus niveles para que deje de estar disponible. También lo quita de los favoritos y da por abandonadas las partidas que seguían abiertas.");
        routes.MapGet("/me/levels", async (HttpContext ctx, AppDbContext db, int? page, int? pageSize) =>
        {
            var (p, s) = RequestRules.Paging(page, pageSize);
            var uid = ctx.UserId();
            var q = db.Levels.Where(l => l.OwnerId == uid && l.Status != "Deleted");
            var total = await q.CountAsync();
            var items = await (from l in q
                               join r in db.Revisions on (l.DraftRevisionId ?? l.PublishedRevisionId) equals r.Id
                               orderby l.UpdatedAt descending, l.Id
                               select new
                               {
                                   l.Id,
                                   l.Status,
                                   l.Version,
                                   l.DraftRevisionId,
                                   l.PublishedRevisionId,
                                   r.Title,
                                   r.Difficulty,
                                   l.UpdatedAt
                               }).Skip((p - 1) * s).Take(s).ToListAsync();

            return Results.Ok(new
            {
                items,
                page = p,
                pageSize = s,
                totalCount = total
            });
        }).RequireAuthorization().WithSummary("Muestra tus niveles, incluidos los borradores y los publicados, para que puedas administrarlos. Necesitas haber iniciado sesión.");
    }
}
