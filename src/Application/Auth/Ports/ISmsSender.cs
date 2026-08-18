namespace POM.Auth.Ports;

/// <summary>
/// Delivers an SMS message to a phone number. This is the Application-layer port (a provider-agnostic
/// boundary per docs/ARCHITECTURE.md §3 and AGENTS.md §2); the concrete adapter (sms.ir in task 0017,
/// or the dev-only <c>LoggingSmsSender</c> for now) lives in Infrastructure. SMS is the primary
/// notification/auth channel (ADR-0002).
/// </summary>
public interface ISmsSender
{
    /// <summary>Send <paramref name="message"/> to <paramref name="phoneNumber"/>. Throws on delivery failure.</summary>
    Task SendAsync(string phoneNumber, string message, CancellationToken ct);
}
