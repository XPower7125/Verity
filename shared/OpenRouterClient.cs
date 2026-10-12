using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Verity.Shared
{
    /// <summary>Minimal OpenRouter chat-completions client.</summary>
    public sealed class OpenRouterClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;

        public OpenRouterClient(string apiKey, string model)
        {
            _apiKey = apiKey;
            _model = string.IsNullOrWhiteSpace(model) ? "anthropic/claude-sonnet-4.5" : model.Trim();
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _http.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/local/verity");
            _http.DefaultRequestHeaders.Add("X-Title", "Verity");
        }

        public async System.Threading.Tasks.Task<string> CompleteAsync(
            IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
        {
            var messages = new List<object>();
            foreach (var t in history)
            {
                messages.Add(new Dictionary<string, string>
                {
                    ["role"] = t.Role,
                    ["content"] = t.Content
                });
            }

            var payload = new Dictionary<string, object>
            {
                ["model"] = _model,
                ["messages"] = messages,
                ["temperature"] = 0.8
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!res.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    "OpenRouter returned " + (int)res.StatusCode + ": " + Trim(body, 400));
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                throw new InvalidOperationException("OpenRouter response contained no choices.");

            var message = choices[0].GetProperty("message");
            var content = message.GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("OpenRouter returned an empty message.");

            return content!;
        }

        private static string Trim(string s, int max)
        {
            s = s.Replace('\n', ' ').Replace('\r', ' ');
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }

    public readonly struct ChatTurn
    {
        public string Role { get; }
        public string Content { get; }

        public ChatTurn(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}