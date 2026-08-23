using KaamLink.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KaamLink.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
        builder.HasIndex(u => u.PhoneNumber).IsUnique().HasDatabaseName("ux_users_phone_number");

        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(120);
        builder.Property(u => u.PreferredLocale).HasColumnName("preferred_locale").HasMaxLength(8);

        // Enum stored as text: stored values stay stable if the enum is ever reordered.
        builder.Property(u => u.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(u => u.IsPlatformAdmin).HasColumnName("is_platform_admin");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
    }
}
