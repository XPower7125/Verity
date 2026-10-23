using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Verity.Shared;

namespace Verity
{
    /// <summary>Dev-only: renders a form to a PNG so layout can be eyeballed without a display.</summary>
    internal static class Snapshot
    {
        [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int L, T, R, B; }

        [STAThread]
        internal static void Capture(string outPath, string mode)
        {
            ApplicationConfiguration.Initialize();
            Form form = mode switch
            {
                "prompt" => new KeyPromptForm(""),
                "bubbles" => BuildBubbleTest(),
                "pet" => BuildPetStage(),
                "bubble" => BuildBubbleCapture(),
                _ => new BubbleForm(new SettingsData())
            };

            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(0, 0);
            form.Show();
            for (int i = 0; i < 12; i++)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(60);
            }

            using var bmp = new Bitmap(form.Width, form.Height);
            if (mode == "bubble" || mode == "pet")
            {
                // PrintWindow only draws one window's own contents, so a
                // composite of pet + bubble would come back empty. Grab the
                // screen region instead.
                using var g = Graphics.FromImage(bmp);
                g.CopyFromScreen(form.Location, Point.Empty, form.Size);
            }
            else
            {
                using var g = Graphics.FromImage(bmp);
                var hdc = g.GetHdc();
                PrintWindow(form.Handle, hdc, 2);
                g.ReleaseHdc(hdc);
            }
            bmp.Save(outPath, ImageFormat.Png);
            form.Close();
        }

        /// <summary>Pet alone on a backdrop, to inspect the transparency keying.</summary>
        private static Form BuildPetStage()
        {
            var stage = new Form
            {
                ClientSize = new Size(300, 300),
                BackColor = Color.FromArgb(38, 74, 92),
                StartPosition = FormStartPosition.Manual
            };
            var pet = new PetForm(new SettingsData(), _ => { });
            stage.Shown += (_, _) =>
            {
                pet.Show();
                pet.Location = new Point(88, 88);
                pet.BringToFront();
            };
            return stage;
        }

        /// <summary>
        /// Captures pet + bubble over a desktop-like backdrop, since the
        /// transparency key needs a non-magenta background to be meaningful.
        /// </summary>
        private static Form BuildBubbleCapture()
        {
            var stage = new Form
            {
                ClientSize = new Size(720, 560),
                BackColor = Color.FromArgb(38, 74, 92),
                StartPosition = FormStartPosition.Manual
            };

            var pet = new PetForm(new SettingsData(), _ => { })
            {
                FormBorderStyle = FormBorderStyle.None,
                Location = new Point(470, 360),
                TopMost = false
            };

            var bubble = new BubbleForm(new SettingsData())
            {
                Location = new Point(180, 40)
            };

            stage.Shown += (_, _) =>
            {
                pet.Show();
                bubble.Show();
                // Place after Show: the ctor re-anchors from saved settings.
                // Then use the real placement code, not hand-picked coords.
                pet.Location = new Point(430, 300);
                bubble.PositionBesidePet(pet.Bounds);
                pet.BringToFront();
            };
            return stage;
        }

        /// <summary>Exercises BubbleLog directly with known content, no API needed.</summary>
        private static Form BuildBubbleTest()
        {
            var f = new Form
            {
                ClientSize = new Size(720, 480),
                BackColor = Theme.BgDeep,
                StartPosition = FormStartPosition.Manual
            };
            var log = new BubbleLog { Dock = DockStyle.Fill };
            f.Controls.Add(log);

            f.Shown += (_, _) =>
            {
                log.Add(new Bubble("hi!! what is verity?",
                    Theme.BgLight, Theme.Ink, Theme.Outline, fromUser: false));
                log.Add(new Bubble("ask me anything. i answer things.",
                    Theme.Yellow, Color.FromArgb(0x2A, 0x14, 0x50), Theme.Outline, fromUser: true));
                log.Add(new Bubble("verity is thinking...", Color.Transparent,
                    Theme.InkDim, Color.Transparent, fromUser: false, italic: true, size: 9.5f));
                log.Add(new Bubble("this bubble is long enough to wrap onto several lines so the " +
                    "measurement and word-break path gets exercised properly during the capture.",
                    Theme.BgLight, Theme.Ink, Theme.Outline, fromUser: false));
                log.Add(new Bubble("here is a pasted screenshot, does it look right?",
                    Theme.Yellow, Color.FromArgb(0x2A, 0x14, 0x50), Theme.Outline,
                    fromUser: true, image: SampleImage()));
                // Force the hover state on the first bubble so the copy
                // affordance is visible in the capture.
                log.BeginInvoke(new Action(() => log.HoverFirstCopyable()));
            };
            return f;
        }

        /// <summary>A throwaway bitmap standing in for a pasted clipboard image.</summary>
        private static Bitmap SampleImage()
        {
            var bmp = new Bitmap(320, 180);
            using var g = Graphics.FromImage(bmp);
            using var bg = new System.Drawing.Drawing2D.LinearGradientBrush(
                new Rectangle(0, 0, bmp.Width, bmp.Height), Color.FromArgb(0x24, 0x12, 0x48),
                Color.FromArgb(0x6B, 0x3F, 0xB5), 35f);
            g.FillRectangle(bg, 0, 0, bmp.Width, bmp.Height);
            using var text = new Font("Consolas", 16f, FontStyle.Bold);
            TextRenderer.DrawText(g, "pasted image", text,
                new Rectangle(0, 0, bmp.Width, bmp.Height), Theme.Yellow,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return bmp;
        }
    }
}
