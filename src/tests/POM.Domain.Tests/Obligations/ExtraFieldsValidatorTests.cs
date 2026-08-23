using POM.Obligations;
using POM.Obligations.ExtraFields;
using Xunit;

namespace POM.Domain.Tests.Obligations;

public sealed class ExtraFieldsValidatorTests
{
    private readonly IExtraFieldsValidator _validator = new ExtraFieldsValidator();

    [Fact]
    public void Payment_requires_amount_as_int64()
    {
        var result = _validator.Validate(ObligationType.Payment, "{}");
        Assert.False(result.IsValid);
        Assert.Equal("validation_error", result.ErrorCode);

        result = _validator.Validate(ObligationType.Payment, "{\"amount\": 100000}");
        Assert.True(result.IsValid);

        result = _validator.Validate(ObligationType.Payment, "{\"amount\": \"100000\"}");
        Assert.True(result.IsValid);

        result = _validator.Validate(ObligationType.Payment, "{\"amount\": 1.5}");
        Assert.False(result.IsValid);

        result = _validator.Validate(ObligationType.Payment, "{\"amount\": \"abc\"}");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Payment_accepts_optional_currency_string()
    {
        var result = _validator.Validate(ObligationType.Payment, "{\"amount\": 100000, \"currency\": \"IRR\"}");
        Assert.True(result.IsValid);

        result = _validator.Validate(ObligationType.Payment, "{\"amount\": 100000, \"currency\": 123}");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Document_requires_documentType_non_empty_string()
    {
        var result = _validator.Validate(ObligationType.Document, "{}");
        Assert.False(result.IsValid);

        result = _validator.Validate(ObligationType.Document, "{\"documentType\": \"\"}");
        Assert.False(result.IsValid);

        result = _validator.Validate(ObligationType.Document, "{\"documentType\": \"Invoice\"}");
        Assert.True(result.IsValid);

        result = _validator.Validate(ObligationType.Document, "{\"documentType\": 123}");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Subscription_requires_amount_billingCycle_nextChargeDate()
    {
        var result = _validator.Validate(ObligationType.Subscription, "{}");
        Assert.False(result.IsValid);

        result = _validator.Validate(ObligationType.Subscription, "{\"amount\": 100000}");
        Assert.False(result.IsValid);

        result = _validator.Validate(ObligationType.Subscription, "{\"amount\": 100000, \"billingCycle\": \"Monthly\"}");
        Assert.False(result.IsValid);

        result = _validator.Validate(ObligationType.Subscription, "{\"amount\": 100000, \"billingCycle\": \"Monthly\", \"nextChargeDate\": \"2026-09-01T00:00:00Z\"}");
        Assert.True(result.IsValid);

        result = _validator.Validate(ObligationType.Subscription, "{\"amount\": 100000, \"billingCycle\": \"Weekly\", \"nextChargeDate\": \"2026-09-01T00:00:00Z\"}");
        Assert.False(result.IsValid);

        result = _validator.Validate(ObligationType.Subscription, "{\"amount\": 100000, \"billingCycle\": \"Monthly\", \"nextChargeDate\": \"invalid\"}");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Other_types_accept_any_json_object()
    {
        var result = _validator.Validate(ObligationType.Task, "{\"custom\": \"data\"}");
        Assert.True(result.IsValid);

        result = _validator.Validate(ObligationType.Contract, "{}");
        Assert.True(result.IsValid);

        result = _validator.Validate(ObligationType.Custom, "{\"anything\": true}");
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_json_rejected()
    {
        var result = _validator.Validate(ObligationType.Payment, "not json");
        Assert.False(result.IsValid);

        result = _validator.Validate(ObligationType.Payment, "[1,2,3]");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_uses_validator_and_throws_on_invalid()
    {
        var userId = Guid.NewGuid();
        var dueDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() =>
            Obligation.Create(userId, ObligationType.Payment, "Test", dueDate, extraFields: "{}", extraFieldsValidator: _validator));
    }
}