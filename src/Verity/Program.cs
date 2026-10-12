using System;
using System.Drawing;
using System.Windows.Forms;
using Verity.Shared;

namespace Verity
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--snapshot")
            {
                Snapshot.Capture(args[1], args.Length > 2 ? args[2] : "chat");
                return;
            }

            ApplicationConfiguration.Initialize();

            // OutputType is WinExe; borrow the parent console for the CLI modes.
            if (args.Length >= 1 && args[0].StartsWith("--")) Native.AttachConsole(-1);

            if (args.Length >= 1 && args[0] == "--selftest")
            {
                // Verifies the pet window paints its background with the
                // transparency key. PrintWindow and screen capture both lie
                // about transparency, so read the window's own pixels.
                var pet = new PetForm(VerityMode.Prank, new SettingsData(), _ => { });
                pet.Show();
                Application.DoEvents();

                using var bmp = new Bitmap(pet.Width, pet.Height);
                pet.DrawToBitmap(bmp, new Rectangle(0, 0, pet.Width, pet.Height));

                var keyArgb = Color.Magenta.ToArgb();
                var probes = new (string Name, Point P)[]
                {
                    ("top-left", new Point(2, 2)),
                    ("top-right", new Point(pet.Width - 3, 2)),
                    ("bottom-left", new Point(2, pet.Height - 3)),
                    ("bottom-right", new Point(pet.Width - 3, pet.Height - 3)),
                    ("mid-left", new Point(2, pet.Height / 2)),
                    ("mid-right", new Point(pet.Width - 3, pet.Height / 2)),
                };

                int bad = 0;
                foreach (var (name, p) in probes)
                {
                    int argb = bmp.GetPixel(p.X, p.Y).ToArgb();
                    bool ok = argb == keyArgb;
                    if (!ok) bad++;
                    Console.WriteLine($"{name,-13} {bmp.GetPixel(p.X, p.Y)}  {(ok ? "keyed (transparent)" : "OPAQUE <-- bug")}");
                }
                Console.WriteLine(bad == 0
                    ? "PASS: every margin pixel is the transparency key."
                    : $"FAIL: {bad} margin pixel(s) are opaque.");
                pet.Close();
                return;
            }

            if (args.Length >= 1 && args[0] == "--dump-settings")
            {
                // Confirms the registry values parse, without launching the UI.
                var loaded = Settings.Load();
                Console.WriteLine($"path    = {Settings.RegistryPath}");
                Console.WriteLine($"api_key = {(string.IsNullOrEmpty(loaded.ApiKey) ? "<empty>" : loaded.ApiKey.Length + " chars")}");
                Console.WriteLine($"model   = {loaded.Model}");
                Console.WriteLine($"consent = {loaded.ConsentAccepted}");
                Console.WriteLine($"pet     = {loaded.PetX},{loaded.PetY}");
                return;
            }

            // Env vars still win, so scripted launches can bypass the registry.
            var settings = Settings.Load();
            var key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            var model = Environment.GetEnvironmentVariable("VERITY_MODEL");
            if (!string.IsNullOrWhiteSpace(key)) settings.ApiKey = key.Trim();
            if (!string.IsNullOrWhiteSpace(model)) settings.Model = model.Trim();

            Application.Run(new VerityHost(VerityMode.Prank, settings));
        }
    }
}