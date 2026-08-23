using KaamLink.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);
        // Server-generated bigint identity (append-only, high volume).
        builder.Property(a => a.Id).HasColumnName("id").UseIdentityByDefaultColumn();

        builder.Property(a => a.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(80).IsRequired();
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(80).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(80);
        builder.Property(a => a.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(a => new { a.EntityType, a.EntityId }).HasDatabaseName("ix_audit_logs_entity");
        builder.HasIndex(a => a.CreatedAt).HasDatabaseName("ix_audit_logs_created_at");
    }
}
