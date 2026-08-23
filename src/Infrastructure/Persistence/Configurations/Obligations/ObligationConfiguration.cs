using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POM.Obligations;

namespace POM.Persistence.Configurations.Obligations;

internal sealed class ObligationConfiguration : IEntityTypeConfiguration<Obligation>
{
    public void Configure(EntityTypeBuilder<Obligation> builder)
    {
        builder.ToTable("obligations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Type).HasConversion<string>().IsRequired();
        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasDefaultValue(ObligationStatus.Pending).IsRequired();
        builder.Property(x => x.Priority).HasConversion<string>().HasDefaultValue(ObligationPriority.Medium).IsRequired();
        builder.Property(x => x.ExtraFields).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(x => x.IsRecurring).HasDefaultValue(false).IsRequired();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Status, x.DueDate }).HasDatabaseName("ix_obligations_user_status_due");
        builder.HasIndex(x => new { x.UserId, x.Type }).HasDatabaseName("ix_obligations_user_type");
        builder.HasOne<POM.Users.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
