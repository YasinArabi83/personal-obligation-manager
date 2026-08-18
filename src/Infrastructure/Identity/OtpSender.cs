using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using POM.Auth.Ports;

namespace POM.Identity;

/// <summary>
/// <see cref="IOtpSender"/> over ASP.NET Core Identity's built-in <c>PhoneNumberTokenProvider</c>.
/// Ensures the user row exists (find-or-create at <b>request</b> time — a deviation from plan 0004 Q7,
/// see note below) so the token's <c>SecurityStamp</c> entropy is persisted and the code is verifiable
/// later, generates the code, and dispatches it via <see cref="ISmsSender"/>.
/// </summary>
/// <remarks>
/// <b>Find-or-create happens here, not in <c>OtpVerifier</c></b> (plan 0004 Q7 placed it in the
/// verifier). Reason: Identity's TOTP is derived from the user's <c>SecurityStamp</c>, so a code can
/// only be generated — and later verified — against a <i>persisted</i> stamp. A transient in-memory
/// user would get a throwaway stamp that never matches the row created at verify time. Creating the
/// user row at request is harmless: the row is inert until <see cref="OtpVerifier"/> flips
/// <c>PhoneNumberConfirmed</c> on a successful verify, and the per-phone rate limiter bounds row
/// creation. Logged as ADR-0017.
/// </remarks>
internal sealed class OtpSender : IOtpSender
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISmsSender _smsSender;
    private readonly ILogger<OtpSender> _logger;

    public OtpSender(UserManager<ApplicationUser> userManager, ISmsSender smsSender, ILogger<OtpSender> logger)
    {
        _userManager = userManager;
        _smsSender = smsSender;
        _logger = logger;
    }

    public async Task<string> SendOtpAsync(string phoneNumber, CancellationToken ct)
    {
        var user = await _userManager.FindByNameAsync(phoneNumber);
        if (user is null)
        {
            user = new ApplicationUser { UserName = phoneNumber, PhoneNumber = phoneNumber };
            var created = await _userManager.CreateAsync(user);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create user for OTP: " + string.Join(", ", created.Errors.Select(e => e.Description)));
            }
        }

        var code = await _userManager.GenerateUserTokenAsync(
            user, TokenOptions.DefaultPhoneProvider, "PhoneNumber");

        await _smsSender.SendAsync(phoneNumber, $"Your verification code is {code}", ct);
        _logger.LogInformation("OTP generated and dispatched for {PhoneNumber}", phoneNumber);
        return code;
    }
}
