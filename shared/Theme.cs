using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Verity.Shared
{
    /// <summary>
    /// The look: thick-bordered rounded panels, saturated purple, a playful
    /// rounded font. Colours live here so both builds stay consistent.
    /// </summary>
    public static class Theme
    {
        // Backdrop
        public static readonly Color BgDeep = Color.FromArgb(0x35, 0x1B, 0x5E);      // window
        public static readonly Color BgPanel = Color.FromArgb(0x4B, 0x2A, 0x86);     // panels
        public static readonly Color BgLight = Color.FromArgb(0x6B, 0x3F, 0xB5);     // raised

        // Ink
        public static readonly Color Ink = Color.FromArgb(0xFF, 0xFF, 0xFF);
        public static readonly Color InkDim = Color.FromArgb(0xCF, 0xBE, 0xEE);

        // Accents
        public static readonly Color Yellow = Color.FromArgb(0xFF, 0xD4, 0x3B);
        public static readonly Color Red = Color.FromArgb(0xFF, 0x5C, 0x5C);
        public static readonly Color Cyan = Color.FromArgb(0x8B, 0xE8, 0xFF);
        public static readonly Color Green = Color.FromArgb(0x9C, 0xF0, 0x8A);
        public static readonly Color Orange = Color.FromArgb(0xFF, 0xB4, 0x5C);

        public static readonly Color Outline = Color.FromArgb(0x1A, 0x0C, 0x33);    // chunky border

        /// <summary>Chat face: rounded and friendly. Falls back sensibly.</summary>
        public static Font Face(float size, FontStyle style = FontStyle.Regular)
        {
            var candidates = new[] { "Comic Sans MS", "Segoe Print", "Arial Rounded MT Bold" };
            foreach (var name in candidates)
            {
                try
                {
                    var f = new Font(name, size, style);
                    if (f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return f;
                    f.Dispose();
                }
                catch (ArgumentException) { }
            }
            return new Font("Segoe UI", size, style);
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color fill)
        {
            using var path = RoundRect(r, radius);
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color fill, Color border, int borderWidth)
        {
            using var path = RoundRect(r, radius);
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
            using var p = new Pen(border, borderWidth);
            g.DrawPath(p, path);
        }
    }

    /// <summary>A rounded, outlined container panel with an optional title notch.</summary>
    public class ChunkyPanel : Panel
    {
        public int Radius { get; set; } = 18;
        public int Border { get; set; } = 3;
        public Color FillColor { get; set; } = Theme.BgPanel;
        public Color BorderColor { get; set; } = Theme.Outline;

        public ChunkyPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = FillColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.DrawRound(e.Graphics, r, Radius, FillColor, BorderColor, Border);
        }
    }

    /// <summary>Flat rounded button that paints its own chunky border.</summary>
    public class ChunkyButton : Button
    {
        public int Radius { get; set; } = 16;
        public int Border { get; set; } = 3;
        public Color FillColor { get; set; } = Theme.BgLight;
        public Color BorderColor { get; set; } = Theme.Outline;
        public Color TextColor { get; set; } = Theme.Ink;

        public ChunkyButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        private Color CurrentFill => Enabled ? FillColor : Theme.BgDeep;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            var fill = CurrentFill;
            if (Focused) fill = ControlPaint.Light(fill, 0.12f);
            if (MouseIsOver) fill = ControlPaint.Light(fill, 0.08f);
            if (!Enabled) fill = ControlPaint.Dark(fill, 0.25f);

            Theme.DrawRound(g, r, Radius, fill, BorderColor, Border);

            TextRenderer.DrawText(
                g, Text, Font, r, TextColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        public bool MouseIsOver { get; set; }
    }
}