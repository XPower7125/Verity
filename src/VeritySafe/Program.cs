using System;
using System.Windows.Forms;
using Verity.Shared;

namespace VeritySafe
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            // Env vars still win, so scripted launches can bypass the registry.
            var settings = Settings.Load();
            var key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            var model = Environment.GetEnvironmentVariable("VERITY_MODEL");
            if (!string.IsNullOrWhiteSpace(key)) settings.ApiKey = key.Trim();
            if (!string.IsNullOrWhiteSpace(model)) settings.Model = model.Trim();

            Application.Run(new VerityHost(VerityMode.Safe, settings));
        }
    }
}