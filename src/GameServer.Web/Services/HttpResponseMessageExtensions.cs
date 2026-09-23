using System.Net.Http;
using System.Text.Json;

namespace GameServer.Web.Services;

/// <summary>
/// Provides extension methods for HttpResponseMessage to extract detailed error messages from API responses.
/// </summary>
public static class HttpResponseMessageExtensions
{
    /// <summary>
    /// Throws an <see cref="HttpRequestException"/> if the response status is not successful,
    /// extracting the detailed error or ProblemDetails message sent by the server.
    /// </summary>
    public static async Task EnsureSuccessStatusCodeWithDetailsAsync(
        this HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? serverDetail = null;
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(content))
            {
                serverDetail = ExtractDetailFromResponseContent(content);
            }
        }
        catch
        {
            // If reading response body fails, fall back to standard message
        }

        var message = !string.IsNullOrWhiteSpace(serverDetail)
            ? $"HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {serverDetail}"
            : $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}).";

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    /// <summary>
    /// Extracts a user-facing error message from response content (such as RFC 7807 ProblemDetails JSON or plain text).
    /// </summary>
    public static string ExtractDetailFromResponseContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                // RFC 7807 ProblemDetails often has "detail" or "title" or "errors"
                if (root.TryGetProperty("detail", out var detailProp) && detailProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(detailProp.GetString()))
                {
                    return detailProp.GetString()!;
                }

                if (root.TryGetProperty("errors", out var errorsProp) && errorsProp.ValueKind == JsonValueKind.Object)
                {
                    var errorList = new List<string>();
                    foreach (var prop in errorsProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                                {
                                    errorList.Add(item.GetString()!);
                                }
                            }
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                        {
                            errorList.Add(prop.Value.GetString()!);
                        }
                    }

                    if (errorList.Count > 0)
                    {
                        return string.Join("; ", errorList);
                    }
                }

                if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(msgProp.GetString()))
                {
                    return msgProp.GetString()!;
                }

                if (root.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(errProp.GetString()))
                {
                    return errProp.GetString()!;
                }

                if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(titleProp.GetString()))
                {
                    return titleProp.GetString()!;
                }
            }
        }
        catch
        {
            // Not JSON or parsing failed, return raw content trimmed
        }

        return content.Trim();
    }
}
