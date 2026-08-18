using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POM.Persistence;
using POM.Users;

namespace POM.Identity;

/// <summary>
/// Custom ASP.NET Core Identity user store backed by the clean <c>users</c> table (not
/// <c>AspNetUsers</c>) via <see cref="PomDbContext.Users"/>. Implements only the OTP-relevant
/// store interfaces — no password/role/claim/email/login stores — so Identity never demands a
/// password column and the schema stays exactly as documented in DATABASE.md §3 (ADR-0016, Q1).
/// Translates between the Infrastructure-internal <see cref="ApplicationUser"/> adapter and the
/// pure domain <see cref="User"/> aggregate.
/// </summary>
internal sealed class UserStore :
    IUserStore<ApplicationUser>,
    IUserPhoneNumberStore<ApplicationUser>,
    IUserSecurityStampStore<ApplicationUser>
{
    private readonly PomDbContext _db;

    public UserStore(PomDbContext db) => _db = db;

    // ---- IUserStore<ApplicationUser> ----

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.Id.ToString());

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.UserName);

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var domain = User.Create(ResolvePhone(user));
        // Honor an explicitly-assigned id/stamp (e.g. a pre-set Guid); otherwise the factory's win.
        if (user.Id != Guid.Empty) domain.Id = user.Id;
        if (!string.IsNullOrEmpty(user.SecurityStamp)) domain.SecurityStamp = user.SecurityStamp;
        domain.PhoneNumberConfirmed = user.PhoneNumberConfirmed;

        await _db.Users.AddAsync(domain, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // Write the canonical values back so Identity sees the persisted id/stamp.
        SyncBack(user, domain);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var domain = await _db.Users.FindAsync(new object[] { user.Id }, cancellationToken);
        if (domain is null)
            return IdentityResult.Failed(new IdentityError { Code = "UserNotFound", Description = "User not found." });

        domain.PhoneNumber = ResolvePhone(user);
        domain.PhoneNumberConfirmed = user.PhoneNumberConfirmed;
        domain.SecurityStamp = user.SecurityStamp ?? domain.SecurityStamp;

        await _db.SaveChangesAsync(cancellationToken);
        SyncBack(user, domain);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var domain = await _db.Users.FindAsync(new object[] { user.Id }, cancellationToken);
        if (domain is null)
            return IdentityResult.Success; // idempotent

        _db.Users.Remove(domain);
        await _db.SaveChangesAsync(cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var id)) return null;
        var domain = await _db.Users.FindAsync(new object[] { id }, cancellationToken);
        return domain is null ? null : ToApplicationUser(domain);
    }

    public async Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        // Phone numbers are digits/+ — case normalization is a no-op, so the stored phone matches
        // the normalized name directly. Phone uniqueness guarantees a single match.
        var domain = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PhoneNumber == normalizedUserName, cancellationToken);
        return domain is null ? null : ToApplicationUser(domain);
    }

    // ---- IUserPhoneNumberStore<ApplicationUser> ----

    public Task<string?> GetPhoneNumberAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.PhoneNumber);

    public Task SetPhoneNumberAsync(ApplicationUser user, string? phoneNumber, CancellationToken cancellationToken)
    {
        user.PhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    public Task<bool> GetPhoneNumberConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
        => Task.FromResult(user.PhoneNumberConfirmed);

    public Task SetPhoneNumberConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }

    // ---- IUserSecurityStampStore<ApplicationUser> ----

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(string.IsNullOrEmpty(user.SecurityStamp)
            ? null
            : user.SecurityStamp);
    }

    public Task SetSecurityStampAsync(ApplicationUser user, string stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public void Dispose() { }

    // ---- helpers ----

    private static string ResolvePhone(ApplicationUser user)
    {
        var phone = user.PhoneNumber ?? user.UserName;
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("ApplicationUser has neither PhoneNumber nor UserName.");
        return phone.Trim();
    }

    private static ApplicationUser ToApplicationUser(User domain) => new()
    {
        Id = domain.Id,
        UserName = domain.PhoneNumber,
        NormalizedUserName = domain.PhoneNumber.ToUpper(CultureInfo.InvariantCulture),
        PhoneNumber = domain.PhoneNumber,
        PhoneNumberConfirmed = domain.PhoneNumberConfirmed,
        SecurityStamp = domain.SecurityStamp,
    };

    private static void SyncBack(ApplicationUser user, User domain)
    {
        user.Id = domain.Id;
        user.UserName = domain.PhoneNumber;
        user.NormalizedUserName = domain.PhoneNumber.ToUpper(CultureInfo.InvariantCulture);
        user.PhoneNumber = domain.PhoneNumber;
        user.PhoneNumberConfirmed = domain.PhoneNumberConfirmed;
        user.SecurityStamp = domain.SecurityStamp;
    }
}
