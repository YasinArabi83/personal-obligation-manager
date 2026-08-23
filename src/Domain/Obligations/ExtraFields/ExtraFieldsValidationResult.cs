namespace POM.Obligations.ExtraFields;

public sealed record ExtraFieldsValidationResult(bool IsValid, string? ErrorCode, string? ErrorMessage)
{
    public static ExtraFieldsValidationResult Valid() => new(true, null, null);
    public static ExtraFieldsValidationResult Invalid(string code, string message) => new(false, code, message);
}