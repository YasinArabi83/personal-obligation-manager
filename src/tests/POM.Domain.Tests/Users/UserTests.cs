using POM.Users;
using Xunit;

namespace POM.Domain.Tests.Users;

/// <summary>
/// Unit tests for the <see cref="User"/> aggregate factory and invariants. No infrastructure
/// dependencies — pure domain logic (docs/TESTING.md §2).
/// </summary>
public sealed class UserTests
{
    [Theory]
    [InlineData("  +989121234567  ")]
    [InlineData("+989121234567")]
    public void Create_normalizes_phone_number_by_trimming(string phone)
    {
        var user = User.Create(phone);

        Assert.Equal("+989121234567", user.PhoneNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_phone(string phone)
    {
        Assert.Throws<ArgumentException>(() => User.Create(phone));
    }

    [Theory]
    [InlineData("+")]
    [InlineData("call me")]
    public void Create_rejects_phone_without_digits(string phone)
    {
        Assert.Throws<ArgumentException>(() => User.Create(phone));
    }

    [Fact]
    public void Create_sets_sensible_defaults()
    {
        var user = User.Create("+989120000000");

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.False(user.PhoneNumberConfirmed);
        Assert.Null(user.DisplayName);
        Assert.Equal(CalendarPreference.Jalali, user.CalendarPreference);
        Assert.Null(user.DeletedAt);
        Assert.True((DateTime.UtcNow - user.CreatedAt).Duration() < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_assigns_a_security_stamp()
    {
        var user = User.Create("+989120000000");

        Assert.False(string.IsNullOrWhiteSpace(user.SecurityStamp));
    }

    [Fact]
    public void Create_assigns_a_unique_id_and_stamp_per_user()
    {
        var a = User.Create("+989120000001");
        var b = User.Create("+989120000002");

        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEqual(a.SecurityStamp, b.SecurityStamp);
    }

    [Fact]
    public void ConfirmPhoneNumber_flips_the_flag()
    {
        var user = User.Create("+989120000000");

        Assert.False(user.PhoneNumberConfirmed);

        user.ConfirmPhoneNumber();

        Assert.True(user.PhoneNumberConfirmed);
    }

    [Fact]
    public void RegenerateSecurityStamp_changes_the_stamp()
    {
        var user = User.Create("+989120000000");
        var original = user.SecurityStamp;

        user.RegenerateSecurityStamp();

        Assert.NotEqual(original, user.SecurityStamp);
    }
}
