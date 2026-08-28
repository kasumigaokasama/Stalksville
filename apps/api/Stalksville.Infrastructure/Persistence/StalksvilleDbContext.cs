using Microsoft.EntityFrameworkCore;
using Stalksville.Domain.Entities;

namespace Stalksville.Infrastructure.Persistence;

public sealed class StalksvilleDbContext(DbContextOptions<StalksvilleDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Player> Players => Set<Player>();

    public DbSet<PlayerSnapshot> PlayerSnapshots => Set<PlayerSnapshot>();

    public DbSet<PlayerChange> PlayerChanges => Set<PlayerChange>();

    public DbSet<Clan> Clans => Set<Clan>();

    public DbSet<ClanMembership> ClanMemberships => Set<ClanMembership>();

    public DbSet<Relationship> Relationships => Set<Relationship>();

    public DbSet<Evidence> Evidence => Set<Evidence>();

    public DbSet<TimelineEvent> TimelineEvents => Set<TimelineEvent>();

    public DbSet<ApiRequestLog> ApiRequests => Set<ApiRequestLog>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Investigation> Investigations => Set<Investigation>();

    public DbSet<InvestigationTarget> InvestigationTargets => Set<InvestigationTarget>();

    public DbSet<InvestigationNote> InvestigationNotes => Set<InvestigationNote>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<HighscoreEntry> HighscoreEntries => Set<HighscoreEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Player>(e =>
        {
            e.ToTable("players");
            // Wolvesville IDs are GUID-like strings in production data — keep generous room.
            e.Property(x => x.WolvesvillePlayerId).HasMaxLength(64);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.UsernameLower).HasMaxLength(64);
            e.HasIndex(x => x.WolvesvillePlayerId).IsUnique();
            e.HasIndex(x => x.UsernameLower);
            e.HasOne(x => x.CurrentClan)
                .WithMany()
                .HasForeignKey(x => x.CurrentClanId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlayerSnapshot>(e =>
        {
            e.ToTable("player_snapshots");
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.Property(x => x.PayloadHash).HasMaxLength(64);
            e.Property(x => x.Source).HasMaxLength(128);
            e.HasIndex(x => new { x.PlayerId, x.CapturedAt });
            e.HasIndex(x => x.PayloadHash);
        });

        modelBuilder.Entity<PlayerChange>(e =>
        {
            e.ToTable("player_changes");
            e.Property(x => x.Field).HasMaxLength(48);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.OldValue).HasMaxLength(1024);
            e.Property(x => x.NewValue).HasMaxLength(1024);
            e.HasIndex(x => new { x.PlayerId, x.DetectedAt });
            e.HasOne(x => x.Player)
                .WithMany(p => p.Changes)
                .HasForeignKey(x => x.PlayerId);
        });

        modelBuilder.Entity<Clan>(e =>
        {
            e.ToTable("clans");
            e.Property(x => x.WolvesvilleClanId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(2048);
            e.Property(x => x.LanguageCode).HasMaxLength(8);
            e.Property(x => x.JoinType).HasMaxLength(32);
            e.HasIndex(x => x.WolvesvilleClanId).IsUnique();
        });

        modelBuilder.Entity<ClanMembership>(e =>
        {
            e.ToTable("clan_memberships");
            e.Property(x => x.Source).HasMaxLength(128);
            e.Ignore(x => x.IsCurrent);
            e.HasIndex(x => new { x.PlayerId, x.EndedAt });
            e.HasIndex(x => new { x.ClanId, x.EndedAt });
            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId);
            e.HasOne(x => x.Clan)
                .WithMany(c => c.Memberships)
                .HasForeignKey(x => x.ClanId);
        });

        modelBuilder.Entity<Relationship>(e =>
        {
            e.ToTable("relationships");
            e.Property(x => x.SourceEntityType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.TargetEntityType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
            e.HasIndex(x => new { x.SourceEntityType, x.SourceEntityId, x.TargetEntityType, x.TargetEntityId }).IsUnique();
            e.HasIndex(x => x.TargetEntityId);
        });

        modelBuilder.Entity<Evidence>(e =>
        {
            e.ToTable("evidence");
            e.Property(x => x.EntityType).HasMaxLength(32);
            e.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(24);
            e.Property(x => x.SourceReference).HasMaxLength(256);
            e.Property(x => x.PayloadHash).HasMaxLength(64);
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        modelBuilder.Entity<TimelineEvent>(e =>
        {
            e.ToTable("timeline_events");
            e.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.EventType).HasMaxLength(48);
            e.Property(x => x.Summary).HasMaxLength(1024);
            e.Property(x => x.Metadata).HasColumnType("jsonb");
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt });
            e.HasIndex(x => x.OccurredAt);
        });

        modelBuilder.Entity<ApiRequestLog>(e =>
        {
            e.ToTable("api_requests");
            e.Property(x => x.Method).HasMaxLength(8);
            e.Property(x => x.Endpoint).HasMaxLength(256);
            e.Property(x => x.ErrorMessage).HasMaxLength(1024);
            e.HasIndex(x => x.RequestedAt);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.Property(x => x.Action).HasMaxLength(64);
            e.Property(x => x.Target).HasMaxLength(128);
            e.Property(x => x.Details).HasColumnType("jsonb");
            e.HasIndex(x => x.OccurredAt);
        });

        modelBuilder.Entity<Investigation>(e =>
        {
            e.ToTable("investigations");
            e.Property(x => x.CaseNumber).UseIdentityColumn();
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Description).HasMaxLength(2048);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Tags).HasColumnType("jsonb");
            e.HasIndex(x => x.AssignedToUserId);
            e.HasIndex(x => x.CaseNumber).IsUnique();
        });

        modelBuilder.Entity<InvestigationTarget>(e =>
        {
            e.ToTable("investigation_targets");
            e.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.AddedBy).HasMaxLength(64);
            e.HasIndex(x => new { x.InvestigationId, x.EntityType, x.EntityId }).IsUnique();
            e.HasIndex(x => x.EntityId);
            e.HasOne(x => x.Investigation)
                .WithMany(i => i.Targets)
                .HasForeignKey(x => x.InvestigationId);
        });

        modelBuilder.Entity<InvestigationNote>(e =>
        {
            e.ToTable("investigation_notes");
            e.Property(x => x.Content).HasMaxLength(4000);
            e.Property(x => x.Author).HasMaxLength(64);
            e.HasIndex(x => new { x.InvestigationId, x.CreatedAt });
            e.HasOne(x => x.Investigation)
                .WithMany(i => i.Notes)
                .HasForeignKey(x => x.InvestigationId);
        });

        modelBuilder.Entity<Alert>(e =>
        {
            e.ToTable("alerts");
            e.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.EntityTitle).HasMaxLength(64);
            e.Property(x => x.Kind).HasMaxLength(32);
            e.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Body).HasMaxLength(1024);
            e.Property(x => x.Evidence).HasColumnType("jsonb");
            e.Property(x => x.DedupeKey).HasMaxLength(128);
            e.HasIndex(x => x.DedupeKey).IsUnique();
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
            e.HasIndex(x => x.ReadAt);
        });

        modelBuilder.Entity<HighscoreEntry>(e =>
        {
            e.ToTable("highscore_entries");
            e.Property(x => x.Period).HasMaxLength(16);
            e.Property(x => x.WolvesvillePlayerId).HasMaxLength(64);
            e.Property(x => x.Username).HasMaxLength(64);
            e.Property(x => x.UsernameLower).HasMaxLength(64);
            e.HasIndex(x => new { x.Period, x.CapturedAt, x.Rank });
            e.HasIndex(x => x.UsernameLower);
            e.HasIndex(x => x.PlayerId);
        });
    }
}
