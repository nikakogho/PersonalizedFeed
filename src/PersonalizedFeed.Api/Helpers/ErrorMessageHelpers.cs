namespace PersonalizedFeed.Api.Helpers;

public static class ErrorMessageHelpers
{
    public static string ConstructMissingHeadersMessage(string? tenantId, string? apiKey, string? userHash)
    {
        var missingHeaders = new List<string>();
        if (string.IsNullOrWhiteSpace(tenantId)) missingHeaders.Add("X-Tenant-Id");
        if (string.IsNullOrWhiteSpace(apiKey)) missingHeaders.Add("X-Api-Key");
        if (string.IsNullOrWhiteSpace(userHash)) missingHeaders.Add("X-User");

        string headerOrHeaders = missingHeaders.Count > 1 ? "headers" : "header";
        string errorMessage = $"Missing required {headerOrHeaders}: {string.Join(", ", missingHeaders)}";

        return errorMessage;
    }
}
