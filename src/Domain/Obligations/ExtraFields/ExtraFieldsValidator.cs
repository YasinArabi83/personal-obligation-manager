using System.Text.Json;
using POM.Obligations;

namespace POM.Obligations.ExtraFields;

public sealed class ExtraFieldsValidator : IExtraFieldsValidator
{
    public ExtraFieldsValidationResult Validate(ObligationType type, string extraFields)
    {
        if (string.IsNullOrWhiteSpace(extraFields))
            return ExtraFieldsValidationResult.Invalid("validation_error", "ExtraFields is required.");

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(extraFields);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return ExtraFieldsValidationResult.Invalid("validation_error", "ExtraFields must be a JSON object.");
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ExtraFieldsValidationResult.Invalid("validation_error", "ExtraFields must be valid JSON.");
        }

        return type switch
        {
            ObligationType.Payment => ValidatePayment(root),
            ObligationType.Document => ValidateDocument(root),
            ObligationType.Subscription => ValidateSubscription(root),
            _ => ExtraFieldsValidationResult.Valid()
        };
    }

    private static ExtraFieldsValidationResult ValidatePayment(JsonElement root)
    {
        if (!root.TryGetProperty("amount", out var amountElement))
            return ExtraFieldsValidationResult.Invalid("validation_error", "Payment requires 'amount' field.");

        if (!IsValidInt64(amountElement))
            return ExtraFieldsValidationResult.Invalid("validation_error", "Payment 'amount' must be a signed 64-bit integer (Rial).");

        if (root.TryGetProperty("currency", out var currencyElement) &&
            currencyElement.ValueKind != JsonValueKind.String)
        {
            return ExtraFieldsValidationResult.Invalid("validation_error", "Payment 'currency' must be a string.");
        }

        return ExtraFieldsValidationResult.Valid();
    }

    private static ExtraFieldsValidationResult ValidateDocument(JsonElement root)
    {
        if (!root.TryGetProperty("documentType", out var docTypeElement))
            return ExtraFieldsValidationResult.Invalid("validation_error", "Document requires 'documentType' field.");

        if (docTypeElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(docTypeElement.GetString()))
            return ExtraFieldsValidationResult.Invalid("validation_error", "Document 'documentType' must be a non-empty string.");

        return ExtraFieldsValidationResult.Valid();
    }

    private static ExtraFieldsValidationResult ValidateSubscription(JsonElement root)
    {
        if (!root.TryGetProperty("amount", out var amountElement))
            return ExtraFieldsValidationResult.Invalid("validation_error", "Subscription requires 'amount' field.");

        if (!IsValidInt64(amountElement))
            return ExtraFieldsValidationResult.Invalid("validation_error", "Subscription 'amount' must be a signed 64-bit integer (Rial).");

        if (!root.TryGetProperty("billingCycle", out var cycleElement))
            return ExtraFieldsValidationResult.Invalid("validation_error", "Subscription requires 'billingCycle' field.");

        if (cycleElement.ValueKind != JsonValueKind.String)
            return ExtraFieldsValidationResult.Invalid("validation_error", "Subscription 'billingCycle' must be a string.");

        var cycle = cycleElement.GetString();
        if (cycle != "Monthly" && cycle != "Yearly")
            return ExtraFieldsValidationResult.Invalid("validation_error", "Subscription 'billingCycle' must be 'Monthly' or 'Yearly'.");

        if (!root.TryGetProperty("nextChargeDate", out var nextChargeElement))
            return ExtraFieldsValidationResult.Invalid("validation_error", "Subscription requires 'nextChargeDate' field.");

        if (nextChargeElement.ValueKind != JsonValueKind.String ||
            !DateTime.TryParse(nextChargeElement.GetString(), out _))
        {
            return ExtraFieldsValidationResult.Invalid("validation_error", "Subscription 'nextChargeDate' must be a valid ISO 8601 UTC datetime.");
        }

        return ExtraFieldsValidationResult.Valid();
    }

    private static bool IsValidInt64(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out _),
            JsonValueKind.String => long.TryParse(element.GetString(), out _),
            _ => false
        };
    }
}