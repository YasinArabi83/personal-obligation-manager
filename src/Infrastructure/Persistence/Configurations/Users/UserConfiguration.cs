using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POM.Users;

namespace POM.Persistence.Configurations.Users;

/// <summary>
/// EF mapping for the domain <see cref="User"/> to the clean <c>users</c> table
/// (docs/DATABASE.md §3, ADR-0016). This is <b>not</b> Identity's <c>AspNetUsers</c> schema —
/// the custom <c>UserStore</c> keeps Identity off the schema, so there is no password column and
/// no lockout/two-factor columns. Snake_case column naming comes from the global convention; names
/// are set explicitly here only for readability/auditability.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        // App-assigned Guid PK (Q3 / ADR-0016): the domain owns identity, the DB never generates it.
        builder.Property(u => u.Id)
            .ValueGeneratedNever();

        builder.Property(u => u.PhoneNumber)
            .IsRequired()
            .HasColumnName("phone_number");

        // Uniqueness enforced at the DB; surfaced as Identity's DuplicateUserName by the store.
        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique();

        builder.Property(u => u.PhoneNumberConfirmed)
            .IsRequired()
            .HasColumnName("phone_number_confirmed")
            .HasDefaultValue(false);

        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name");

        builder.Property(u => u.CalendarPreference)
            .HasConversion<string>()
            .IsRequired()
            .HasColumnName("calendar_pref")
            .HasDefaultValue(CalendarPreference.Jalali);

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(u => u.DeletedAt)
            .HasColumnName("deleted_at");

        // Identity token entropy (ADR-0016, Q1b).
        builder.Property(u => u.SecurityStamp)
            .IsRequired()
            .HasColumnName("security_stamp");
    }
}
