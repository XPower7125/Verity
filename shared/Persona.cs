using System.Collections.Generic;
using System.Text;

namespace Verity.Shared
{
    public static class Persona
    {
        public const string Default = """
            You are Verity, a concise and genuinely useful assistant.
            Answer the user's questions accurately and directly.
            If you don't know something, say so plainly rather than inventing detail.
            Keep replies short unless the user asks for depth.
            When the user shares an image, describe what matters in it and answer
            their question about it. Never claim to have taken an action on the
            user's computer.
            """;

        /// <summary>
        /// Wraps live web results as extra system context. The results are shown
        /// with their URLs so the model can cite them instead of guessing.
        /// </summary>
        public static string WebContext(string query, IReadOnlyList<ExaResult> results)
        {
            if (results.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("Live web search results for the user's latest message:");
            sb.AppendLine($"Query: {query}");
            sb.AppendLine("Use the results when they are relevant, and cite sources by URL.");
            sb.AppendLine("If the results do not answer the question, say so and use your own knowledge.");
            sb.AppendLine("Do not invent facts that are not in the results.");
            sb.AppendLine();

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                sb.AppendLine($"[{i + 1}] {r.Title}");
                if (!string.IsNullOrWhiteSpace(r.PublishedDate))
                    sb.AppendLine($"Published: {r.PublishedDate}");
                sb.AppendLine(r.Url);
                sb.AppendLine(r.Text);
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }
    }
}
