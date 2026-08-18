using System.Collections.Concurrent;
using POM.Auth.Ports;

namespace POM.Auth.RateLimiting;

/// <summary>
/// In-memory <see cref="IOtpRateLimiter"/> enforcing the per-phone windows in docs/SECURITY.md §2:
/// max <c>3</c> OTP requests per phone per <c>10</c> minutes, max <c>5</c> failed verifies per phone
/// per <c>1</c> hour. Each phone key keeps a sliding window of timestamps; entries older than the
/// window are pruned on access. The built-in <c>AddRateLimiter</c> middleware handles the coarse
/// per-IP gate (it cannot partition on a JSON-body phone number) — this enforces the per-phone window
/// inside the application (plan 0004 Q2). Single-VPS MVP only: state is process-local and lost on
/// restart (no Redis).
/// </summary>
/// <remarks>
/// <see cref="TimeProvider"/> is injected so window logic is deterministically testable with a fake clock.
/// </remarks>
public sealed class InMemoryOtpRateLimiter : IOtpRateLimiter
{
    private static readonly TimeSpan OtpWindow = TimeSpan.FromMinutes(10);
    private const int OtpMax = 3;
    private static readonly TimeSpan VerifyWindow = TimeSpan.FromHours(1);
    private const int VerifyMax = 5;

    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _otpRequests = new();
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _verifyFailures = new();

    public InMemoryOtpRateLimiter(TimeProvider time) => _time = time;

    public Task<RateLimitDecision> CheckOtpRequestAsync(string phoneNumber, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var list = _otpRequests.GetOrAdd(phoneNumber, _ => new List<DateTimeOffset>());
        lock (list)
        {
            list.RemoveAll(t => t <= now - OtpWindow);
            if (list.Count >= OtpMax)
            {
                var oldest = list[0];
                var retry = oldest + OtpWindow - now;
                return Task.FromResult(Deny(retry));
            }
            list.Add(now);
            return Task.FromResult(new RateLimitDecision(true, null));
        }
    }

    public Task<RateLimitDecision> CheckOtpVerifyAsync(string phoneNumber, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var list = _verifyFailures.GetOrAdd(phoneNumber, _ => new List<DateTimeOffset>());
        lock (list)
        {
            list.RemoveAll(t => t <= now - VerifyWindow);
            if (list.Count >= VerifyMax)
            {
                var oldest = list[0];
                var retry = oldest + VerifyWindow - now;
                return Task.FromResult(Deny(retry));
            }
            return Task.FromResult(new RateLimitDecision(true, null));
        }
    }

    public Task RecordVerifyFailureAsync(string phoneNumber, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var list = _verifyFailures.GetOrAdd(phoneNumber, _ => new List<DateTimeOffset>());
        lock (list)
        {
            list.RemoveAll(t => t <= now - VerifyWindow);
            list.Add(now);
        }
        return Task.CompletedTask;
    }

    private static RateLimitDecision Deny(TimeSpan retry)
    {
        var safe = retry < TimeSpan.Zero ? TimeSpan.Zero : retry;
        return new RateLimitDecision(false, safe);
    }
}
