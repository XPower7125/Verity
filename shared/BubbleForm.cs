using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace Verity.Shared
{
    /// <summary>
    /// The chat bubble that pops up next to the pet: borderless, transparent
    /// outside a rounded rectangle, always on top, no taskbar button.
    /// </summary>
    public sealed class BubbleForm : Form
    {
        private readonly VerityMode _mode;
        private readonly PrankEngine? _engine;
        private readonly SettingsData _settings;
        private readonly BubbleLog _log = new();
        private readonly TextBox _input = new();
        private readonly ChunkyButton _send = new();
        private readonly Label _counter = new();
        private readonly ChunkyButton _close = new();
        private readonly ChunkyButton _purge = new();
        private readonly ChunkyButton _copyLast = new();

        private readonly List<ChatTurn> _history = new();
        private readonly CancellationTokenSource _cts = new();
        private OpenRouterClient? _client;

        public BubbleForm(VerityMode mode, SettingsData settings)
        {
            _mode = mode;
            _settings = settings;
            if (mode == VerityMode.Prank) _engine = new PrankEngine();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            ClientSize = new Size(404, 372);
            Font = Theme.Face(10.5f);
            Icon = Brand.Icon;
            KeyPreview = true;

            BuildUi();
            this.Resize += (_, _) => ApplyRoundedRegion();
            ApplyRoundedRegion();

            Say("Hey, it's me. It's Verity. Ask me anything!");
            if (_engine != null)
                Whisper("nothing you say can hurt anything here. but let's find out.");
        }

        public event EventHandler? BubbleClosed;

        private void BuildUi()
        {
            // One top-level strip stack: header, transcript, composer. Explicit
            // heights and a manual Bounds assignment for the transcript, because
            // nested Fill docking against sibling Top/Bottom panels clipped the
            // first bubble behind the header.
            var strip = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(0x1A, 0x0C, 0x30) };
            Controls.Add(strip);

            const int HeaderH = 42;
            const int ComposerH = 54;
            const int Pad = 6;

            var header = new Panel { Dock = DockStyle.Top, Height = HeaderH, BackColor = Color.FromArgb(0x24, 0x12, 0x48) };
            strip.Controls.Add(header);

            var face = new PictureBox
            {
                Image = Brand.RoundFace(30),
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(8, 6),
                Size = new Size(30, 30),
                BackColor = Color.FromArgb(0x24, 0x12, 0x48)
            };
            header.Controls.Add(face);

            var title = new Label
            {
                Text = _mode == VerityMode.Prank ? "verity" : "verity safe",
                ForeColor = Theme.Ink,
                Font = Theme.Face(11f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(44, 4),
                BackColor = Color.Transparent
            };
            header.Controls.Add(title);

            _counter.AutoSize = true;
            _counter.ForeColor = Theme.Yellow;
            _counter.Font = Theme.Face(8.5f, FontStyle.Bold);
            _counter.Text = _mode == VerityMode.Prank ? "0 questions" : "";
            _counter.Location = new Point(46, 23);
            header.Controls.Add(_counter);

            _purge.Text = "clear sandbox";
            _purge.Font = Theme.Face(8f);
            _purge.FillColor = Color.FromArgb(0x24, 0x12, 0x48);
            _purge.BorderColor = Color.FromArgb(0x24, 0x12, 0x48);
            _purge.TextColor = Theme.InkDim;
            _purge.Size = new Size(96, 18);
            _purge.Radius = 9;
            _purge.Border = 1;
            _purge.Cursor = Cursors.Hand;
            _purge.Visible = _mode == VerityMode.Prank;
            _purge.Click += (_, _) =>
            {
                PrankEngine.PurgeSandbox();
                Whisper("sandbox removed.");
            };
            header.Controls.Add(_purge);

            _copyLast.Text = "copy";
            _copyLast.Font = Theme.Face(8f, FontStyle.Bold);
            _copyLast.FillColor = Color.FromArgb(0x24, 0x12, 0x48);
            _copyLast.BorderColor = Color.FromArgb(0x3A, 0x1C, 0x6E);
            _copyLast.TextColor = Theme.InkDim;
            _copyLast.Size = new Size(50, 20);
            _copyLast.Radius = 10;
            _copyLast.Border = 1;
            _copyLast.Cursor = Cursors.Hand;
            _copyLast.Click += (_, _) =>
            {
                var last = _log.LastCopyableText;
                if (last != null) CopyToClipboard(last);
            };
            header.Controls.Add(_copyLast);

            _log.CopyRequested += (_, text) => CopyToClipboard(text);

            var composer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = ComposerH,
                BackColor = Color.FromArgb(0x24, 0x12, 0x48),
                Padding = new Padding(8)
            };
            strip.Controls.Add(composer);

            _send.Text = "SEND";
            _send.Dock = DockStyle.Right;
            _send.Width = 76;
            _send.FillColor = Theme.Yellow;
            _send.TextColor = Color.FromArgb(0x2A, 0x14, 0x50);
            _send.Font = Theme.Face(9.5f, FontStyle.Bold);
            _send.Click += (_, _) => SendAsync();
            composer.Controls.Add(_send);

            _input.Multiline = true;
            _input.AcceptsReturn = false;
            _input.Dock = DockStyle.Fill;
            _input.BackColor = Color.FromArgb(0x2E, 0x18, 0x58);
            _input.ForeColor = Theme.Ink;
            _input.BorderStyle = BorderStyle.None;
            _input.Font = Theme.Face(10f);
            _input.KeyDown += InputKeyDown;
            composer.Controls.Add(_input);
            composer.Controls.SetChildIndex(_input, 0);

            strip.Controls.Add(_log);

            _close.Text = "X";
            _close.Size = new Size(30, 26);
            _close.Radius = 13;
            _close.FillColor = Color.FromArgb(0x3A, 0x1C, 0x6E);
            _close.TextColor = Theme.Ink;
            _close.Font = Theme.Face(9f, FontStyle.Bold);
            _close.Cursor = Cursors.Hand;
            _close.Click += (_, _) => Close();
            header.Controls.Add(_close);
            header.Resize += (_, _) =>
            {
                _close.Location = new Point(header.Width - _close.Width - 8, 8);
                int right = header.Width - _close.Width - 14;
                _purge.Location = new Point(right - _purge.Width, 12);
                _copyLast.Location = new Point(_purge.Left - _copyLast.Width - 6, 12);
            };
            _purge.Location = new Point(300, 12);
            _copyLast.Location = new Point(244, 12);

            // Transcript takes whatever is left between the two strips.
            void LayoutLog() =>
                _log.Bounds = new Rectangle(Pad, HeaderH,
                    Math.Max(40, strip.ClientSize.Width - Pad * 2),
                    Math.Max(40, strip.ClientSize.Height - HeaderH - ComposerH - Pad));
            strip.Resize += (_, _) => LayoutLog();
            this.Resize += (_, _) => LayoutLog();
            LayoutLog();
        }

        /// <summary>
        /// Copies to the clipboard and says so. The clipboard is a shared OS
        /// resource and throws when another process holds it, so this is a
        /// best-effort operation rather than one the UI depends on.
        /// </summary>
        private void CopyToClipboard(string text)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (Clipboard.ContainsText())
                        Clipboard.Clear();
                    Clipboard.SetText(text);
                    Whisper("copied.");
                    return;
                }
                catch (Exception) when (attempt < 2)
                {
                    Thread.Sleep(40);
                }
                catch (Exception ex)
                {
                    Signal("could not copy: " + ex.Message, Theme.Red);
                    return;
                }
            }
        }

        private void ApplyRoundedRegion()
        {
            using var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 20);
            Region = new Region(path);
            Native.ApplyRoundedCorners(this);
        }

        private void InputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendAsync();
            }
        }

        private void Say(string text) =>
            _log.Add(new Bubble(text, Theme.BgLight, Theme.Ink, Theme.Outline, fromUser: false));

        private void Whisper(string text) =>
            _log.Add(new Bubble(text, Color.Transparent, Theme.InkDim, Color.Transparent,
                fromUser: false, italic: true, size: 9.5f));

        private void Signal(string text, Color ink) =>
            _log.Add(new Bubble(text, Color.Transparent, ink, Color.Transparent,
                fromUser: false, italic: true, size: 9.5f));

        private OpenRouterClient Client =>
            _client ??= new OpenRouterClient(_settings.ApiKey, _settings.Model);

        private async void SendAsync()
        {
            var text = _input.Text.Trim();
            if (text.Length == 0) return;

            _input.Clear();
            _send.Enabled = false;
            _log.Add(new Bubble(text, Theme.Yellow, Color.FromArgb(0x2A, 0x14, 0x50),
                Theme.Outline, fromUser: true));

            _history.Add(new ChatTurn("user", text));
            _engine?.RecordQuestion();
            if (_engine != null)
                _counter.Text = $"{_engine.QuestionCount} questions";

            Whisper("...");

            try
            {
                var system = _mode == VerityMode.Prank
                    ? Persona.Prank + Persona.PrankPressure(_engine!.QuestionCount)
                    : Persona.Safe;

                var turns = new List<ChatTurn> { new ChatTurn("system", system) };
                turns.AddRange(_history);

                var reply = await Client.CompleteAsync(turns, _cts.Token);
                _history.Add(new ChatTurn("assistant", reply));
                Say(reply);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Signal(ex.Message, Theme.Red);
            }

            _send.Enabled = true;
            _input.Focus();
            TriggerStageDirection();
        }

        private void TriggerStageDirection()
        {
            if (_engine == null) return;

            foreach (var ev in _engine.OnQuestionAsked())
            {
                Signal(ev.Message, ev.Level == "danger" ? Theme.Red : Theme.Orange);
            }

            if (_engine.Tier == ThreatTier.Terminal)
            {
                Signal("press ESC. please.", Theme.Red);
                using var screen = new CrashScreen();
                screen.ShowDialog(this);
                Say("nothing broke. see?");
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                // Escape closes the bubble, except once the fake crash is up.
                if (_engine?.Tier == ThreatTier.Terminal) return true;
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _cts.Cancel();
            BubbleClosed?.Invoke(this, EventArgs.Empty);
            base.OnFormClosed(e);
        }

        /// <summary>Places the bubble above-left of the pet, clamped to the screen.</summary>
        public void PositionBesidePet(Rectangle petBounds)
        {
            var screen = Screen.FromRectangle(petBounds).WorkingArea;

            int x = petBounds.Left - Width + petBounds.Width / 2;
            int y = petBounds.Top - Height - 10;

            if (y < screen.Top + 8)
            {
                // No room above: sit below the pet instead.
                y = petBounds.Bottom + 10;
                if (y + Height > screen.Bottom)
                    y = screen.Bottom - Height - 8;
            }

            x = Math.Clamp(x, screen.Left + 8, screen.Right - Width - 8);
            y = Math.Clamp(y, screen.Top + 8, screen.Bottom - Height - 8);

            Location = new Point(x, y);
        }
    }
}