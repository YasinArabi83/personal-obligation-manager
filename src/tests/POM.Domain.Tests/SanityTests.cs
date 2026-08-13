using Xunit;

namespace POM.Domain.Tests;

/// <summary>
/// Sanity test: proves the test project discovers, builds, and runs green
/// end-to-end from the very first task. Real domain unit tests arrive with
/// the features that ship them (recurrence engine, value objects, ...).
/// </summary>
public class SanityTests
{
    [Fact]
    public void The_truth_is_true()
    {
        Assert.True(true);
    }
}
