namespace SkillSnap.Client.Services;

public static class ApiResponseErrorBuilder
{
    // Centralize API error formatting so services return consistent messages.
    public static async Task<string> BuildAsync(HttpResponseMessage response, string operation)
    {
        var responseBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return $"Unable to {operation}. Server returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
        }

        return $"Unable to {operation}. Server returned {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}";
    }
}
