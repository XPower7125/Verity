using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Verity.Shared
{
    public sealed class Bubble
    {
        public string Text { get; }
        public Color Fill { get; }
        public Color Ink { get; }
        public Color Border { get; }
        public bool FromUser { get; }
        public bool Italic { get; }
        public float Size { get; }

        public Bubble(string text, Color fill, Color ink, Color border, bool fromUser, bool italic = false, float size = 10f)
        {
            Text = text;
            Fill = fill;
            Ink = ink;
            Border = border;
            FromUser = fromUser;
            Italic = italic;
            Size = size;
        }

        /// <summary>True for italic, bubble-less system notices, which have nothing worth copying.</summary>
        public bool Copyable => Fill != Color.Transparent && Text.Length > 0;
    }

    /// <summary>
    /// Auto-scrolling chat area that paints each line as a rounded speech bubble,
    /// user lines right-aligned, Verity lines left-aligned.
    /// </summary>
    public sealed class BubbleLog : Control
    {
        private readonly List<Bubble> _items = new();
        private int _scroll;
        private int _hoverIndex = -1;
        private bool _hoverOnCopy;

        /// <summary>Raised when a bubble's copy button is clicked, carrying the bubble text.</summary>
        public event EventHandler<string>? CopyRequested;

        /// <summary>Text of the most recent copyable bubble, for a "copy last" affordance.</summary>
        public string? LastCopyableText
        {
            get
            {
                for (int i = _items.Count - 1; i >= 0; i--)
                    if (_items[i].Copyable) return _items[i].Text;
                return null;
            }
        }

        public BubbleLog()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.BgDeep;
            Font = Theme.Face(10.5f);
            // MouseMove/MouseLeave fire without any tracking flag; no setup needed.
        }

        private const int Pad = 14;
        private const int Gap = 8;
        private const int MaxBubbleWidth = 520;

        public void Add(Bubble b)
        {
            _items.Add(b);
            _scroll = int.MaxValue;
            Invalidate();
        }

        public void ClearAll()
        {
            _items.Clear();
            _scroll = 0;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _scroll -= e.Delta / 120 * 60;
            ClampScroll();
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Up or Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.PageUp) { _scroll -= 120; ClampScroll(); Invalidate(); e.Handled = true; }
            if (e.KeyCode == Keys.PageDown) { _scroll += 120; ClampScroll(); Invalidate(); e.Handled = true; }
            if (e.KeyCode == Keys.Home) { _scroll = 0; Invalidate(); e.Handled = true; }
            if (e.KeyCode == Keys.End) { _scroll = int.MaxValue; Invalidate(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        private void ClampScroll()
        {
            _scroll = Math.Max(0, Math.Min(_scroll, MaxScroll()));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _scroll = int.MaxValue;
        }

        private sealed class Laid
        {
            public Bubble B = null!;
            public Rectangle Rect;
            public Rectangle TextRect;
            public Rectangle CopyRect;
            public int Index;
        }

        /// <summary>Copy glyph target, top-right inside the bubble.</summary>
        private static Rectangle CopyTarget(Rectangle bubble) =>
            new Rectangle(bubble.Right - CopySize - 5, bubble.Top + 4, CopySize, CopySize);

        private const int CopySize = 22;

        private List<Laid> LayoutBubbles()
        {
            var laid = new List<Laid>();
            // Clamp before subtracting: _scroll can be int.MaxValue as a sentinel
            // for "pinned to bottom", and Pad - int.MaxValue overflows to a large
            // positive offset, parking every bubble off-screen.
            int scroll = Math.Min(_scroll, MaxScroll());
            int y = Pad - scroll;

            foreach (var b in _items)
            {
                var font = Theme.Face(b.Size, b.Italic ? FontStyle.Italic : FontStyle.Regular);

                // Reserve room for the copy button *before* measuring, so the
                // text re-wraps instead of being clipped at the new right edge.
                int allowance = b.Copyable ? CopySize + 8 : 0;
                int avail = Math.Max(120, Math.Min(MaxBubbleWidth, Width - 120));
                int textAvail = Math.Max(60, avail - allowance);

                var sz = TextRenderer.MeasureText(b.Text, font, new Size(textAvail, 0),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                int w = Math.Min(avail, sz.Width + allowance) + Pad * 2;
                int h = sz.Height + Pad * 2 - 4;
                if (h < 34) h = 34;

                int x = b.FromUser ? Width - Pad - w : Pad;
                if (b.Border == Color.Empty && b.Fill == Color.Transparent)
                    x = Math.Max(Pad, Width / 2 - w / 2);

                var rect = new Rectangle(x, y, w, h);
                laid.Add(new Laid
                {
                    B = b,
                    Rect = rect,
                    TextRect = new Rectangle(x + Pad, y + Pad - 2,
                        w - Pad * 2 - allowance,
                        h - Pad * 2 + 4),
                    CopyRect = CopyTarget(rect),
                    Index = laid.Count
                });
                y += h + Gap;
            }
            return laid;
        }

        private int MaxScroll()
        {
            int total = Pad * 2;
            foreach (var b in _items)
            {
                var font = Theme.Face(b.Size, b.Italic ? FontStyle.Italic : FontStyle.Regular);
                // Same allowance as LayoutBubbles, or the scroll height drifts
                // out of sync with what actually gets drawn.
                int allowance = b.Copyable ? CopySize + 8 : 0;
                int avail = Math.Max(120, Math.Min(MaxBubbleWidth, Width - 120));
                int textAvail = Math.Max(60, avail - allowance);
                var sz = TextRenderer.MeasureText(b.Text, font, new Size(textAvail, 0),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                total += Math.Max(34, sz.Height + Pad * 2 - 4) + Gap;
            }
            return Math.Max(0, total - Height);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            int idx = -1;
            bool onCopy = false;
            foreach (var l in LayoutBubbles())
            {
                if (!l.B.Copyable) continue;
                if (!l.Rect.Contains(e.Location)) continue;
                idx = l.Index;
                onCopy = l.CopyRect.Contains(e.Location);
                break;
            }

            if (idx != _hoverIndex || onCopy != _hoverOnCopy)
            {
                _hoverIndex = idx;
                _hoverOnCopy = onCopy;
                Invalidate();
            }
            Cursor = onCopy ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1 || _hoverOnCopy)
            {
                _hoverIndex = -1;
                _hoverOnCopy = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_hoverIndex < 0 || !_hoverOnCopy) return;

            foreach (var l in LayoutBubbles())
            {
                if (l.Index == _hoverIndex && l.CopyRect.Contains(e.Location))
                {
                    CopyRequested?.Invoke(this, l.B.Text);
                    _hoverIndex = -1;
                    _hoverOnCopy = false;
                    Cursor = Cursors.Default;
                    Invalidate();
                    return;
                }
            }
        }

        /// <summary>Draws the copy affordance: two offset sheets.</summary>
        private static void PaintCopyGlyph(Graphics g, Rectangle r, Color ink, bool active)
        {
            int w = 9, h = 11;
            int x = r.X + (r.Width - w) / 2 + 2;
            int y = r.Y + (r.Height - h) / 2;

            using var back = new Pen(ink, 1.6f);
            // rear sheet
            g.DrawRectangle(back, x + 3, y - 1, w, h);
            // front sheet
            using var fillBrush = new SolidBrush(active
                ? Color.FromArgb(70, ink)
                : Color.FromArgb(0, ink));
            g.FillRectangle(fillBrush, x, y + 3, w, h);
            g.DrawRectangle(back, x, y + 3, w, h);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.BgDeep);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            foreach (var l in LayoutBubbles())
            {
                if (l.Rect.Bottom < -20 || l.Rect.Top > Height + 20) continue;

                if (l.B.Fill != Color.Transparent)
                {
                    using var path = Theme.RoundRect(l.Rect, 16);
                    using var br = new SolidBrush(l.B.Fill);
                    g.FillPath(br, path);
                    if (l.B.Border != Color.Transparent)
                    {
                        using var pen = new Pen(l.B.Border, 3);
                        g.DrawPath(pen, path);
                    }
                }

                using var f = l.B.Italic ? Theme.Face(l.B.Size, FontStyle.Italic) : Theme.Face(l.B.Size);
                TextRenderer.DrawText(g, l.B.Text, f, l.TextRect, l.B.Ink,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

                if (l.B.FromUser) DrawTail(g, l.Rect, l.B.Fill, l.B.Border);
                else DrawTailLeft(g, l.Rect, l.B.Fill, l.B.Border);

                // Copy button appears on the hovered bubble only.
                if (l.Index == _hoverIndex && l.B.Copyable)
                {
                    bool active = _hoverOnCopy;
                    Theme.DrawRound(g, l.CopyRect, 7,
                        active ? Theme.Outline : Color.FromArgb(0x5A, 0x2C, 0xA0),
                        active ? Theme.Yellow : Color.FromArgb(0, 0, 0, 0), 1);
                    PaintCopyGlyph(g, l.CopyRect, Theme.Yellow, active);
                }
            }

            // Fade at the top when scrolled up, so there's a visual cue.
            if (_scroll > 0 && _scroll < MaxScroll())
            {
                using var grad = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, 24), Theme.BgDeep, Color.FromArgb(0, Theme.BgDeep),
                    LinearGradientMode.Vertical);
                g.FillRectangle(grad, 0, 0, Width, 24);
            }
        }

        private static void DrawTail(Graphics g, Rectangle r, Color fill, Color border)
        {
            if (fill == Color.Transparent) return;
            var pts = new[]
            {
                new Point(r.Right, r.Bottom - 18),
                new Point(r.Right, r.Bottom - 2),
                new Point(r.Right - 13, r.Bottom - 2)
            };
            using var b = new SolidBrush(fill);
            g.FillPolygon(b, pts);
            if (border != Color.Transparent)
            {
                using var pen = new Pen(border, 3);
                g.DrawLines(pen, pts);
            }
        }

        private static void DrawTailLeft(Graphics g, Rectangle r, Color fill, Color border)
        {
            if (fill == Color.Transparent) return;
            var pts = new[]
            {
                new Point(r.Left, r.Bottom - 18),
                new Point(r.Left, r.Bottom - 2),
                new Point(r.Left + 13, r.Bottom - 2)
            };
            using var b = new SolidBrush(fill);
            g.FillPolygon(b, pts);
            if (border != Color.Transparent)
            {
                using var pen = new Pen(border, 3);
                g.DrawLines(pen, pts);
            }
        }

        /// <summary>Programmatically hovers the first copyable bubble. Used by the snapshot harness.</summary>
        public void HoverFirstCopyable()
        {
            foreach (var l in LayoutBubbles())
            {
                if (l.B.Copyable)
                {
                    _hoverIndex = l.Index;
                    _hoverOnCopy = false;
                    Invalidate();
                    return;
                }
            }
        }

        public void FocusLog() => Focus();
    }
}