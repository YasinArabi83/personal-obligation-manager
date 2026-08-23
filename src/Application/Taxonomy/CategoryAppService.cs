using POM.Taxonomy.Ports;

namespace POM.Taxonomy;

public sealed record CategoryDto(Guid Id, Guid? UserId, string Name, bool IsDefault, string? Icon);
public sealed record CategoryCommandResult(CategoryDto? Category, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => Category is not null && ErrorCode is null;
    public static CategoryCommandResult Failure(string code, string message) => new(null, code, message);
}

public sealed class CategoryAppService(ICategoryRepository repository)
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        EnsureUser(userId);
        var categories = await repository.ListVisibleAsync(userId, ct);
        return categories.Select(ToDto).ToArray();
    }

    public async Task<CategoryCommandResult> CreateAsync(Guid userId, string name, string? icon, CancellationToken ct = default)
    {
        EnsureUser(userId);
        try
        {
            var category = Category.CreateUser(userId, name, icon);
            await repository.AddAsync(category, ct);
            await repository.SaveChangesAsync(ct);
            return new CategoryCommandResult(ToDto(category), null, null);
        }
        catch (ArgumentException ex)
        {
            return CategoryCommandResult.Failure("validation_error", ex.Message);
        }
    }

    public async Task<CategoryCommandResult> UpdateAsync(Guid userId, Guid categoryId, string name, string? icon, CancellationToken ct = default)
    {
        EnsureUser(userId);
        var category = await repository.GetOwnedOrDefaultAsync(userId, categoryId, ct);
        if (category is null || category.IsDefault)
            return CategoryCommandResult.Failure("not_found", "Category was not found.");

        try
        {
            category.Update(name, icon);
            await repository.SaveChangesAsync(ct);
            return new CategoryCommandResult(ToDto(category), null, null);
        }
        catch (ArgumentException ex)
        {
            return CategoryCommandResult.Failure("validation_error", ex.Message);
        }
    }

    public async Task<CategoryCommandResult> DeleteAsync(Guid userId, Guid categoryId, CancellationToken ct = default)
    {
        EnsureUser(userId);
        var category = await repository.GetOwnedOrDefaultAsync(userId, categoryId, ct);
        if (category is null || category.IsDefault)
            return CategoryCommandResult.Failure("not_found", "Category was not found.");

        if (await repository.IsReferencedByObligationAsync(userId, categoryId, ct))
            return CategoryCommandResult.Failure("conflict", "Category is referenced by an obligation.");

        repository.Remove(category);
        await repository.SaveChangesAsync(ct);
        return new CategoryCommandResult(new CategoryDto(category.Id, category.UserId, category.Name, category.IsDefault, category.Icon), null, null);
    }

    private static CategoryDto ToDto(Category category) =>
        new(category.Id, category.UserId, category.Name, category.IsDefault, category.Icon);

    private static void EnsureUser(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User id is required.", nameof(userId));
    }
}
