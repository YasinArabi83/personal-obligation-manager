namespace POM.Auth.Ports;

/// <summary>
/// Generates and dispatches a one-time login code for a phone number via ASP.NET Core Identity's
/// phone-number token provider. The generated code is returned so the (dev) SMS sender can surface
/// it; production hands it to <see cref="ISmsSender"/> (ADR-0016). There is <b>no</b> fixed/constant
/// OTP and no backdoor — the code is always framework-generated (plan 0004 Q5).
/// </summary>
public interface IOtpSender
{
    /// <summary>
    /// Generates an OTP for <paramref name="phoneNumber"/> and dispatches it. The user row is
    /// ensured to exist here (find-or-create) so the token's entropy (<c>SecurityStamp</c>) is
    /// stable and verifiable later — generation requires a persisted stamp. Returns the generated code.
    /// </summary>
    Task<string> SendOtpAsync(string phoneNumber, CancellationToken ct);
}
