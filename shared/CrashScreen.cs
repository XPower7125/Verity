using System;
using System.Drawing;
using System.Windows.Forms;

namespace Verity.Shared
{
    /// <summary>
    /// A cosmetic imitation of a stop screen, drawn inside this app's own window.
    /// It has no access to the display pipeline, the kernel, or any real device.
    /// Dismissed with ESC or any key.
    /// </summary>
    public sealed class CrashScreen : Form
    {
        public CrashScreen()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(760, 420);
            Icon = Brand.Icon;
            BackColor = Color.FromArgb(0, 0x4E, 0x9E);
            TopMost = true;
            KeyPreview = true;
            Text = "CrashScreen";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape || keyData == Keys.Enter || keyData == Keys.Space)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            using var title = new Font("Consolas", 22f, FontStyle.Bold);
            using var body = new Font("Consolas", 11f);
            using var brush = new SolidBrush(Color.White);

            g.DrawString("VERITY HAS SEIZED YOUR MACHINE", title, brush, 40, 40);
            g.DrawString(
                "A problem has been detected and Verity has been shut down to prevent damage.\r\n\r\n" +
                "STOP: this screen is a prop. Nothing happened to your computer.\r\n" +
                "No process was terminated. No file was deleted. No crash occurred.\r\n\r\n" +
                "Press ESC to return to the conversation.",
                body, brush, 40, 110);

            for (int y = 300; y < ClientSize.Height; y += 4)
                g.DrawLine(Pens.White, 0, y, ClientSize.Width, y);
        }
    }
}