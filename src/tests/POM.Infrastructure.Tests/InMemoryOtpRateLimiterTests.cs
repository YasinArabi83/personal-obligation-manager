using POM.Auth.RateLimiting;

namespace POM.Infrastructure.Tests;

/// <summary>
/// Unit tests for the per-phone OTP rate-limit windows (docs/SECURITY.md §2): 3 OTP requests / 10 min,
/// 5 failed verifies / 1 h. Uses a <see cref="FakeTimeProvider"/> so the sliding windows are exercised
/// without real-time waits.
/// </summary>
public sealed class InMemoryOtpRateLimiterTests
{
    private static InMemoryOtpRateLimiter Create(FakeTimeProvider clock) => new(clock);

    [Fact]
    public async Task Allows_up_to_three_otp_requests_then_denies_until_window_clears()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var limiter = Create(clock);

        Assert.True((await limiter.CheckOtpRequestAsync("+9891", default)).Allowed);
        Assert.True((await limiter.CheckOtpRequestAsync("+9891", default)).Allowed);
        Assert.True((await limiter.CheckOtpRequestAsync("+9891", default)).Allowed);

        var denied = await limiter.CheckOtpRequestAsync("+9891", default);
        Assert.False(denied.Allowed);
        Assert.NotNull(denied.RetryAfter);

        // After the 10-minute window passes, requests are allowed again.
        clock.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));
        Assert.True((await limiter.CheckOtpRequestAsync("+9891", default)).Allowed);
    }

    [Fact]
    public async Task Otp_windows_are_isolated_per_phone()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var limiter = Create(clock);

        // Exhaust phone A.
        await limiter.CheckOtpRequestAsync("A", default);
        await limiter.CheckOtpRequestAsync("A", default);
        await limiter.CheckOtpRequestAsync("A", default);

        // Phone B is unaffected.
        Assert.True((await limiter.CheckOtpRequestAsync("B", default)).Allowed);
        Assert.False((await limiter.CheckOtpRequestAsync("A", default)).Allowed);
    }

    [Fact]
    public async Task Locks_out_after_five_failed_verifies_within_an_hour()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var limiter = Create(clock);

        // Five failed verifies are recorded; each preceding check is still allowed.
        for (var i = 0; i < 5; i++)
        {
            Assert.True((await limiter.CheckOtpVerifyAsync("+9892", default)).Allowed);
            await limiter.RecordVerifyFailureAsync("+9892", default);
        }

        // The sixth attempt is locked out.
        Assert.False((await limiter.CheckOtpVerifyAsync("+9892", default)).Allowed);

        // After the 1-hour window, verify attempts are allowed again.
        clock.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));
        Assert.True((await limiter.CheckOtpVerifyAsync("+9892", default)).Allowed);
    }
}
