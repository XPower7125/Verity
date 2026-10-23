using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Verity.Shared
{
    public sealed record ExaResult(string Title, string Url, string Text, string? PublishedDate);

    /// <summary>Minimal Exa search client. Only /search is used.</summary>
    public sealed class ExaClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public ExaClient(string apiKey)
        {
            _apiKey = apiKey.Trim();
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public async System.Threading.Tasks.Task<IReadOnlyList<ExaResult>> SearchAsync(
            string query, CancellationToken ct = default)
        {
            var payload = new
            {
                query,
                type = "auto",
                numResults = 5,
                contents = new
                {
                    text = new { maxCharacters = 1200 }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.exa.ai/search");
            req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    "Exa returned " + (int)res.StatusCode + ": " + Trim(body, 400));
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<ExaResult>();
            }

            var list = new List<ExaResult>();
            foreach (var item in results.EnumerateArray())
            {
                var url = Str(item, "url");
                if (string.IsNullOrWhiteSpace(url)) continue;

                var title = Str(item, "title") ?? "(untitled)";
                var text = Str(item, "text") ?? Str(item, "summary") ?? Highlights(item);
                list.Add(new ExaResult(
                    title,
                    url,
                    Trim(text, 1200),
                    Str(item, "publishedDate")));
            }
            return list;
        }

        private static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static string Highlights(JsonElement item)
        {
            if (!item.TryGetProperty("highlights", out var highlights) ||
                highlights.ValueKind != JsonValueKind.Array)
            {
                return "";
            }

            var parts = new List<string>();
            foreach (var part in highlights.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                    parts.Add(part.GetString() ?? "");
            }
            return string.Join("\n", parts);
        }

        private static string Trim(string s, int max)
        {
            s = s.Trim().Replace("\r", "");
            return s.Length <= max ? s : s.Substring(0, max).TrimEnd() + "...";
        }
    }
}
