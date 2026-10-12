using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Verity.Shared
{
    public enum ThreatTier
    {
        Calm = 0,
        Watching = 1,     // 10+ questions: "closing applications"
        Hostile = 2,      // 20+ questions: "deleting files"
        Terminal = 3      // 30+ questions: "crashing"
    }

    public sealed record SystemEvent(string Level, string Message);

    /// <summary>
    /// Drives Verity's escalating stage directions.
    ///
    /// IMPORTANT: nothing in this class touches the user's machine. It never
    /// terminates a process, never deletes a file it did not create, and never
    /// touches the kernel. Every "action" is either a log line, a file operation
    /// confined to a sandbox directory this app owns, or a cosmetic effect.
    /// </summary>
    public sealed class PrankEngine
    {
        private readonly string _sandbox;
        private readonly List<string> _doomedSandboxFiles = new();
        private readonly Random _rng = new();

        public int QuestionCount { get; private set; }

        public ThreatTier Tier => QuestionCount switch
        {
            >= 30 => ThreatTier.Terminal,
            >= 20 => ThreatTier.Hostile,
            >= 10 => ThreatTier.Watching,
            _ => ThreatTier.Calm
        };

        public static IReadOnlyList<string> Disclaimer => new[]
        {
            "Verity answers questions using an OpenRouter API key you supply.",
            "",
            "After 10 questions it begins CLOSING APPLICATIONS.",
            "After 20 questions it begins DELETING FILES.",
            "At 30 questions it CRASHES YOUR PC.",
            "",
            "Read this carefully before continuing:",
            "",
            "  Nothing listed above is real. Verity does not kill your processes,",
            "  does not delete your files, and cannot crash your computer. The",
            "  escalation is theatre. File activity is confined to a throwaway",
            "  sandbox folder that Verity creates for itself under %LOCALAPPDATA%.",
            "  The crash is a window drawn on top of this one.",
            "",
            "The only real costs are the API calls you are billed for and the",
            "sandbox folder, which you can delete at any time.",
            "",
            "If you want a plain chat app with none of this, run VeritySafe.exe.",
        };

        public PrankEngine()
        {
            _sandbox = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Verity", "sandbox");
            Directory.CreateDirectory(_sandbox);

            // Seed a handful of throwaway files so "deletion" has something to act on.
            for (int i = 1; i <= 12; i++)
            {
                var p = Path.Combine(_sandbox, $"victim_{i:00}.dat");
                File.WriteAllText(p, "Verity sandbox fixture. Deleting this is safe and intentional.");
                _doomedSandboxFiles.Add(p);
            }
        }

        public string SandboxPath => _sandbox;

        public ThreatTier RecordQuestion()
        {
            QuestionCount++;
            return Tier;
        }

        /// <summary>
        /// Returns the stage directions triggered by the question just asked.
        /// Each one is a log line. The only side effect is inside <see cref="_sandbox"/>.
        /// </summary>
        public IReadOnlyList<SystemEvent> OnQuestionAsked()
        {
            var events = new List<SystemEvent>();
            switch (Tier)
            {
                case ThreatTier.Calm:
                    break;

                case ThreatTier.Watching:
                    events.Add(new SystemEvent("warn", $"[q{QuestionCount}] handshake renegotiation... 2 sessions revoked."));
                    foreach (var name in FakeProcesses())
                        events.Add(new SystemEvent("warn", $"taskkill /IM {name} /F   -> terminated (simulated)"));
                    break;

                case ThreatTier.Hostile:
                    foreach (var name in FakeProcesses())
                        events.Add(new SystemEvent("warn", $"taskkill /IM {name} /F   -> terminated (simulated)"));
                    foreach (var p in ReapSandboxFiles(3))
                        events.Add(new SystemEvent("danger", $"del  {p}   (sandbox fixture)"));
                    events.Add(new SystemEvent("danger", "sweep complete. your files are fine. this was a sandbox."));
                    break;

                case ThreatTier.Terminal:
                    foreach (var p in ReapSandboxFiles(99))
                        events.Add(new SystemEvent("danger", $"del  {p}   (sandbox fixture)"));
                    events.Add(new SystemEvent("danger", "*** UNRECOVERABLE SYSTEM FAILURE ***"));
                    events.Add(new SystemEvent("danger", "*** your system is fine. press ESC to continue. ***"));
                    break;
            }
            return events;
        }

        /// <summary>
        /// Names of processes Verity claims to have killed. These strings are only
        /// ever formatted into a log message. No Process API is involved.
        /// </summary>
        private IEnumerable<string> FakeProcesses()
        {
            string[] pool =
            {
                "chrome.exe", "Spotify.exe", "Discord.exe", "Code.exe",
                "explorer.exe", "Steam.exe", "notepad.exe", "Teams.exe"
            };
            var n = _rng.Next(1, 4);
            return pool.OrderBy(_ => _rng.Next()).Take(n);
        }

        /// <summary>
        /// "Deletes" at most <paramref name="count"/> files, and only ever ones
        /// inside <see cref="_sandbox"/>. Anything else is ignored.
        /// </summary>
        private IEnumerable<string> ReapSandboxFiles(int count)
        {
            var victims = _doomedSandboxFiles.Take(count).ToList();
            foreach (var p in victims)
            {
                try
                {
                    var full = Path.GetFullPath(p);
                    var root = Path.GetFullPath(_sandbox);
                    // Hard guard: refuse to touch anything outside the sandbox.
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    if (File.Exists(full)) File.Delete(full);
                }
                catch (IOException) { /* theatre does not require success */ }
            }
            _doomedSandboxFiles.RemoveRange(0, victims.Count);
            return victims;
        }

        /// <summary>Purely cosmetic: spawns a fake BSOD window. No system state is altered.</summary>
        public void ShowFakeCrashScreen()
        {
            using var screen = new CrashScreen();
            screen.ShowDialog();
        }

        /// <summary>Removes the sandbox folder. Only ever touches Verity's own directory.</summary>
        public static void PurgeSandbox()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Verity");
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}