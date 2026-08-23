namespace POM.Obligations;

public sealed record ObligationCommandResult(ObligationDto? Obligation, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => Obligation is not null && ErrorCode is null;
    public static ObligationCommandResult Failure(string code, string message) => new(null, code, message);
}

public sealed record ObligationActionResult(bool Succeeded, string? ErrorCode, string? ErrorMessage)
{
    public static ObligationActionResult Success() => new(true, null, null);
    public static ObligationActionResult Failure(string code, string message) => new(false, code, message);
}

public sealed record ObligationListResult(ObligationListResponse? Response, string? ErrorCode, string? ErrorMessage)
{
    public bool Succeeded => Response is not null && ErrorCode is null;
    public static ObligationListResult Failure(string code, string message) => new(null, code, message);
}