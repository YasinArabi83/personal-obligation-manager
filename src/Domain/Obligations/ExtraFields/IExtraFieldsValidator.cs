namespace POM.Obligations.ExtraFields;

public interface IExtraFieldsValidator
{
    ExtraFieldsValidationResult Validate(ObligationType type, string extraFields);
}