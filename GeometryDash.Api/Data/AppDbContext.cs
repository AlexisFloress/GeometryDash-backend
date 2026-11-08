using GeometryDash.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GeometryDash.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<Level> Levels => Set<Level>();
    public DbSet<LevelRevision> Revisions => Set<LevelRevision>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<PlayAttempt> Attempts => Set<PlayAttempt>();
    public DbSet<UserRevisionProgress> Progress => Set<UserRevisionProgress>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<AppUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        b.Entity<AppUser>().Property(x => x.DisplayName).HasMaxLength(60);
        b.Entity<AppUser>().ToTable(t => t.HasCheckConstraint("CK_User_Status", "\"Status\" IN ('Active','Suspended','Deleted')"));
        b.Entity<AuthSession>().HasIndex(x => x.RefreshTokenHash).IsUnique();
        b.Entity<AuthSession>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AuthSession>().HasOne<AuthSession>().WithMany().HasForeignKey(x => x.ReplacedBySessionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Level>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Level>().HasIndex(x => new { x.OwnerId, x.UpdatedAt });
        b.Entity<Level>().HasIndex(x => new { x.OwnerId, x.CreationKey }).IsUnique();
        b.Entity<Level>().HasIndex(x => new { x.Status, x.UpdatedAt, x.Id });
        b.Entity<Level>().ToTable(t => t.HasCheckConstraint("CK_Level_Status", "\"Status\" IN ('Draft','Published','Withdrawn','Deleted')"));
        b.Entity<LevelRevision>().HasOne<Level>().WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LevelRevision>().HasAlternateKey(x => new { x.LevelId, x.Id });
        b.Entity<Level>().HasOne<LevelRevision>().WithMany().HasForeignKey(x => new { x.Id, x.PublishedRevisionId }).HasPrincipalKey(x => new { x.LevelId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Level>().HasOne<LevelRevision>().WithMany().HasForeignKey(x => new { x.Id, x.DraftRevisionId }).HasPrincipalKey(x => new { x.LevelId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LevelRevision>().HasIndex(x => new { x.LevelId, x.RevisionNumber }).IsUnique();
        b.Entity<LevelRevision>().HasIndex(x => x.LevelId).IsUnique().HasFilter("\"State\" = 'Draft'");
        b.Entity<LevelRevision>().Property(x => x.Title).HasMaxLength(80);
        b.Entity<LevelRevision>().Property(x => x.Description).HasMaxLength(2000);
        b.Entity<LevelRevision>().Property(x => x.Content).HasColumnType("jsonb");
        b.Entity<LevelRevision>().ToTable(t => {
            t.HasCheckConstraint("CK_Revision_State", "\"State\" IN ('Draft','Published')");
            t.HasCheckConstraint("CK_Revision_Difficulty", "\"Difficulty\" IN ('Easy','Normal','Hard')");
        });
        b.Entity<Favorite>().HasKey(x => new { x.UserId, x.LevelId });
        b.Entity<Favorite>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Favorite>().HasOne<Level>().WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Favorite>().HasIndex(x => new { x.UserId, x.CreatedAt });
        b.Entity<Rating>().HasKey(x => new { x.UserId, x.RevisionId });
        b.Entity<Rating>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Rating>().HasOne<LevelRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Rating>().ToTable(t => t.HasCheckConstraint("CK_Rating_Score", "\"Score\" BETWEEN 1 AND 5"));
        b.Entity<PlayAttempt>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PlayAttempt>().HasOne<LevelRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PlayAttempt>().HasIndex(x => new { x.UserId, x.StartedAt });
        b.Entity<PlayAttempt>().HasIndex(x => new { x.RevisionId, x.StartedAt });
        b.Entity<PlayAttempt>().ToTable(t => {
            t.HasCheckConstraint("CK_Attempt_State", "\"State\" IN ('Started','Finished','Abandoned','Expired')");
            t.HasCheckConstraint("CK_Attempt_Outcome", "\"Outcome\" IS NULL OR \"Outcome\" IN ('Dead','Completed','Quit')");
            t.HasCheckConstraint("CK_Attempt_Progress", "\"ProgressPercent\" BETWEEN 0 AND 100");
        });
        b.Entity<UserRevisionProgress>().HasKey(x => new { x.UserId, x.RevisionId });
        b.Entity<UserRevisionProgress>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserRevisionProgress>().HasOne<LevelRevision>().WithMany().HasForeignKey(x => x.RevisionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<UserRevisionProgress>().HasIndex(x => new { x.UserId, x.LastPlayedAt });
        b.Entity<AuditEvent>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
    // All level mutations acquire this row first. Shared ordering covers publish, deletion,
    // favorites, ratings and attempt results across multiple API instances.
    public async Task<Level> LockLevel(Guid id, CancellationToken ct = default) =>
        await Levels.FromSqlInterpolated($"SELECT * FROM \"Levels\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct)
        ?? throw new Infrastructure.ApiError(404, "LEVEL_NOT_AVAILABLE", "Nivel no disponible.");
}
