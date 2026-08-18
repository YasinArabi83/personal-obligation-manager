namespace POM.Users;

/// <summary>
/// The application user. Authentication is OTP-only via ASP.NET Core Identity's phone-number
/// token provider — there is <b>no password field anywhere</b> on this entity (docs/AGENTS.md
/// §0/§2, ADR-0001). The phone number is the user's identity; it doubles as the Identity
/// username and must be unique.
/// </summary>
/// <remarks>
/// <see cref="Id"/> is app-assigned (<see cref="Guid.NewGuid"/>) in <see cref="Create"/> so the
/// domain controls identity and the id is usable before persistence (Q3 / ADR-0016); EF is told
/// the PK is never DB-generated. <see cref="SecurityStamp"/> is the per-user entropy Identity's
/// TOTP token provider needs; it is infrastructure-managed state carried on the user row
/// (ADR-0016, Q1b), initialized at creation and rotatable on phone-number change.
/// </remarks>
public class User
{
    /// <summary>Private ctor for EF Core materialization. Use <see cref="Create"/> to build one.</summary>
    private User() { }

    public Guid Id { get; internal set; }
    public string PhoneNumber { get; internal set; } = null!;
    public bool PhoneNumberConfirmed { get; internal set; }
    public string? DisplayName { get; internal set; }
    public CalendarPreference CalendarPreference { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public DateTime? DeletedAt { get; internal set; }

    /// <summary>Identity token entropy. Not a domain concept — managed by the auth store.</summary>
    public string SecurityStamp { get; internal set; } = null!;

    /// <summary>
    /// Factory enforcing creation invariants. The phone number is trimmed and validated for
    /// non-empty + digit content. Defaults: <see cref="PhoneNumberConfirmed"/> = false,
    /// <see cref="CalendarPreference"/> = <see cref="CalendarPreference.Jalali"/>,
    /// <see cref="CreatedAt"/> = now (UTC), and a fresh <see cref="SecurityStamp"/>.
    /// </summary>
    public static User Create(string phoneNumber)
    {
        var normalized = NormalizePhoneNumber(phoneNumber);
        return new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = normalized,
            PhoneNumberConfirmed = false,
            DisplayName = null,
            CalendarPreference = CalendarPreference.Jalali,
            CreatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
    }

    /// <summary>Marks the phone number as confirmed (after a successful OTP verification).</summary>
    public void ConfirmPhoneNumber() => PhoneNumberConfirmed = true;

    /// <summary>Rotates the security stamp (e.g. on phone-number change). Invalidates any in-flight tokens.</summary>
    public void RegenerateSecurityStamp() => SecurityStamp = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Minimal, deterministic normalization: trim whitespace and reject empty / non-digit input.
    /// Full E.164 parsing is a presentation-layer concern; the domain only guarantees a stable,
    /// non-empty identifier is stored. Throws <see cref="ArgumentException"/> on invalid input.
    /// </summary>
    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (phoneNumber is null)
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        var trimmed = phoneNumber.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Phone number must not be empty.", nameof(phoneNumber));

        // A phone number must contain at least some digits; a stray '+' alone is not valid.
        var hasDigit = false;
        foreach (var c in trimmed)
        {
            if (char.IsDigit(c)) hasDigit = true;
        }
        if (!hasDigit)
            throw new ArgumentException("Phone number must contain at least one digit.", nameof(phoneNumber));

        return trimmed;
    }
}
