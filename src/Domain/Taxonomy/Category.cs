namespace POM.Taxonomy;

public sealed class Category
{
    private Category() { }

    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public bool IsDefault { get; private set; }
    public string? Icon { get; private set; }

    public static Category CreateUser(Guid userId, string name, string? icon = null, Guid? id = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User id is required.", nameof(userId));
        return Create(id ?? Guid.NewGuid(), userId, name, false, icon);
    }

    public static Category CreateDefault(Guid id, string name, string? icon = null) =>
        Create(id, null, name, true, icon);

    public void Update(string name, string? icon = null)
    {
        if (IsDefault) throw new InvalidOperationException("System default categories cannot be modified.");
        Name = NormalizeName(name);
        Icon = NormalizeIcon(icon);
    }

    private static Category Create(Guid id, Guid? userId, string name, bool isDefault, string? icon)
    {
        if (id == Guid.Empty) throw new ArgumentException("Category id is required.", nameof(id));
        if (isDefault && userId.HasValue) throw new ArgumentException("System defaults cannot have an owner.", nameof(userId));
        if (!isDefault && (!userId.HasValue || userId.Value == Guid.Empty))
            throw new ArgumentException("User-owned categories require an owner.", nameof(userId));

        return new Category
        {
            Id = id,
            UserId = userId,
            Name = NormalizeName(name),
            IsDefault = isDefault,
            Icon = NormalizeIcon(icon),
        };
    }

    private static string NormalizeName(string name)
    {
        if (name is null) throw new ArgumentException("Category name is required.", nameof(name));
        var normalized = name.Trim();
        if (normalized.Length == 0) throw new ArgumentException("Category name is required.", nameof(name));
        if (normalized.Length > 100) throw new ArgumentException("Category name must be 100 characters or fewer.", nameof(name));
        return normalized;
    }

    private static string? NormalizeIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        var normalized = icon.Trim();
        if (normalized.Length > 100) throw new ArgumentException("Category icon must be 100 characters or fewer.", nameof(icon));
        return normalized;
    }
}
