using POM.Taxonomy;
using Xunit;

namespace POM.Domain.Tests.Taxonomy;

public sealed class CategoryTests
{
    [Fact]
    public void User_category_trims_name_and_icon()
    {
        var userId = Guid.NewGuid();
        var category = Category.CreateUser(userId, "  Bills  ", "  receipt  ");

        Assert.Equal("Bills", category.Name);
        Assert.Equal("receipt", category.Icon);
        Assert.Equal(userId, category.UserId);
        Assert.False(category.IsDefault);
    }

    [Fact]
    public void Name_is_required_and_limited()
    {
        Assert.Throws<ArgumentException>(() => Category.CreateUser(Guid.NewGuid(), "  "));
        Assert.Throws<ArgumentException>(() => Category.CreateUser(Guid.NewGuid(), new string('x', 101)));
    }

    [Fact]
    public void System_default_cannot_be_modified()
    {
        var category = Category.CreateDefault(Guid.NewGuid(), "Bills");

        Assert.Throws<InvalidOperationException>(() => category.Update("Other"));
    }
}
