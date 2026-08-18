namespace POM.Infrastructure.Tests;

/// <summary>
/// Minimal controllable <see cref="TimeProvider"/> for deterministic expiry/window tests. Avoids the
/// <c>Microsoft.Extensions.Time.Testing</c> dependency (whose .NET 10 build isn't on our feed yet).
/// Only <see cref="GetUtcNow"/> is overridden — the token service and rate limiter use nothing else.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public FakeTimeProvider(DateTimeOffset start) => _now = start;
    public FakeTimeProvider() : this(DateTimeOffset.UtcNow) { }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now += delta;
    public void SetUtcNow(DateTimeOffset value) => _now = value;
}
