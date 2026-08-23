using Microsoft.EntityFrameworkCore;
using POM.Taxonomy;

namespace POM.Persistence.Seeding;

public sealed class DefaultCategorySeeder(PomDbContext dbContext)
{
    public static IReadOnlyList<(Guid Id, string Name, string? Icon)> Defaults { get; } =
    [
        (Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-111111111111"), "Bills", "receipt"),
        (Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-222222222222"), "Subscriptions", "repeat"),
        (Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-333333333333"), "Documents", "file-text"),
        (Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-444444444444"), "Maintenance", "wrench"),
        (Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-555555555555"), "Appointments", "calendar"),
        (Guid.Parse("4f1f5f84-9a1a-4b0b-9f8f-666666666666"), "Personal", "user"),
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Categories
            .Where(x => x.IsDefault)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var item in Defaults)
        {
            if (existing.ContainsKey(item.Id)) continue;
            dbContext.Categories.Add(Category.CreateDefault(item.Id, item.Name, item.Icon));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
