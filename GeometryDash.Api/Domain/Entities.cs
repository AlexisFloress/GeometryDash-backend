using Microsoft.AspNetCore.Identity;

namespace GeometryDash.Api.Domain;

public sealed class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "Active";
    public int SecurityVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuthSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId
    { get; set; }
    public string RefreshTokenHash { get; set; } = "";
    public Guid FamilyId { get; set; } = Guid.NewGuid();
    public Guid? ReplacedBySessionId
    { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt
    { get; set; }
    public DateTimeOffset? RevokedAt
    { get; set; }
}

public sealed class Level
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId
    { get; set; }
    public Guid CreationKey
    { get; set; }
    public string CreationHash { get; set; } = "";
    public string Status { get; set; } = "Draft";
    public Guid? PublishedRevisionId
    { get; set; }
    public Guid? DraftRevisionId
    { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt
    { get; set; }
    public DateTimeOffset? WithdrawnAt
    { get; set; }
}

public sealed class LevelRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LevelId
    { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public string Title { get; set; } = "Nuevo nivel";
    public string Description { get; set; } = "";
    public string Difficulty { get; set; } = "Normal";
    public string State { get; set; } = "Draft";
    public string Content { get; set; } = LevelRules.Canonical(LevelDocument.Empty);
    public int SchemaVersion { get; set; } = 1;
    public int PhysicsVersion { get; set; } = 1;
    public string ContentHash { get; set; } = "";
    public int ObjectCount
    { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt
    { get; set; }
}

public sealed class Favorite
{
    public Guid UserId
    { get; set; }
    public Guid LevelId
    { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Rating
{
    public Guid UserId
    { get; set; }
    public Guid RevisionId
    { get; set; }
    public short Score
    { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PlayAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId
    { get; set; }
    public Guid RevisionId
    { get; set; }
    public string State { get; set; } = "Started";
    public string? Outcome
    { get; set; }
    public decimal ProgressPercent
    { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt
    { get; set; }
    public long ElapsedTicks
    { get; set; }
    public long PauseTicks
    { get; set; }
    public string VerificationStatus { get; set; } = "ClientReported";
    public int PhysicsVersion
    { get; set; }
    public string ContentHash { get; set; } = "";
    public Guid? ClientResultId
    { get; set; }
}

public sealed class UserRevisionProgress
{
    public Guid UserId
    { get; set; }
    public Guid RevisionId
    { get; set; }
    public int AttemptsCount
    { get; set; }
    public decimal BestProgress
    { get; set; }
    public DateTimeOffset? FirstCompletedAt
    { get; set; }
    public DateTimeOffset LastPlayedAt
    { get; set; }
    public DateTimeOffset? LastCompletedAt
    { get; set; }
    public int CompletionCount
    { get; set; }
    public long? BestVerifiedTicks
    { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId
    { get; set; }
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public Guid TargetId
    { get; set; }
    public string Reason { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
