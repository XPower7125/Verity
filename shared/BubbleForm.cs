using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
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
        private readonly SettingsData _settings;
        private readonly BubbleLog _log = new();
        private readonly TextBox _input = new();
        private readonly Panel _composer = new();
        private readonly PictureBox _attached = new();
        private readonly ChunkyButton _removeAttached = new();
        private readonly ChunkyButton _send = new();
        private readonly ChunkyButton _close = new();
        private readonly ChunkyButton _copyLast = new();
        private readonly ChunkyButton _web = new();

        private readonly List<ChatTurn> _history = new();
        private readonly CancellationTokenSource _cts = new();
        private OpenRouterClient? _client;
        private ExaClient? _exa;
        private Image? _attachedImage;
        private bool _webEnabled;

        public BubbleForm(SettingsData settings)
        {
            _settings = settings;

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

            Say("Hey, it's me. It's Verity. Ask me anything, or paste an image with Ctrl+V!");
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
                Text = "verity",
                ForeColor = Theme.Ink,
                Font = Theme.Face(11f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(44, 10),
                BackColor = Color.Transparent
            };
            header.Controls.Add(title);

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

            _web.Text = "web";
            _web.Font = Theme.Face(8f, FontStyle.Bold);
            _web.FillColor = Color.FromArgb(0x3A, 0x1C, 0x6E);
            _web.BorderColor = Color.FromArgb(0x3A, 0x1C, 0x6E);
            _web.TextColor = Theme.InkDim;
            _web.Size = new Size(52, 20);
            _web.Radius = 10;
            _web.Border = 1;
            _web.Cursor = Cursors.Hand;
            _web.Click += (_, _) => ToggleWeb();
            header.Controls.Add(_web);

            _log.CopyRequested += (_, text) => CopyToClipboard(text);

            _composer.Dock = DockStyle.Bottom;
            _composer.Height = ComposerH;
            _composer.BackColor = Color.FromArgb(0x24, 0x12, 0x48);
            _composer.Padding = new Padding(8);
            strip.Controls.Add(_composer);

            _attached.Size = new Size(40, 40);
            _attached.Location = new Point(8, 7);
            _attached.SizeMode = PictureBoxSizeMode.Zoom;
            _attached.BackColor = Color.FromArgb(0x2E, 0x18, 0x58);
            _attached.BorderStyle = BorderStyle.FixedSingle;
            _attached.Visible = false;
            _composer.Controls.Add(_attached);

            _removeAttached.Text = "x";
            _removeAttached.Size = new Size(18, 18);
            _removeAttached.Radius = 9;
            _removeAttached.Border = 1;
            _removeAttached.FillColor = Theme.Red;
            _removeAttached.BorderColor = Theme.Outline;
            _removeAttached.TextColor = Color.White;
            _removeAttached.Font = Theme.Face(7.5f, FontStyle.Bold);
            _removeAttached.Location = new Point(42, 1);
            _removeAttached.Visible = false;
            _removeAttached.Click += (_, _) => ClearAttachment();
            _composer.Controls.Add(_removeAttached);

            _send.Text = "SEND";
            _send.Dock = DockStyle.Right;
            _send.Width = 76;
            _send.FillColor = Theme.Yellow;
            _send.TextColor = Color.FromArgb(0x2A, 0x14, 0x50);
            _send.Font = Theme.Face(9.5f, FontStyle.Bold);
            _send.Click += (_, _) => SendAsync();
            _composer.Controls.Add(_send);

            _input.Multiline = true;
            _input.AcceptsReturn = false;
            _input.Dock = DockStyle.Fill;
            _input.BackColor = Color.FromArgb(0x2E, 0x18, 0x58);
            _input.ForeColor = Theme.Ink;
            _input.BorderStyle = BorderStyle.None;
            _input.Font = Theme.Face(10f);
            _input.PlaceholderText = "ask anything... ctrl+v pastes an image";
            _input.KeyDown += InputKeyDown;
            _composer.Controls.Add(_input);
            _composer.Controls.SetChildIndex(_input, 0);

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
                _copyLast.Location = new Point(_close.Left - _copyLast.Width - 8, 12);
                _web.Location = new Point(_copyLast.Left - _web.Width - 6, 12);
            };
            _close.Location = new Point(366, 8);
            _copyLast.Location = new Point(308, 12);
            _web.Location = new Point(250, 12);

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

        private void ToggleWeb()
        {
            if (string.IsNullOrWhiteSpace(_settings.ExaApiKey))
            {
                Signal("add an Exa API key in settings (right-click the pet) to use web search.", Theme.Orange);
                return;
            }

            _webEnabled = !_webEnabled;
            UpdateWebButton();
        }

        private void UpdateWebButton()
        {
            _web.Text = _webEnabled ? "web on" : "web";
            _web.FillColor = _webEnabled ? Theme.Cyan : Color.FromArgb(0x3A, 0x1C, 0x6E);
            _web.BorderColor = _webEnabled ? Theme.Cyan : Color.FromArgb(0x3A, 0x1C, 0x6E);
            _web.TextColor = _webEnabled ? Color.FromArgb(0x2A, 0x14, 0x50) : Theme.InkDim;
        }

        /// <summary>
        /// Pulls an image off the clipboard. Returns false when the clipboard
        /// has no image, so the text box keeps its normal paste behaviour.
        /// </summary>
        private bool TryPasteImage()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (!Clipboard.ContainsImage()) return false;
                    var image = Clipboard.GetImage();
                    if (image == null) return false;
                    SetAttachment(image);
                    return true;
                }
                catch (Exception) when (attempt < 2)
                {
                    Thread.Sleep(40);
                }
                catch (Exception ex)
                {
                    Signal("could not read the clipboard image: " + ex.Message, Theme.Red);
                    return false;
                }
            }
            return false;
        }

        private void SetAttachment(Image image)
        {
            ClearAttachment();
            _attachedImage = image;
            _attached.Image = image;
            _attached.Visible = true;
            _removeAttached.Visible = true;
            _composer.Padding = new Padding(56, 8, 8, 8);
            _input.Focus();
        }

        private void ClearAttachment()
        {
            if (_attachedImage != null)
            {
                _attached.Image = null;
                _attachedImage.Dispose();
                _attachedImage = null;
            }
            _attached.Visible = false;
            _removeAttached.Visible = false;
            _composer.Padding = new Padding(8);
        }

        /// <summary>
        /// Re-encodes the attachment as PNG, shrinking very large pastes so the
        /// request stays a reasonable size.
        /// </summary>
        private static byte[] EncodePng(Image source)
        {
            const int MaxDim = 1568;
            double scale = Math.Min(1d, Math.Min((double)MaxDim / source.Width, (double)MaxDim / source.Height));
            int w = Math.Max(1, (int)Math.Round(source.Width * scale));
            int h = Math.Max(1, (int)Math.Round(source.Height * scale));

            using var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, w, h);
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        private void ApplyRoundedRegion()
        {
            using var path = Theme.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), 20);
            Region = new Region(path);
            Native.ApplyRoundedCorners(this);
        }

        private void InputKeyDown(object? sender, KeyEventArgs e)
        {
            // ProcessCmdKey normally catches this first; keep a fallback here in
            // case the text box gets the key directly.
            if (e.Control && e.KeyCode == Keys.V && TryPasteImage())
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

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

        private ExaClient Exa =>
            _exa ??= new ExaClient(_settings.ExaApiKey);

        private async void SendAsync()
        {
            var text = _input.Text.Trim();
            if (text.Length == 0 && _attachedImage == null) return;

            byte[]? imagePng = null;
            Image? bubbleImage = null;
            try
            {
                if (_attachedImage != null)
                {
                    imagePng = EncodePng(_attachedImage);
                    bubbleImage = new Bitmap(_attachedImage);
                }
            }
            catch (Exception ex)
            {
                Signal("could not prepare the image: " + ex.Message, Theme.Red);
                return;
            }

            _input.Clear();
            ClearAttachment();
            _send.Enabled = false;
            _log.Add(new Bubble(text, Theme.Yellow, Color.FromArgb(0x2A, 0x14, 0x50),
                Theme.Outline, fromUser: true, image: bubbleImage));

            // An image with no caption still needs a prompt for the model.
            var prompt = text.Length > 0 ? text : "What's in this image?";
            _history.Add(new ChatTurn("user", prompt, imagePng));

            Whisper("...");

            // With web mode on, fetch live results and hand them to the model
            // as extra system context. Search failures are non-fatal.
            string webContext = "";
            if (_webEnabled && text.Length > 0 && !string.IsNullOrWhiteSpace(_settings.ExaApiKey))
            {
                try
                {
                    var results = await Exa.SearchAsync(prompt, _cts.Token);
                    webContext = Persona.WebContext(prompt, results);
                    if (results.Count == 0)
                        Signal("no web results for that.", Theme.Orange);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    if (!IsDisposed) Signal("web search failed: " + ex.Message, Theme.Orange);
                }
            }

            try
            {
                var system = Persona.Default;
                if (webContext.Length > 0)
                    system += "\n\n" + webContext;

                var turns = new List<ChatTurn> { new ChatTurn("system", system) };
                turns.AddRange(_history);

                var reply = await Client.CompleteAsync(turns, _cts.Token);
                if (IsDisposed) return;
                _history.Add(new ChatTurn("assistant", reply));
                Say(reply);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsDisposed) Signal(ex.Message, Theme.Red);
            }

            if (IsDisposed) return;
            _send.Enabled = true;
            _input.Focus();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V) && TryPasteImage())
                return true;

            if (keyData == Keys.Escape)
            {
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearAttachment();
            }
            base.Dispose(disposing);
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
