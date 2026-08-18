namespace POM.Auth.Ports;

/// <summary>
/// The user resolved on a successful OTP verification — enough to mint the login response
/// (id, phone number, display name). <see cref="DisplayName"/> may be <c>null</c> until the user
/// sets one via the account endpoints.
/// </summary>
public sealed record VerifiedUser(Guid Id, string PhoneNumber, string? DisplayName);

/// <summary>
/// Verifies a one-time code against a phone number. Returns the verified user on success, or
/// <c>null</c> on an invalid/expired code. On success the user's <c>PhoneNumberConfirmed</c> flag is
/// flipped (only a successful OTP confirms a number — docs/SECURITY.md §2) and the security stamp is
/// rotated so the consumed OTP cannot be replayed.
/// </summary>
public interface IOtpVerifier
{
    /// <summary>Verifies <paramref name="code"/> for <paramref name="phoneNumber"/>. Returns the user, or <c>null</c> if invalid/expired.</summary>
    Task<VerifiedUser?> VerifyAsync(string phoneNumber, string code, CancellationToken ct);
}
