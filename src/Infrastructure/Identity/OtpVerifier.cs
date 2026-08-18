using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using POM.Auth.Ports;
using POM.Persistence;

namespace POM.Identity;

/// <summary>
/// <see cref="IOtpVerifier"/> over ASP.NET Core Identity's token verification. On a valid code it
/// confirms the phone number (only a successful OTP confirms a number — docs/SECURITY.md §2) and
/// rotates the <c>SecurityStamp</c> so the consumed code cannot be replayed. Returns the verified
/// user's id, phone, and display name. Returns <c>null</c> on an invalid/expired code or unknown user.
/// </summary>
internal sealed class OtpVerifier : IOtpVerifier
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PomDbContext _db;

    public OtpVerifier(UserManager<ApplicationUser> userManager, PomDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<VerifiedUser?> VerifyAsync(string phoneNumber, string code, CancellationToken ct)
    {
        var user = await _userManager.FindByNameAsync(phoneNumber);
        if (user is null)
            return null;

        var valid = await _userManager.VerifyUserTokenAsync(
            user, TokenOptions.DefaultPhoneProvider, "PhoneNumber", code);
        if (!valid)
            return null;

        if (!user.PhoneNumberConfirmed)
        {
            user.PhoneNumberConfirmed = true;
            await _userManager.UpdateAsync(user);
        }

        // Rotate entropy so this OTP is single-use. Subsequent generation/verification uses the new stamp.
        await _userManager.UpdateSecurityStampAsync(user);

        // Carry the display name from the domain row (the Identity adapter doesn't model it).
        var displayName = await _db.Users.AsNoTracking()
            .Where(u => u.Id == user.Id)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(ct);

        return new VerifiedUser(user.Id, user.PhoneNumber ?? phoneNumber, displayName);
    }
}
